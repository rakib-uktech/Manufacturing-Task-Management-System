using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Mono.TextTemplating;
using NetSuite;
using Npgsql;
using System.Data;
using System.Data.Odbc;
using System.Xml.Linq;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using MQTTnet;
using MQTTnet.Client;
using System.Text;


namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Assign_TaskModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public TaskInfo TaskInfo { get; set; } = new TaskInfo();
        private readonly NetSuiteClient _netSuiteClient;
        private readonly IConfiguration _configuration;

        public string DefaultConnection { get; private set; }
        public string ConnectionString { get; private set; }

        public List<TaskInfo> TaskList { get; set; } = new List<TaskInfo>(); // New combined list
        public List<TaskInfo> WorkOrderlist { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> AssignTolist { get; set; } = new List<TaskInfo>();

        // Constructor with Dependency Injection
        public Assign_TaskModel(NetSuiteClient netSuiteClient, IConfiguration configuration)
        {
            _netSuiteClient = netSuiteClient;
            _configuration = configuration;
        }

        // OnGet method to fetch data from NetSuite ODBC
        public void OnGet()
        {
            try
            {
                ConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");
                Console.WriteLine($"[DEBUG] ODBC Connection String: {ConnectionString}");

                using (OdbcConnection connection = new OdbcConnection(ConnectionString))
                {
                    connection.Open();
                    Console.WriteLine("[DEBUG] Connected to NetSuite ODBC successfully!");

                    // Clear TaskList to avoid duplicates
                    TaskList.Clear();

                    string lastMonthDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");

                    // Query to get unique work orders based on tranid
                    string sql = $@"
                        SELECT a.tranid, 
                               MIN(c.itemid) AS itemid, 
                               MIN(c.displayname) AS displayname,
                               MIN(c.custitemproduct_spec_gtin) AS gtin 
                        FROM transaction a
                        INNER JOIN transactionLine b ON a.id = b.createdfrom
                        INNER JOIN item c ON b.item = c.id
                        WHERE RTRIM(a.recordtype) = 'workorder' AND a.createddate >= {{d '{lastMonthDate}'}}
                        GROUP BY a.tranid
                        ORDER BY a.tranid DESC";

                    Console.WriteLine($"[DEBUG] Final SQL Query: {sql}");

                    using (OdbcCommand command = new OdbcCommand(sql, connection))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var taskInfo = new TaskInfo
                                {
                                    Ref_No = reader["tranid"].ToString(),
                                    Task_Name = !reader.IsDBNull(reader.GetOrdinal("itemid")) ? reader["itemid"].ToString() : "Unknown",
                                    Task_Description = !reader.IsDBNull(reader.GetOrdinal("displayname")) ? reader["displayname"].ToString() : "No Description",
                                    GTIN = !reader.IsDBNull(reader.GetOrdinal("gtin")) ? reader["gtin"].ToString() : "No Gtin"
                                };

                                TaskList.Add(taskInfo);
                                Console.WriteLine($"[DEBUG] Retrieved Item: {taskInfo.Task_Name} with Description: {taskInfo.Task_Description}");
                            }
                        }
                    }

                    // Clear AssignTolist to avoid duplicates
                    AssignTolist.Clear();

                    // Fetching Machine Name
                    sql = "SELECT DISTINCT groupname FROM entitygroup WHERE ismanufacturingworkcenter = 'T' ORDER BY groupname";
                    using (OdbcCommand command = new OdbcCommand(sql, connection))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AssignTolist.Add(new TaskInfo { Assign_To = reader["groupname"].ToString() });
                                Console.WriteLine($"[DEBUG] Retrieved groupname: {reader["groupname"]}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ODBC Connection Failed: {ex.ToString()}");
                ErrorMessage = ex.Message;
            }

        }

        // OnPost method to handle form submission and database insertion
        public IActionResult OnPost()
        {
            try
            {
                // Assign values from form
                TaskInfo task = new TaskInfo
                {
                    Ref_No = Request.Form["Work_Order"],
                    Task_Type = "Assembly Build",
                    Task_Name = Request.Form["Task_Name"],
                    Task_Description = Request.Form["Task_Description"],
                    Assign_To = Request.Form["Assign_To"],
                    Qty = Convert.ToInt32(Request.Form["Qty"]),
                    Unit = Request.Form["Unit"],
                    Priority_Level = Request.Form["Priority_Level"],
                    Comments = Request.Form["Comments"],
                    GTIN = Request.Form["GTIN"]
                };

                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                // Database insert query for task
                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO task_manager
                        (Ref_No, Task_Type, Task_Name, Task_Description, Assign_To, Qty, Unit, Status, Priority_Level, Created_by, Comments)
                        VALUES (@Ref_No, @Task_Type, @Task_Name, @Task_Description, @Assign_To, @Qty, @Unit, @Status, @Priority_Level, @Created_by, @Comments)";

                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Ref_No", task.Ref_No);
                        command.Parameters.AddWithValue("@Task_Type", task.Task_Type);
                        command.Parameters.AddWithValue("@Task_Name", task.Task_Name);
                        command.Parameters.AddWithValue("@Task_Description", task.Task_Description);
                        command.Parameters.AddWithValue("@Assign_To", task.Assign_To);
                        command.Parameters.AddWithValue("@Qty", task.Qty);
                        command.Parameters.AddWithValue("@Unit", task.Unit);
                        command.Parameters.AddWithValue("@Status", "A");
                        command.Parameters.AddWithValue("@Priority_Level", task.Priority_Level);
                        command.Parameters.AddWithValue("@Created_by", User.Identity?.Name ?? "System");
                        command.Parameters.AddWithValue("@Comments", task.Comments);
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                }

                // Send WhatsApp Notification to multiple numbers
                SendWhatsAppNotificationToMultipleRecipients(task);
                //SendSmsNotificationToMultipleRecipients(task);
                
                // NEW: Publish to MQTT broker
                _ = PublishTaskToMqttAsync(task);

                SuccessMessage = "Task has been successfully added.";
                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return RedirectToPage("/Index");
            }
        }


        // Twilio WhatsApp Notification Method
        private void SendWhatsAppNotificationToMultipleRecipients(TaskInfo task)
        {
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            var fromNumber = _configuration["Twilio:WhatsAppNumber"];
            var recipientNumbers = _configuration["Twilio:RecipientNumbers"].Split(',');

            TwilioClient.Init(accountSid, authToken);

            foreach (var toNumber in recipientNumbers)
            {
                var trimmedNumber = toNumber.Trim(); // Remove spaces if any

                var message = MessageResource.Create(
                    from: new PhoneNumber(fromNumber),
                    to: new PhoneNumber(trimmedNumber),
                    body: $"🚀 New Task Assigned!\n\nRef No: {task.Ref_No}\nTask Name: {task.Task_Type}\nAssembly Code: {task.Task_Name}\nDescription: {task.Task_Description}\nAssigned To: {task.Assign_To}\nQty: {task.Qty} {task.Unit}\nPriority: {task.Priority_Level}\nLink:http://tpltm-dev.eu-west-1.elasticbeanstalk.com/Assign_TaskList"
                );

                Console.WriteLine($"[INFO] WhatsApp Message Sent to {trimmedNumber}: {message.Sid}");
            }
        }


        private void SendSmsNotificationToMultipleRecipients(TaskInfo task)
        {
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            var fromNumber = _configuration["Twilio:SmsNumber"]; // Twilio SMS-enabled number
            var recipientNumbers = _configuration["Twilio:RecipientNumbers"].Split(',');

            TwilioClient.Init(accountSid, authToken);

            foreach (var toNumber in recipientNumbers)
            {
                var trimmedNumber = toNumber.Trim(); // Remove spaces if any

                var message = MessageResource.Create(
                    from: new PhoneNumber(fromNumber),
                    to: new PhoneNumber(trimmedNumber), // No "whatsapp:" prefix
                    body: $"🚀 New Task Assigned!\n\nRef No: {task.Ref_No}\nTask: {task.Task_Name}\nAssigned To: {task.Assign_To}\nQty: {task.Qty}\nPriority: {task.Priority_Level}\nLink:http://tpltm-dev.eu-west-1.elasticbeanstalk.com/Assign_TaskList"
                );

                Console.WriteLine($"[INFO] SMS Sent to {trimmedNumber}: {message.Sid}");
            }
        }

        private async Task PublishTaskToMqttAsync(TaskInfo task)
        {
            try
            {
                var mqttBroker = _configuration["MQTT:Broker"]?.Replace("tcp://", "") ?? "transcend-tdt.co.uk";
                var mqttPort = int.Parse(_configuration["MQTT:Port"] ?? "1883");
                var mqttTopic = _configuration["MQTT:Topic"] ?? "tdt/bc";

                // Create MQTT client
                var mqttClient = new MqttFactory().CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(mqttBroker, mqttPort)
                    .WithClientId("TPL_TM_TaskPublisher")
                    .Build();

                await mqttClient.ConnectAsync(options, CancellationToken.None);

                // Build JSON payload
                var payload = $@"{{
                    ""barcode"": ""{task.GTIN}""
                }}";

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(mqttTopic)
                    .WithPayload(Encoding.UTF8.GetBytes(payload))
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mqttClient.PublishAsync(message, CancellationToken.None);

                Console.WriteLine($"[INFO] Task published to MQTT topic '{mqttTopic}': {payload}");

                await mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] MQTT publish failed: {ex.Message}");
            }
        }

    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Npgsql;
using System.Data.Odbc;
using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Markdig.Extensions.TaskLists;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Clear_WasteModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public TaskInfo TaskInfo { get; set; } = new TaskInfo();
        private readonly NetSuiteClient _netSuiteClient;
        private readonly IConfiguration _configuration;

        public string DefaultConnection { get; private set; }
        public string ConnectionString { get; private set; }

        public List<TaskInfo> LocationList { get; set; } = new List<TaskInfo>();

        // Constructor with Dependency Injection
        public Clear_WasteModel(NetSuiteClient netSuiteClient, IConfiguration configuration)
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
                                       
                    // Clear AssignTolist to avoid duplicates
                    LocationList.Clear();

                    // Fetching Machine Name
                    string sql = @"
                                SELECT a.binnumber AS binname, 
                                       b.name AS wastelocation,
                                       c.name AS wastetype,
                                       d.name AS wastedescription,
                                       custrecord166 AS binweight
                                FROM bin a
                                INNER JOIN CUSTOMLIST1425 b ON a.custrecord163 = b.id
                                INNER JOIN CUSTOMLIST1426 c ON a.custrecord164 = c.id
                                INNER JOIN CUSTOMLIST1427 d ON a.custrecord165 = d.id
                                WHERE a.custrecord166 > 0";

                    using (OdbcCommand command = new OdbcCommand(sql, connection))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                LocationList.Add(new TaskInfo
                                {
                                    BinName = reader["binname"].ToString(),
                                    WasteLocation = reader["wastelocation"].ToString(),
                                    WasteType = reader["wastetype"].ToString(),
                                    WasteDescription = reader["wastedescription"].ToString(),
                                    BinWeight = Convert.ToDecimal(reader["binweight"])
                                });
                                Console.WriteLine($"[DEBUG] Retrieved location: {reader["wastelocation"]}");
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
                    Ref_No = Request.Form["BinName"],
                    Task_Type = "Clear Waste",
                    Task_Name = Request.Form["Waste_Type"],
                    Task_Description = Request.Form["Waste_Description"],
                    Assign_To = Request.Form["BinName"],
                    Qty = Convert.ToInt32(Request.Form["Qty"]),
                    Unit = Request.Form["Unit"],
                    Priority_Level = Request.Form["Priority_Level"],
                    Comments = Request.Form["Comments"]
                };

                // Connection string for Postgres database
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                // Database insert query for task
                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();

                    string sqlString = "select COUNT(*) as task_count from task_manager Where Task_Type = 'Clear Waste'";
                    using (NpgsqlCommand sqlCmd = new NpgsqlCommand(sqlString, connection))
                    {
                        using (NpgsqlDataReader reader = sqlCmd.ExecuteReader())
                        {
                            int currentQty = 0; // Default to 0 if no data is retrieved

                            // Check if the reader has rows and read data
                            if (reader.Read() && !reader.IsDBNull(0))
                            {
                                currentQty = reader.GetInt32(reader.GetOrdinal("task_count"));
                                task.Ref_No = "CW" + (currentQty + 1).ToString("00000#");
                            }
                            else 
                            {
                                task.Ref_No = "CW" + (1).ToString("00000#");
                            }
                            
                        }
                    }

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
                        command.Parameters.AddWithValue("@Created_by", User.Identity?.Name);
                        command.Parameters.AddWithValue("@Comments", task.Comments);
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                }
                SendWhatsAppNotificationToMultipleRecipients(task);
                SuccessMessage = "Task has been successfully added.";
                return RedirectToPage("/Index"); // Redirect after successful submission
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return RedirectToPage("/Index"); // Redirect after failure or exception
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
                    body: $"🚀 New Task Assigned!\n\nRef No: {task.Ref_No}\nTask Name: {task.Task_Type}\nWaste Type: {task.Task_Name}\nDescription: {task.Task_Description}\nClear From: {task.Assign_To}\nQty: {task.Qty} {task.Unit}\nPriority: {task.Priority_Level}\nLink: http://tpltm-dev.eu-west-1.elasticbeanstalk.com/Clear_WasteList"
                );

                Console.WriteLine($"[INFO] WhatsApp Message Sent to {trimmedNumber}: {message.Sid}");
            }
        }
    }
}

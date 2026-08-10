using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Npgsql;
using NuGet.Protocol.Plugins;
using System.Data.Odbc;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML.Voice;
using Twilio.Types;


namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management, User")]
    public class Request_InventoryModel : PageModel
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
        public Request_InventoryModel(NetSuiteClient netSuiteClient, IConfiguration configuration)
        {
            _netSuiteClient = netSuiteClient;
            _configuration = configuration;
        }

        // OnGet method to fetch data from NetSuite ODBC
        public void OnGet(string? wo, string? item, string? desc, string? machine, int? qty)
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


                    string lastMonthDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd");

                    string sql = $@"
                                    SELECT DISTINCT 
                                        a.tranid, 
                                        c.itemid, 
                                        c.displayname
                                    FROM 
                                        transaction a
                                    JOIN 
                                        transactionline b ON a.id = b.transaction
                                    JOIN 
                                        item c ON b.item = c.id
                                    WHERE 
                                        RTRIM(a.recordtype) = 'workorder'
                                        AND b.mainline = 'F'
                                        AND b.itemtype != 'OthCharge'
                                        AND b.item IS NOT NULL
                                        AND a.createddate >= {{d '{lastMonthDate}'}}
                                    ORDER BY 
                                        a.tranid DESC";

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
                                    Task_Description = !reader.IsDBNull(reader.GetOrdinal("displayname")) ? reader["displayname"].ToString() : "No Description"
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
                if (!string.IsNullOrWhiteSpace(wo))
                {
                    TaskInfo.Ref_No = wo;
                }

                if (!string.IsNullOrWhiteSpace(item))
                {
                    TaskInfo.Task_Name = item;
                }

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    TaskInfo.Task_Description = desc;
                }

                if (!string.IsNullOrWhiteSpace(machine))
                {
                    TaskInfo.Assign_To = machine;
                }
                if (qty.HasValue)
                {
                    TaskInfo.Qty = qty.Value;
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
                // Assign values from form
                TaskInfo task = new TaskInfo
                {
                    Ref_No = Request.Form["TaskInfo.Ref_No"],
                    Task_Type = "Request Inventory",
                    Task_Name = Request.Form["TaskInfo.Task_Name"],
                    Task_Description = Request.Form["TaskInfo.Task_Description"],
                    Assign_To = Request.Form["TaskInfo.Assign_To"],
                    Qty = Convert.ToInt32(Request.Form["TaskInfo.Qty"]),
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
                    body: $"🚀 New Task Assigned!\n\nRef No: {task.Ref_No}\nTask Name: {task.Task_Type}\nInventory Code: {task.Task_Name}\nDescription: {task.Task_Description}\nRequested For: {task.Assign_To}\nQty: {task.Qty} {task.Unit}\nPriority: {task.Priority_Level}\nMove: {task.Assign_To}\nQty: {task.Qty} {task.Unit}\nPriority: {task.Priority_Level}\nLink: http://tpltm-dev.eu-west-1.elasticbeanstalk.com/Request_InventoryList"
                );

                Console.WriteLine($"[INFO] WhatsApp Message Sent to {trimmedNumber}: {message.Sid}");
            }
        }
    }
}

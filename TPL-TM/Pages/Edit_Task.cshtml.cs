using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Npgsql;
using NuGet.Packaging.Signing;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Edit_TaskModel : PageModel
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
        public Edit_TaskModel(NetSuiteClient netSuiteClient, IConfiguration configuration)
        {
            _netSuiteClient = netSuiteClient;
            _configuration = configuration;
        }

        public List<TaskInfo> listtask { get; set; } = new List<TaskInfo>();
        public int item_id;
        public int req_id;

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
                               MIN(c.displayname) AS displayname
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
                    sql = "SELECT DISTINCT groupname FROM entitygroup WHERE ismanufacturingworkcenter = 'T'";
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

            string id = Request.Query["id"];
            if (!String.IsNullOrEmpty(id))
            {
                item_id = Int32.Parse(id);
            }
            else
            {
                item_id = 1;
            }

            try
            {
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();
                    string sql = "SELECT * FROM task_manager WHERE id = @id;";
                    using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@id", item_id);
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                TaskInfo.Id = reader.GetInt32(reader.GetOrdinal("id"));
                                TaskInfo.Ref_No = reader.GetString(reader.GetOrdinal("Ref_No"));
                                TaskInfo.Task_Type = reader.GetString(reader.GetOrdinal("Task_Type"));
                                TaskInfo.Task_Name = reader.GetString(reader.GetOrdinal("Task_Name"));
                                TaskInfo.Task_Description = reader.GetString(reader.GetOrdinal("Task_Description"));
                                TaskInfo.Assign_To = reader.GetString(reader.GetOrdinal("Assign_To"));
                                TaskInfo.Qty = reader.GetInt32(reader.GetOrdinal("Qty"));
                                TaskInfo.Unit = reader.GetString(reader.GetOrdinal("Unit"));
                                TaskInfo.Status = reader.GetString(reader.GetOrdinal("Status"));
                                TaskInfo.Priority_Level = reader.GetString(reader.GetOrdinal("Priority_Level"));
                                TaskInfo.Comments = reader.GetString(reader.GetOrdinal("Comments"));
                                // Modify Create_On to show both date and time
                                TaskInfo.Create_On = reader.IsDBNull(reader.GetOrdinal("Create_On"))
                                    ? ""
                                    : reader.GetDateTime(reader.GetOrdinal("Create_On")).ToString("dd/MM/yyyy HH:mm:ss");

                                TaskInfo.Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by"))
                                    ? "N/A"
                                    : reader.GetString(reader.GetOrdinal("Created_by"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
        }


        // OnPost method to handle form submission and database insertion
        public IActionResult OnPost()
        {
            try
            {
                // Assign values from form
                TaskInfo.Ref_No = Request.Form["Work_Order"];
                TaskInfo.Task_Type = Request.Form["Task_Type"];
                TaskInfo.Task_Name = Request.Form["Task_Name"];
                TaskInfo.Task_Description = Request.Form["Task_Description"];
                TaskInfo.Assign_To = Request.Form["Assign_To"];
                TaskInfo.Qty = Convert.ToInt32(Request.Form["Qty"]);
                TaskInfo.Unit = Request.Form["Unit"];
                TaskInfo.Priority_Level = Request.Form["Priority_Level"];
                TaskInfo.Comments = Request.Form["Comments"];

                // Connection string for Postgres database
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();

                    // Check if the task exists
                    string checkQuery = "SELECT COUNT(1) FROM task_manager WHERE Ref_No = @Ref_No";
                    using (NpgsqlCommand checkCommand = new NpgsqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Ref_No", TaskInfo.Ref_No);
                        int taskExists = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (taskExists > 0)
                        {
                            // Task exists, update the task
                            string updateQuery = @"
                        UPDATE task_manager
                        SET Task_Type = @Task_Type,
                            Task_Name = @Task_Name,
                            Task_Description = @Task_Description,
                            Assign_To = @Assign_To,
                            Qty = @Qty,
                            Unit = @Unit,
                            Priority_Level = @Priority_Level,
                            Comments = @Comments,
                            Status = @Status,
                            Created_by = @Created_by
                        WHERE Ref_No = @Ref_No";

                            using (NpgsqlCommand command = new NpgsqlCommand(updateQuery, connection))
                            {
                                command.Parameters.AddWithValue("@Task_Type", TaskInfo.Task_Type);
                                command.Parameters.AddWithValue("@Task_Name", TaskInfo.Task_Name);
                                command.Parameters.AddWithValue("@Task_Description", TaskInfo.Task_Description);
                                command.Parameters.AddWithValue("@Assign_To", TaskInfo.Assign_To);
                                command.Parameters.AddWithValue("@Qty", TaskInfo.Qty);
                                command.Parameters.AddWithValue("@Unit", TaskInfo.Unit);
                                command.Parameters.AddWithValue("@Priority_Level", TaskInfo.Priority_Level);
                                command.Parameters.AddWithValue("@Comments", TaskInfo.Comments);
                                command.Parameters.AddWithValue("@Status", "A"); // Keep status same for now
                                command.Parameters.AddWithValue("@Created_by", User.Identity?.Name ?? "System");
                                command.Parameters.AddWithValue("@Ref_No", TaskInfo.Ref_No);

                                command.ExecuteNonQuery();
                            }

                            SuccessMessage = "Task has been successfully updated.";
                        }
                        else
                        {
                            // Task not found, return an error
                            ErrorMessage = "Task not found. Unable to update.";
                            return Page(); // Return to the same page if error
                        }
                    }

                    connection.Close();
                }

                return RedirectToPage("/Index"); // Redirect after success
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
                return Page(); // Stay on the page and show error
            }
        }

    }
}

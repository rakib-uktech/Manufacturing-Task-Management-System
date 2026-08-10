using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Npgsql;
using System.Data.Odbc;

using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Mono.TextTemplating;

using System.Data;
using System.Xml.Linq;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;


namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Schedule_TaskModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public TaskInfo TaskInfo { get; set; } = new TaskInfo();
        private readonly NetSuiteClient _netSuiteClient;
        private readonly IConfiguration _configuration;

        public List<TaskInfo> TaskList { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> AssignTolist { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> ScheduledTasks { get; set; } = new List<TaskInfo>();

        public Schedule_TaskModel(NetSuiteClient netSuiteClient, IConfiguration configuration)
        {
            _netSuiteClient = netSuiteClient;
            _configuration = configuration;
        }

        public void OnGet()
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("NetSuiteOdbc");

                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    connection.Open();

                    TaskList.Clear();
                    AssignTolist.Clear();
                    ScheduledTasks.Clear();

                    // Load TaskList
                    string sql = @"
                    SELECT DISTINCT itemid, displayname
                    FROM item
                    WHERE itemtype='Assembly' AND isinactive = 'F'
                    ORDER BY itemid";
                    using (OdbcCommand command = new OdbcCommand(sql, connection))
                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TaskList.Add(new TaskInfo
                            {
                                Task_Name = reader["itemid"].ToString(),
                                Task_Description = reader["displayname"].ToString()
                            });
                        }
                    }

                    // Load AssignToList
                    sql = "SELECT DISTINCT groupname FROM entitygroup WHERE ismanufacturingworkcenter = 'T' ORDER BY groupname";
                    using (OdbcCommand command = new OdbcCommand(sql, connection))
                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            AssignTolist.Add(new TaskInfo { Assign_To = reader["groupname"].ToString() });
                        }
                    }
                }

                // Load ScheduledTasks from PostgreSQL (assuming scheduled tasks are in task_schedule)
                string pgConnString = _configuration.GetConnectionString("DefaultConnection");
                using (NpgsqlConnection pgConn = new NpgsqlConnection(pgConnString))
                {
                    pgConn.Open();
                    string query = "SELECT * FROM task_schedule WHERE Status = 'Scheduled'";
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, pgConn))
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScheduledTasks.Add(new TaskInfo
                            {
                                Task_Date = reader["Task_Date"] as DateTime?,
                                Task_EndDate = reader["Task_EndDate"] as DateTime?,
                                Task_StartTime = reader["Task_StartTime"] as TimeSpan?,
                                Task_EndTime = reader["Task_EndTime"] as TimeSpan?,
                                Assign_To = reader["Assign_To"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                TaskInfo task = new TaskInfo
                {
                    Task_Type = "Scheduled Task",
                    Task_Name = Request.Form["Task_Name"],
                    Task_Description = Request.Form["Task_Description"],
                    Assign_To = Request.Form["Assign_To"],
                    Qty = Convert.ToInt32(Request.Form["Qty"]),
                    Unit = Request.Form["Unit"],
                    Priority_Level = Request.Form["Priority_Level"],
                    Comments = Request.Form["Comments"],
                    Task_Date = DateTime.Parse(Request.Form["Task_Date"]),
                    Task_EndDate = DateTime.Parse(Request.Form["Task_EndDate"]),
                    Task_StartTime = TimeSpan.Parse(Request.Form["Task_StartTime"]),
                    Task_EndTime = TimeSpan.Parse(Request.Form["Task_EndTime"])
                };

                string connString = _configuration.GetConnectionString("DefaultConnection");

                using (NpgsqlConnection conn = new NpgsqlConnection(connString))
                {
                    conn.Open();

                    // Check for overlapping tasks
                    DateTime startDateTime = task.Task_Date.Value + task.Task_StartTime.Value;
                    DateTime endDateTime = task.Task_EndDate.Value + task.Task_EndTime.Value;

                    string checkOverlapQuery = @"
                        SELECT 1
                        FROM task_schedule
                        WHERE Assign_To = @Assign_To
                          AND (
                            @StartDateTime < (Task_EndDate + Task_EndTime)
                            AND
                            @EndDateTime > (Task_Date + Task_StartTime)
                        )
                        LIMIT 1;
                    ";

                    using (var checkCmd = new NpgsqlCommand(checkOverlapQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Assign_To", task.Assign_To);
                        checkCmd.Parameters.AddWithValue("@StartDateTime", startDateTime);
                        checkCmd.Parameters.AddWithValue("@EndDateTime", endDateTime);

                        var exists = checkCmd.ExecuteScalar();
                        if (exists != null)
                        {
                            ErrorMessage = "A task is already scheduled in the selected time slot for this assignee.";
                            return Page();
                        }
                    }


                    // No overlap – insert new task
                    string insertQuery = @"
                        INSERT INTO task_schedule
                        (Task_Type, Task_Name, Task_Description, Assign_To, Qty, Unit, Status, Priority_Level, Created_by, Comments, Task_Date, Task_EndTime, Task_StartTime, Task_EndDate)
                        VALUES
                        (@Task_Type, @Task_Name, @Task_Description, @Assign_To, @Qty, @Unit, @Status, @Priority_Level, @Created_by, @Comments, @Task_Date, @Task_EndTime, @Task_StartTime, @Task_EndDate);
                    ";

                    using (var insertCmd = new NpgsqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Task_Type", task.Task_Type);
                        insertCmd.Parameters.AddWithValue("@Task_Name", task.Task_Name);
                        insertCmd.Parameters.AddWithValue("@Task_Description", task.Task_Description);
                        insertCmd.Parameters.AddWithValue("@Assign_To", task.Assign_To);
                        insertCmd.Parameters.AddWithValue("@Qty", task.Qty);
                        insertCmd.Parameters.AddWithValue("@Unit", task.Unit);
                        insertCmd.Parameters.AddWithValue("@Status", "Scheduled");
                        insertCmd.Parameters.AddWithValue("@Priority_Level", task.Priority_Level);
                        insertCmd.Parameters.AddWithValue("@Created_by", User.Identity?.Name ?? "System");
                        insertCmd.Parameters.AddWithValue("@Comments", task.Comments);
                        insertCmd.Parameters.AddWithValue("@Task_Date", task.Task_Date);
                        insertCmd.Parameters.AddWithValue("@Task_EndDate", task.Task_EndDate);
                        insertCmd.Parameters.AddWithValue("@Task_StartTime", task.Task_StartTime);
                        insertCmd.Parameters.AddWithValue("@Task_EndTime", task.Task_EndTime);

                        insertCmd.ExecuteNonQuery();
                    }
                }

                SuccessMessage = "Task has been scheduled successfully.";
                return RedirectToPage("ScheduleCalendar");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }


    }

}

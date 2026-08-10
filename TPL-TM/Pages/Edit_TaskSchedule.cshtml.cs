using Markdig.Extensions.TaskLists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor")]
    public class Edit_TaskScheduleModel : PageModel
    {
        private readonly IConfiguration _configuration;

        [BindProperty]
        public TaskInfo TaskInfo { get; set; }

        public Edit_TaskScheduleModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public List<TaskInfo> TaskList { get; set; } = new List<TaskInfo>();
        public IActionResult OnGet(int id)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");

                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();

                    string query = @"SELECT * FROM task_schedule WHERE id = @id";
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                TaskInfo = new TaskInfo
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    Task_Name = reader["task_name"].ToString(),
                                    Task_Description = reader["task_description"].ToString(),
                                    Assign_To = reader["assign_to"].ToString(),
                                    Qty = Convert.ToInt32(reader["qty"]),
                                    Unit = reader["unit"].ToString(),
                                    Task_Date = Convert.ToDateTime(reader["task_date"]),
                                    Task_StartTime = (TimeSpan)reader["task_starttime"],
                                    Task_EndTime = (TimeSpan)reader["task_endtime"],
                                    Priority_Level = reader["priority_level"].ToString(),
                                    Comments = reader["comments"].ToString()
                                };
                            }
                            else
                            {
                                return RedirectToPage("/Index");
                            }
                        }
                    }
                }

                // Populate TaskList for task name & description datalist
                string connectionString = _configuration.GetConnectionString("NetSuiteOdbc");

                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    connection.Open();
                    string sql = @"
                    SELECT DISTINCT itemid, displayname
                    FROM item
                    WHERE isinactive = 'F'
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
                }
                return Page();
            }
            catch (Exception)
            {
                return RedirectToPage("/Index");
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");

                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();

                    string updateQuery = @"
                        UPDATE task_schedule
                        SET 
                            task_name = @Task_Name,
                            task_description = @Task_Description,
                            assign_to = @Assign_To,
                            qty = @Qty,
                            unit = @Unit,
                            task_date = @Task_Date,
                            task_starttime = @Task_StartTime,
                            task_endtime = @Task_EndTime,
                            priority_level = @Priority_Level,
                            comments = @Comments
                        WHERE id = @Id";

                    using (var cmd = new NpgsqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", TaskInfo.Id);
                        cmd.Parameters.AddWithValue("@Task_Name", TaskInfo.Task_Name);
                        cmd.Parameters.AddWithValue("@Task_Description", TaskInfo.Task_Description);
                        cmd.Parameters.AddWithValue("@Assign_To", TaskInfo.Assign_To);
                        cmd.Parameters.AddWithValue("@Qty", TaskInfo.Qty);
                        cmd.Parameters.AddWithValue("@Unit", TaskInfo.Unit);
                        cmd.Parameters.AddWithValue("@Task_Date", TaskInfo.Task_Date);
                        cmd.Parameters.AddWithValue("@Task_StartTime", TaskInfo.Task_StartTime);
                        cmd.Parameters.AddWithValue("@Task_EndTime", TaskInfo.Task_EndTime);
                        cmd.Parameters.AddWithValue("@Priority_Level", TaskInfo.Priority_Level);
                        cmd.Parameters.AddWithValue("@Comments", TaskInfo.Comments ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }

                return RedirectToPage("/Index");
            }
            catch (Exception)
            {
                return Page();
            }
        }
    }
}

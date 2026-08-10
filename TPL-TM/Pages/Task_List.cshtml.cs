using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Newtonsoft.Json;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Task_ListModel : PageModel
    {
        public string errorMessage = "";
        public string successMessage = "";
        public TaskInfo taskinfo = new TaskInfo();
        private readonly IConfiguration _configuration;

        public Task_ListModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<TaskInfo> listtask { get; set; } = new List<TaskInfo>(); // Initialize list

        public void OnGet()
        {
            try
            {
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();
                    string sql = "SELECT * FROM task_manager";

                    using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            listtask = new List<TaskInfo>(); // Initialize list

                            while (reader.Read())
                            {
                                TaskInfo taskinfo = new TaskInfo
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                                    Ref_No = reader.GetString(reader.GetOrdinal("Ref_No")),
                                    Task_Type = reader.GetString(reader.GetOrdinal("Task_Type")),
                                    Task_Name = reader.GetString(reader.GetOrdinal("Task_Name")),
                                    Task_Description = reader.GetString(reader.GetOrdinal("Task_Description")),
                                    Assign_To = reader.GetString(reader.GetOrdinal("Assign_To")),
                                    Qty = reader.GetInt32(reader.GetOrdinal("Qty")),
                                    Unit = reader.GetString(reader.GetOrdinal("Unit")),
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    Priority_Level = reader.GetString(reader.GetOrdinal("Priority_Level")),
                                    Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by")) ? "N/A" : reader.GetString(reader.GetOrdinal("Created_by")),
                                    Completed_by = reader.IsDBNull(reader.GetOrdinal("Completed_by")) ? "N/A" : reader.GetString(reader.GetOrdinal("Completed_by")),
                                    Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? "" : reader.GetString(reader.GetOrdinal("Comments"))
                                };

                                // Handle nullable timestamp fields
                                taskinfo.Created_On = reader.IsDBNull(reader.GetOrdinal("Create_On")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Create_On"));
                                taskinfo.Completed_On = reader.IsDBNull(reader.GetOrdinal("Complete_On")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Complete_On"));

                                // Convert nullable DateTime to formatted string for display (include seconds)
                                taskinfo.Create_On = taskinfo.Created_On?.ToString("dd/MM/yyyy HH:mm:ss") ?? "";
                                taskinfo.Complete_On = taskinfo.Completed_On?.ToString("dd/MM/yyyy HH:mm:ss") ?? "";

                                // Calculate task duration in hours, minutes, and seconds
                                if (taskinfo.Created_On.HasValue && taskinfo.Completed_On.HasValue)
                                {
                                    TimeSpan duration = taskinfo.Completed_On.Value - taskinfo.Created_On.Value;
                                    int hours = (int)duration.TotalHours;
                                    int minutes = duration.Minutes;
                                    int seconds = duration.Seconds;
                                    taskinfo.Duration = $"{hours} hrs {minutes} mins {seconds} secs";
                                }
                                else
                                {
                                    taskinfo.Duration = "-";
                                }

                                listtask.Add(taskinfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
        }

    }

}

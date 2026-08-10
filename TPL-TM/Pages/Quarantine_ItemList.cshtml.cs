using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    public class Quarantine_ItemListModel : PageModel
    {
        public String errorMessage = "";
        public String successMessage = "";
        public TaskInfo taskinfo = new TaskInfo();
        private readonly IConfiguration _configuration;
        public string DefaultConnection { get; private set; }
        public string _connectionString { get; private set; }
        public Quarantine_ItemListModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<TaskInfo> listtask { get; set; } = new List<TaskInfo>(); // Initialize listtask

        public void OnGet()
        {
            try
            {
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();

                    string sql = "SELECT * from task_manager Where task_type='Quarantine Item' and status='A'";

                    using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            listtask = new List<TaskInfo>(); // Initialize list

                            while (reader.Read())
                            {
                                TaskInfo taskinfo = new TaskInfo();
                                taskinfo.Id = reader.GetInt32(reader.GetOrdinal("id"));
                                taskinfo.Create_On = reader.IsDBNull(reader.GetOrdinal("Create_On"))
                                    ? ""
                                    : reader.GetDateTime(reader.GetOrdinal("Create_On")).ToString("dd/MM/yyyy");

                                taskinfo.Ref_No = reader.GetString(reader.GetOrdinal("Ref_No"));
                                taskinfo.Task_Type = reader.GetString(reader.GetOrdinal("Task_Type"));
                                taskinfo.Task_Name = reader.GetString(reader.GetOrdinal("Task_Name"));
                                taskinfo.Task_Description = reader.GetString(reader.GetOrdinal("Task_Description"));
                                taskinfo.Assign_To = reader.GetString(reader.GetOrdinal("Assign_To"));
                                taskinfo.Qty = reader.GetInt32(reader.GetOrdinal("Qty"));
                                taskinfo.Unit = reader.GetString(reader.GetOrdinal("Unit"));
                                taskinfo.Status = reader.GetString(reader.GetOrdinal("Status"));
                                taskinfo.Priority_Level = reader.GetString(reader.GetOrdinal("Priority_Level"));

                                // Handle nullable Complete_On
                                taskinfo.Complete_On = reader.IsDBNull(reader.GetOrdinal("Complete_On"))
                                    ? ""
                                    : reader.GetDateTime(reader.GetOrdinal("Complete_On")).ToString("dd/MM/yyyy");

                                taskinfo.Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by"))
                                    ? "N/A"
                                    : reader.GetString(reader.GetOrdinal("Created_by"));

                                taskinfo.Completed_by = reader.IsDBNull(reader.GetOrdinal("Completed_by"))
                                    ? "N/A"
                                    : reader.GetString(reader.GetOrdinal("Completed_by"));

                                taskinfo.Comments = reader.IsDBNull(reader.GetOrdinal("Comments"))
                                    ? ""
                                    : reader.GetString(reader.GetOrdinal("Comments"));

                                listtask.Add(taskinfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return;
            }
        }

    }
}

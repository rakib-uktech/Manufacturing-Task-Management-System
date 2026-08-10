using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class Delete_TaskModel : PageModel
    {
        public string errorMessage = "";
        public string successMessage = "";
        public TaskInfo taskinfo = new TaskInfo();

        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;

        public Delete_TaskModel(IConfiguration configuration, UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public int item_id;

        // GET: Load task details before confirming delete
        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out int taskId))
            {
                item_id = taskId;
            }
            else
            {
                errorMessage = "Invalid Task ID!";
                return;
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
                                taskinfo.Id = reader.GetInt32(reader.GetOrdinal("id"));
                                taskinfo.Ref_No = reader.GetString(reader.GetOrdinal("Ref_No"));
                                taskinfo.Task_Type = reader.GetString(reader.GetOrdinal("Task_Type"));
                                taskinfo.Task_Name = reader.GetString(reader.GetOrdinal("Task_Name"));
                                taskinfo.Task_Description = reader.GetString(reader.GetOrdinal("Task_Description"));
                                taskinfo.Assign_To = reader.GetString(reader.GetOrdinal("Assign_To"));
                                taskinfo.Qty = reader.GetInt32(reader.GetOrdinal("Qty"));
                                taskinfo.Unit = reader.GetString(reader.GetOrdinal("Unit"));
                                taskinfo.Status = reader.GetString(reader.GetOrdinal("Status"));
                                taskinfo.Priority_Level = reader.GetString(reader.GetOrdinal("Priority_Level"));
                                taskinfo.Comments = reader.GetString(reader.GetOrdinal("Comments"));

                                // Format Create_On date and time
                                taskinfo.Create_On = reader.IsDBNull(reader.GetOrdinal("Create_On"))
                                    ? ""
                                    : reader.GetDateTime(reader.GetOrdinal("Create_On")).ToString("dd/MM/yyyy HH:mm:ss");

                                taskinfo.Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by"))
                                    ? "N/A"
                                    : reader.GetString(reader.GetOrdinal("Created_by"));
                            }
                            else
                            {
                                errorMessage = "Task not found.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error fetching task: " + ex.Message;
            }
        }

        // POST: Handle Task Deletion
        public async Task<IActionResult> OnPostDeleteTaskAsync(int TaskId)
        {
            Console.WriteLine($"TaskId Received: {TaskId}");

            if (TaskId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Task ID!";
                return RedirectToPage("/Index");
            }

            try
            {
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();

                    string sql = @"
                                DELETE FROM task_manager
                                WHERE id = @id;";

                    using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@id", TaskId);
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Task deleted successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Task not found. Deletion failed.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error while deleting task: {ex.Message}";
            }

            return RedirectToPage("/Index");
        }

    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages.Api
{
    public class TaskCountsModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public TaskCountsModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult OnGet()
        {
            var counts = new
            {
                assignTask = 0,
                requestInventory = 0,
                moveItem = 0,
                clearWaste = 0,
                quarantineItem = 0
            };

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT 
                            (SELECT COUNT(*) FROM task_manager WHERE task_type = 'Assembly Build' AND status = 'A') AS assignTask,
                            (SELECT COUNT(*) FROM task_manager WHERE task_type = 'Request Inventory' AND status = 'A') AS requestInventory,
                            (SELECT COUNT(*) FROM task_manager WHERE task_type = 'Move Item' AND status = 'A') AS moveItem,
                            (SELECT COUNT(*) FROM task_manager WHERE task_type = 'Clear Waste' AND status = 'A') AS clearWaste,
                            (SELECT COUNT(*) FROM task_manager WHERE task_type = 'Quarantine Item' AND status = 'A') AS quarantineItem;
                    ";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        counts = new
                        {
                            assignTask = reader.GetInt32(reader.GetOrdinal("assignTask")),
                            requestInventory = reader.GetInt32(reader.GetOrdinal("requestInventory")),
                            moveItem = reader.GetInt32(reader.GetOrdinal("moveItem")),
                            clearWaste = reader.GetInt32(reader.GetOrdinal("clearWaste")),
                            quarantineItem = reader.GetInt32(reader.GetOrdinal("quarantineItem"))
                        };
                    }
                }

                return new JsonResult(counts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Downtime_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Downtime_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public string successMessage = "";

        public DowntimeItem downtime = new DowntimeItem();

        public long item_id;

        public class DowntimeItem
        {
            public long Id { get; set; }
            public int? Downtime { get; set; }
            public long? ReasonId { get; set; }
            public long? Shift { get; set; }
            public string Comment { get; set; }
            public string ReasonName { get; set; }
        }

        // Load record
        public void OnGet()
        {
            string id = Request.Query["id"];

            if (!long.TryParse(id, out long dtId))
            {
                errorMessage = "Invalid downtime ID!";
                return;
            }

            item_id = dtId;

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT d.id, d.downtime, d.reason_id, d.shift, d.comment,
                           r.reason_name
                    FROM downtime d
                    LEFT JOIN downtime_reason r ON d.reason_id = r.id
                    WHERE d.id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", item_id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    downtime = new DowntimeItem
                    {
                        Id = reader.GetInt64(0),
                        Downtime = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        ReasonId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                        Shift = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                        Comment = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        ReasonName = reader.IsDBNull(5) ? "" : reader.GetString(5)
                    };
                }
                else
                {
                    errorMessage = "Downtime record not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error fetching downtime record: " + ex.Message;
            }
        }

        // POST — delete downtime
        public IActionResult OnPostDeleteDowntime(long Id)
        {
            if (Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid downtime ID!";
                return RedirectToPage("/Downtime_Report");
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM downtime WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    TempData["SuccessMessage"] = "Downtime record deleted successfully!";
                else
                    TempData["ErrorMessage"] = "Downtime not found. Deletion failed.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting downtime: " + ex.Message;
            }
            if (User.IsInRole("User"))
            {
                return RedirectToPage("/Operator_Index");
            }
            return RedirectToPage("/Downtime_Report");            
        }
    }
}

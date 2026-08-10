using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class DowntimeReasonDeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public DowntimeReasonDeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public DowntimeReasonItem Reason = new DowntimeReasonItem();

        public class DowntimeReasonItem
        {
            public long Id { get; set; }
            public string ReasonName { get; set; }
            public string Description { get; set; }
            public bool Active { get; set; }
            public DateTime CreatedOn { get; set; }
            public string CreatedBy { get; set; }
        }

        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!long.TryParse(id, out long reasonId))
            {
                errorMessage = "Invalid downtime reason ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, reason_name, description, active, created_on, created_by
                    FROM downtime_reason
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", reasonId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Reason.Id = reader.GetInt64(0);
                    Reason.ReasonName = reader.GetString(1);
                    Reason.Description = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    Reason.Active = reader.IsDBNull(3) ? false : reader.GetBoolean(3);
                    Reason.CreatedOn = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4);
                    Reason.CreatedBy = reader.IsDBNull(5) ? "" : reader.GetString(5);
                }
                else
                {
                    errorMessage = "Downtime reason not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error loading downtime reason: " + ex.Message;
            }
        }

        public IActionResult OnPostDeleteReason(long Id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM downtime_reason WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    TempData["SuccessMessage"] = "Downtime reason deleted successfully!";
                else
                    TempData["ErrorMessage"] = "Downtime reason not found.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting downtime reason: " + ex.Message;
            }

            return RedirectToPage("/DowntimeReasonList");
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Downtime_EditModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Downtime_EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public DowntimeItem Downtime { get; set; } = new DowntimeItem();

        public List<DowntimeReason> ReasonList { get; set; } = new();

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public class DowntimeItem
        {
            public long Id { get; set; }
            public int? Downtime { get; set; }
            public long? ReasonId { get; set; }
            public long? Shift { get; set; }
            public string Comment { get; set; }
        }

        public class DowntimeReason
        {
            public long Id { get; set; }
            public string ReasonName { get; set; }
        }

        public void OnGet(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                // Load current downtime entry
                string sql = "SELECT id, downtime, reason_id, shift, comment FROM downtime WHERE id=@id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Downtime = new DowntimeItem
                    {
                        Id = reader.GetInt64(0),
                        Downtime = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        ReasonId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                        Shift = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                        Comment = reader.IsDBNull(4) ? "" : reader.GetString(4)
                    };
                }

                reader.Close();

                // Load reasons
                sql = "SELECT id, reason_name FROM downtime_reason WHERE active = TRUE ORDER BY reason_name";
                using var reasonCmd = new NpgsqlCommand(sql, conn);
                using var reasonReader = reasonCmd.ExecuteReader();

                while (reasonReader.Read())
                {
                    ReasonList.Add(new DowntimeReason
                    {
                        Id = reasonReader.GetInt64(0),
                        ReasonName = reasonReader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading downtime record: {ex.Message}";
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    UPDATE downtime
                    SET downtime=@d, reason_id=@r, shift=@s, comment=@c
                    WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@d", (object?)Downtime.Downtime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@r", (object?)Downtime.ReasonId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@s", (object?)Downtime.Shift ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@c", (object?)Downtime.Comment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", Downtime.Id);

                cmd.ExecuteNonQuery();

                if (User.IsInRole("User"))
                {
                    return RedirectToPage("/Operator_Index");
                }
                return RedirectToPage("/Downtime_Report");                
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating downtime record: {ex.Message}";
                return Page();
            }
        }
    }
}

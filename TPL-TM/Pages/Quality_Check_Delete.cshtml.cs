using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class Quality_Check_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Quality_Check_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

    public string errorMessage = "";
        public string successMessage = "";

        public QualityCheckItem qualityCheck = new QualityCheckItem();
        public long item_id;

        public class QualityCheckItem
        {
            public long Id { get; set; }
            public long Shift_Id { get; set; }
            public string TestName { get; set; }
            public string Status { get; set; }
            public bool Fail { get; set; }
            public string Weights { get; set; }
            public string Comment { get; set; }
            public TimeSpan? Check_Time { get; set; }
        }

        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!long.TryParse(id, out long qcId))
            {
                errorMessage = "Invalid quality check ID!";
                return;
            }

            item_id = qcId;

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                SELECT qc.id, qc.shift_id, qc.status, qc.fail, qc.weights, qc.comment, qc.check_time, t.test_name
                FROM quality_check qc
                LEFT JOIN quality_checks_template t ON qc.test_id = t.id
                WHERE qc.id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", item_id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    qualityCheck = new QualityCheckItem
                    {
                        Id = reader.GetInt64(0),
                        Shift_Id = reader.GetInt64(1),
                        Status = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Fail = reader.IsDBNull(3) ? false : reader.GetBoolean(3),
                        Weights = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Comment = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Check_Time = reader.IsDBNull(6) ? null : reader.GetTimeSpan(6),
                        TestName = reader.IsDBNull(7) ? "" : reader.GetString(7)
                    };
                }
                else
                {
                    errorMessage = "Quality check record not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error fetching quality check record: " + ex.Message;
            }
        }

        public IActionResult OnPostDeleteQualityCheck(long Id)
        {
            if (Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid quality check ID!";
                return RedirectToPage("/Quality_Check_Report");
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM quality_check WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    TempData["SuccessMessage"] = "Quality check record deleted successfully!";
                else
                    TempData["ErrorMessage"] = "Quality check not found. Deletion failed.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting quality check: " + ex.Message;
            }

            return RedirectToPage("/Quality_Check_Report");
        }
    }

}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Quality_Check_EditModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Quality_Check_EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

    [BindProperty]
        public QualityCheckItem QualityCheck { get; set; } = new();

        public List<TestTemplate> TestList { get; set; } = new();

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public class QualityCheckItem
        {
            public long Id { get; set; }
            public long Shift_Id { get; set; }
            public long TestId { get; set; }
            public string Status { get; set; }
            public bool Fail { get; set; }
            public string Weights { get; set; }
            public string Comment { get; set; }
            public TimeSpan? Check_Time { get; set; }
        }

        public class TestTemplate
        {
            public long Id { get; set; }
            public string TestName { get; set; }
        }

        public void OnGet(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                // Load quality check
                string sql = @"
                SELECT id, shift_id, test_id, status, fail, weights, comment, check_time
                FROM quality_check
                WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    QualityCheck = new QualityCheckItem
                    {
                        Id = reader.GetInt64(0),
                        Shift_Id = reader.GetInt64(1),
                        TestId = reader.GetInt64(2),
                        Status = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Fail = reader.IsDBNull(4) ? false : reader.GetBoolean(4),
                        Weights = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Comment = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        Check_Time = reader.IsDBNull(7) ? null : reader.GetTimeSpan(7)
                    };
                }

                reader.Close();

                // Load test templates
                sql = "SELECT id, test_name FROM quality_checks_template ORDER BY test_name";
                using var testCmd = new NpgsqlCommand(sql, conn);
                using var testReader = testCmd.ExecuteReader();

                while (testReader.Read())
                {
                    TestList.Add(new TestTemplate
                    {
                        Id = testReader.GetInt64(0),
                        TestName = testReader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading quality check: {ex.Message}";
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                UPDATE quality_check
                SET shift_id=@shift, test_id=@test, status=@status, fail=@fail,
                    weights=@weights, comment=@comment, check_time=@check_time
                WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@shift", QualityCheck.Shift_Id);
                cmd.Parameters.AddWithValue("@test", QualityCheck.TestId);
                cmd.Parameters.AddWithValue("@status", (object?)QualityCheck.Status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fail", QualityCheck.Fail);
                cmd.Parameters.AddWithValue("@weights", (object?)QualityCheck.Weights ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comment", (object?)QualityCheck.Comment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@check_time", (object?)QualityCheck.Check_Time ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", QualityCheck.Id);

                cmd.ExecuteNonQuery();

                return RedirectToPage("/Quality_Check_Report");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating quality check: {ex.Message}";
                return Page();
            }
        }
    }

}

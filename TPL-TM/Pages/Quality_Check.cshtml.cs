using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Quality_CheckModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        [BindProperty]
        public long ShiftId { get; set; }

        [BindProperty]
        public string ProductCategory { get; set; } = string.Empty;

        [BindProperty]
        public List<QualityCheckInput> TestChecks { get; set; } = new();

        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public Quality_CheckModel(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // -----------------------------
        // Load templates on GET
        // -----------------------------
        public void OnGet(long? shiftId, string? productCategory)
        {
            if (shiftId.HasValue)
                ShiftId = shiftId.Value;

            if (!string.IsNullOrEmpty(productCategory))
                ProductCategory = productCategory;

            LoadQualityCheckTemplates(ProductCategory);
        }

        // -----------------------------
        // Handle POST (insert all checks)
        // -----------------------------
        public IActionResult OnPost()
        {
            try
            {
                if (TestChecks == null || TestChecks.Count == 0)
                {
                    ErrorMessage = "No quality checks found to submit.";
                    return Page();
                }

                var createdBy = User.Identity?.Name ?? "Unknown";

                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string insertQuery = @"
                    INSERT INTO quality_check
                    (shift_id, test_id, check_time, status, fail, weights, comment, created_by, created_on)
                    VALUES (@ShiftId, @TestId, NOW()::time, @Status, @Fail, @Weights, @Comment, @CreatedBy, NOW())";

                foreach (var test in TestChecks)
                {
                    using var cmd = new NpgsqlCommand(insertQuery, connection);
                    cmd.Parameters.AddWithValue("@ShiftId", ShiftId);
                    cmd.Parameters.AddWithValue("@TestId", test.TestId);
                    cmd.Parameters.AddWithValue("@Status", test.Status ? "Pass" : "Fail");
                    cmd.Parameters.AddWithValue("@Fail", !test.Status);
                    cmd.Parameters.AddWithValue("@Weights", test.Weights.HasValue ? (object)test.Weights.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Comment", string.IsNullOrEmpty(test.Comment) ? DBNull.Value : (object)test.Comment);
                    cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                    cmd.ExecuteNonQuery();
                }


                SuccessMessage = "✅ Quality checks submitted successfully!";
                return RedirectToPage("/Shift_Dashboard");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to save quality checks: {ex.Message}";
                LoadQualityCheckTemplates(ProductCategory); // reload for retry
                return Page();
            }
        }

        // -----------------------------
        // Load test templates by product category
        // -----------------------------
        private void LoadQualityCheckTemplates(string category)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = @"
                    SELECT id, test_name
                    FROM quality_checks_template
                    WHERE (@Category = '' OR product_category = @Category)
                    ORDER BY id";

                using var cmd = new NpgsqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Category", category ?? string.Empty);

                using var reader = cmd.ExecuteReader();
                TestChecks.Clear();

                while (reader.Read())
                {
                    TestChecks.Add(new QualityCheckInput
                    {
                        TestId = reader.GetInt64(0),
                        TestName = reader.GetString(1),
                        Status = false, // default Pass
                        Comment = string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to load quality check templates: {ex.Message}";
            }
        }
    }

    // -----------------------------
    // Supporting classes
    // -----------------------------
    public class QualityCheckInput
    {
        public long TestId { get; set; }
        public string TestName { get; set; }
        public bool Status { get; set; } // true = Pass, false = Fail
        public string Comment { get; set; }
        public decimal? Weights { get; set; } // NEW FIELD
    }

    public class QualityCheckInfo
    {
        public long Id { get; set; }
        public long Shift_Id { get; set; }
        public long TestId { get; set; }          // ✅ New Test ID
        public string Test_Name { get; set; }
        public string Test_Type { get; set; }
        public TimeSpan? Check_Time { get; set; }
        public string Status { get; set; }
        public bool Fail { get; set; }
        public string Weights { get; set; }
        public string Comment { get; set; }
        public string Created_By { get; set; }
        public DateTime? Created_On { get; set; }
    }
}

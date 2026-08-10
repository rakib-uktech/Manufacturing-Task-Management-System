using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_EndModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_EndModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public ShiftInfo ShiftInfo { get; set; } = new();

        public ShiftInfo? ActiveShift { get; set; }

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet(int? shiftId)
        {
            try
            {
                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                string sql = @"
                    SELECT id, machine_line, machine_name, product_line, shift_start_time
                    FROM shift
                    WHERE shift_active = TRUE
                      AND (@ShiftId IS NULL OR id = @ShiftId)
                    ORDER BY id DESC
                    LIMIT 1";

                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ShiftId", (object?)shiftId ?? DBNull.Value);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    ActiveShift = new ShiftInfo
                    {
                        Shift_Id = reader["id"].ToString(),
                        Machine_Line = reader["machine_line"].ToString(),
                        Machine_Name = reader["machine_name"].ToString(),
                        Product_Line = reader["product_line"].ToString(),
                        Shift_Start_Time = reader["shift_start_time"] as DateTime?
                    };
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Error loading shift: {ex.Message}";
            }
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(ShiftInfo.Shift_Id))
            {
                ErrorMessage = "❌ Shift ID missing. Please refresh the page and try again.";
                return Page();
            }

            try
            {
                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                string sql = @"
                    UPDATE shift
                    SET shift_end_time = @EndTime,
                        shift_rating = @Rating,
                        comment = @Comment,
                        shift_active = FALSE
                    WHERE id = @ShiftId
                    RETURNING id;";

                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ShiftId", int.Parse(ShiftInfo.Shift_Id));
                cmd.Parameters.AddWithValue("@EndTime", ShiftInfo.Shift_End_Time ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@Rating", ShiftInfo.Shift_Rating ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Comment", (object?)ShiftInfo.Comment ?? DBNull.Value);

                var result = cmd.ExecuteScalar();

                if (result != null)
                {
                    TempData["Message"] = "✅ Shift ended successfully.";
                    return RedirectToPage("/Operator_Index");
                }
                else
                {
                    ErrorMessage = "⚠️ No active shift found to end.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to end shift: {ex.Message}";
                return Page();
            }
        }
    }
}

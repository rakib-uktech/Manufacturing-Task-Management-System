using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class Shift_Check_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_Check_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public ShiftCheckItem ShiftCheck = new ShiftCheckItem();

        public class ShiftCheckItem
        {
            public long Id { get; set; }
            public string Check_Name { get; set; }
            public bool Check_Status { get; set; }
            public string Check_Comment { get; set; }
            public string Check_Created_By { get; set; }
            public DateTime Shift_Created_On { get; set; }
        }

        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!long.TryParse(id, out long checkId))
            {
                errorMessage = "Invalid Shift Check ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, check_name, check_status, comment, created_by, created_on
                    FROM public.shift_checks
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", checkId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    ShiftCheck.Id = reader.GetInt64(reader.GetOrdinal("id"));
                    ShiftCheck.Check_Name = reader["check_name"]?.ToString();
                    ShiftCheck.Check_Status = reader.GetBoolean(reader.GetOrdinal("check_status"));
                    ShiftCheck.Check_Comment = reader["comment"]?.ToString();
                    ShiftCheck.Check_Created_By = reader["created_by"]?.ToString();
                    ShiftCheck.Shift_Created_On = reader.GetDateTime(reader.GetOrdinal("created_on"));
                }
                else
                {
                    errorMessage = "Shift check record not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error loading shift check: " + ex.Message;
            }
        }

        public IActionResult OnPostDeleteShiftCheck(long Id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM public.shift_checks WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    TempData["SuccessMessage"] = "Shift check deleted successfully!";
                else
                    TempData["ErrorMessage"] = "Shift check not found.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting shift check: " + ex.Message;
            }

            return RedirectToPage("/Shift_Check_Report");
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class Shift_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Shift_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string ErrorMessage = "";
        public ShiftItem Shift = new ShiftItem();

        public class ShiftItem
        {
            public long Id { get; set; }
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public DateTime? Shift_Start_Time { get; set; }
            public DateTime? Shift_End_Time { get; set; }
            public bool Shift_Active { get; set; }
            public int? Handover_Rating { get; set; }
        }

        public void OnGet()
        {
            string id = Request.Query["id"];

            if (!long.TryParse(id, out long shiftId))
            {
                ErrorMessage = "Invalid shift ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "SELECT * FROM shift WHERE id=@id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", shiftId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Shift.Id = reader.GetInt64(reader.GetOrdinal("id"));
                    Shift.Machine_Line = reader["machine_line"].ToString();
                    Shift.Machine_Name = reader["machine_name"].ToString();
                    Shift.Product_Line = reader["product_line"].ToString();
                    Shift.Shift_Start_Time = reader.IsDBNull(reader.GetOrdinal("shift_start_time")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_start_time"));
                    Shift.Shift_End_Time = reader.IsDBNull(reader.GetOrdinal("shift_end_time")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_end_time"));
                    Shift.Shift_Active = reader.GetBoolean(reader.GetOrdinal("shift_active"));
                    Shift.Handover_Rating = reader.IsDBNull(reader.GetOrdinal("handover_rating")) ? null : reader.GetInt32(reader.GetOrdinal("handover_rating"));
                }
                else
                {
                    ErrorMessage = "Shift record not found!";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public IActionResult OnPostDeleteShift(long id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid shift ID!";
                return RedirectToPage("/Shift_Report");
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM shift WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "Shift record deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting shift: {ex.Message}";
            }

            return RedirectToPage("/Shift_Report");
        }
    }
}

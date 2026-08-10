using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Waste_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Waste_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public string successMessage = "";
        public WasteInfo Waste { get; set; } = new WasteInfo();
        public int item_id;

        public class WasteInfo
        {
            public int Id { get; set; }
            public long Shift_Id { get; set; }
            public decimal? Waste_Weight { get; set; }
            public string Waste_Type { get; set; } = "";
            public string Created_By { get; set; } = "";
            public DateTime? Created_At { get; set; }
        }

        // GET: Load waste record
        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out int wasteId))
            {
                item_id = wasteId;
            }
            else
            {
                errorMessage = "Invalid Waste ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "SELECT * FROM waste WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", item_id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Waste = new WasteInfo
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Shift_Id = reader.GetInt64(reader.GetOrdinal("shift_id")),
                        Waste_Weight = reader.IsDBNull(reader.GetOrdinal("waste_weight")) ? null : reader.GetDecimal(reader.GetOrdinal("waste_weight")),
                        Waste_Type = reader.IsDBNull(reader.GetOrdinal("waste_type")) ? "" : reader.GetString(reader.GetOrdinal("waste_type")),
                        Created_By = reader.IsDBNull(reader.GetOrdinal("created_by")) ? "" : reader.GetString(reader.GetOrdinal("created_by")),
                        Created_At = reader.IsDBNull(reader.GetOrdinal("created_on")) ? null : reader.GetDateTime(reader.GetOrdinal("created_on"))
                    };
                }
                else
                {
                    errorMessage = "Waste record not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error fetching waste record: " + ex.Message;
            }
        }

        // POST: Delete waste record
        public IActionResult OnPostDeleteWaste(int Id)
        {
            if (Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Waste ID!";

                if (User.IsInRole("User"))
                {
                    return RedirectToPage("/Operator_Index");
                }
                return RedirectToPage("/Waste_RecordsReport");
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM waste WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Waste record deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Waste record not found. Deletion failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting waste record: " + ex.Message;
            }

            if (User.IsInRole("User"))
            {
                return RedirectToPage("/Operator_Index");
            }
            return RedirectToPage("/Waste_RecordsReport");
        }
    }
}

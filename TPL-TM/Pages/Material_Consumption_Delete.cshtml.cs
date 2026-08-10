using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Material_Consumption_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Material_Consumption_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public string successMessage = "";
        public MaterialItem material = new MaterialItem();
        public int item_id;

        public class MaterialItem
        {
            public int Id { get; set; }
            public long Shift_Id { get; set; }
            public string Wo_Number { get; set; } = "";
            public string Material_Id { get; set; } = "";
            public string Material_Description { get; set; } = "";
            public decimal Quantity_Consumed { get; set; }
        }

        // GET: Load material record
        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out int matId))
            {
                item_id = matId;
            }
            else
            {
                errorMessage = "Invalid Material ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "SELECT * FROM material_consumption WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", item_id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    material = new MaterialItem
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Shift_Id = reader.IsDBNull(reader.GetOrdinal("shift_id")) ? 0 : reader.GetInt64(reader.GetOrdinal("shift_id")),
                        Wo_Number = reader.IsDBNull(reader.GetOrdinal("wo_number")) ? "" : reader.GetString(reader.GetOrdinal("wo_number")),
                        Material_Id = reader.IsDBNull(reader.GetOrdinal("material_id")) ? "" : reader.GetString(reader.GetOrdinal("material_id")),
                        Material_Description = reader.IsDBNull(reader.GetOrdinal("material_description")) ? "" : reader.GetString(reader.GetOrdinal("material_description")),
                        Quantity_Consumed = reader.IsDBNull(reader.GetOrdinal("quantity_consumed")) ? 0 : reader.GetDecimal(reader.GetOrdinal("quantity_consumed"))
                    };
                }
                else
                {
                    errorMessage = "Material record not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error fetching material record: " + ex.Message;
            }
        }

        // POST: Handle deletion
        public IActionResult OnPostDeleteMaterial(int Id)
        {
            if (Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Material ID!";
                return RedirectToPage("/Material_Consumption_Report");
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM material_consumption WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Material record deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Material record not found. Deletion failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting material record: " + ex.Message;
            }
            if (User.IsInRole("User"))
            {
                return RedirectToPage("/Operator_Index");
            }
            return RedirectToPage("/Material_Consumption_Report");
        }
    }
}

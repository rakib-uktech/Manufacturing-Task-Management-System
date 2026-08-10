using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Material_Consumption_EditModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Material_Consumption_EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public MaterialItem Material { get; set; } = new MaterialItem();

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public class MaterialItem
        {
            public int Id { get; set; }
            public long Shift_Id { get; set; }
            public string Wo_Number { get; set; } = "";
            public string Material_Id { get; set; } = "";
            public string Material_Description { get; set; } = "";
            public decimal Quantity_Consumed { get; set; }
        }

        // Make id nullable so page works for both Add and Edit
        public void OnGet(int? id)
        {
            if (id == null)
            {
                ErrorMessage = "No material ID provided.";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "SELECT * FROM material_consumption WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id.Value);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Material = new MaterialItem
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
                    ErrorMessage = "Material record not found.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading material record: {ex.Message}";
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    UPDATE material_consumption
                    SET shift_id=@shift, wo_number=@wo, material_id=@mid,
                        material_description=@desc, quantity_consumed=@qty
                    WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@shift", Material.Shift_Id);
                cmd.Parameters.AddWithValue("@wo", Material.Wo_Number);
                cmd.Parameters.AddWithValue("@mid", Material.Material_Id);
                cmd.Parameters.AddWithValue("@desc", Material.Material_Description);
                cmd.Parameters.AddWithValue("@qty", Material.Quantity_Consumed);
                cmd.Parameters.AddWithValue("@id", Material.Id);

                cmd.ExecuteNonQuery();

                SuccessMessage = "Material consumption record updated successfully.";
                if (User.IsInRole("User"))
                {
                    return RedirectToPage("/Operator_Index");
                }
                return RedirectToPage("/Material_Consumption_Report");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating material record: {ex.Message}";
                return Page();
            }
        }
    }
}

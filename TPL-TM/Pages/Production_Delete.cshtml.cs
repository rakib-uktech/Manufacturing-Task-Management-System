using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Production_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public string successMessage = "";
        public ProductionItem production = new ProductionItem();

        public int item_id;

        public class ProductionItem
        {
            public int Id { get; set; }
            public string Shift_Letter { get; set; } = "";
            public string Wo_Number { get; set; } = "";
            public string Batch_Identifier { get; set; } = "";
            public string Machine_Line { get; set; } = "";
            public string Machine_Name { get; set; } = "";
            public string Product_Line { get; set; } = "";
            public string Part_Number { get; set; } = "";
            public string Item_Description { get; set; } = "";
            public DateTime? Timestamp_Start { get; set; }
            public DateTime? Timestamp_End { get; set; }
            public int Product_Count { get; set; }
        }

        // GET: Load production record
        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out int prodId))
            {
                item_id = prodId;
            }
            else
            {
                errorMessage = "Invalid Production ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();
                string sql = "SELECT * FROM production_count WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", item_id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    production = new ProductionItem
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Shift_Letter = reader.GetString(reader.GetOrdinal("shift_letter")),
                        Wo_Number = reader.GetString(reader.GetOrdinal("wo_number")),
                        Batch_Identifier = reader.GetString(reader.GetOrdinal("batch_identifier")),
                        Timestamp_Start = reader.IsDBNull(reader.GetOrdinal("timestamp_start")) ? null : reader.GetDateTime(reader.GetOrdinal("timestamp_start")),
                        Timestamp_End = reader.IsDBNull(reader.GetOrdinal("timestamp_end")) ? null : reader.GetDateTime(reader.GetOrdinal("timestamp_end")),
                        Product_Count = reader.IsDBNull(reader.GetOrdinal("product_count")) ? 0 : reader.GetInt32(reader.GetOrdinal("product_count")),
                        Machine_Line = reader.GetString(reader.GetOrdinal("machine_line")),
                        Machine_Name = reader.GetString(reader.GetOrdinal("machine_name")),
                        Product_Line = reader.GetString(reader.GetOrdinal("product_line")),
                        Part_Number = reader.GetString(reader.GetOrdinal("part_number")),
                        Item_Description = reader.GetString(reader.GetOrdinal("item_description"))
                    };
                }
                else
                {
                    errorMessage = "Production record not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error fetching production record: " + ex.Message;
            }
        }

        // POST: Handle deletion
        public IActionResult OnPostDeleteProduction(int Id)
        {
            if (Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Production ID!";
                return RedirectToPage("/ProductionReport");
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM production_count WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Production record deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Production record not found. Deletion failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting production record: " + ex.Message;
            }
            if (User.IsInRole("User"))
            {
                return RedirectToPage("/Operator_Index");
            }
            return RedirectToPage("/ProductionReport");
        }
    }
}
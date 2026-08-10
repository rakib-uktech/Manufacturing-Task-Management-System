using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_EditModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public Production_EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public ProductionItem Production { get; set; } = new ProductionItem();

        public List<string> ShiftLetters { get; set; } = new List<string>();
        public List<string> MachineNames { get; set; } = new List<string>();

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public class ProductionItem
        {
            public int Id { get; set; }
            public string Shift_Letter { get; set; } = "";
            public string Wo_Number { get; set; } = "";
            public string Batch_Identifier { get; set; } = "";
            public DateTime? Timestamp_Start { get; set; }
            public DateTime? Timestamp_End { get; set; }
            public int Product_Count { get; set; }
            public string Machine_Line { get; set; } = "";
            public string Machine_Name { get; set; } = "";
            public string Product_Line { get; set; } = "";
            public string Part_Number { get; set; } = "";
            public string Item_Description { get; set; } = "";
        }

        public void OnGet(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                // Load production record
                string sql = "SELECT * FROM production_count WHERE id = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Production = new ProductionItem
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Shift_Letter = reader.GetString(reader.GetOrdinal("shift_letter")),
                        Wo_Number = reader.GetString(reader.GetOrdinal("wo_number")),
                        Batch_Identifier = reader.IsDBNull(reader.GetOrdinal("batch_identifier"))? ""
                        : reader.GetString(reader.GetOrdinal("batch_identifier")),
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

                reader.Close();

                // Load distinct Shift Letters
                sql = "SELECT DISTINCT shift_letter FROM production_count ORDER BY shift_letter";
                using var shiftCmd = new NpgsqlCommand(sql, conn);
                using var shiftReader = shiftCmd.ExecuteReader();
                while (shiftReader.Read())
                {
                    ShiftLetters.Add(shiftReader.GetString(0));
                }
                shiftReader.Close();

                // Load distinct Machine Names
                sql = "SELECT DISTINCT machine_name FROM production_count ORDER BY machine_name";
                using var machineCmd = new NpgsqlCommand(sql, conn);
                using var machineReader = machineCmd.ExecuteReader();
                while (machineReader.Read())
                {
                    MachineNames.Add(machineReader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading production record: {ex.Message}";
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    UPDATE production_count
                    SET shift_letter=@shift, wo_number=@wo, batch_identifier=@batch,
                        timestamp_start=@start, timestamp_end=@end, product_count=@count,
                        machine_line=@line, machine_name=@name, product_line=@pline,
                        part_number=@part, item_description=@desc
                    WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@shift", Production.Shift_Letter);
                cmd.Parameters.AddWithValue("@wo", Production.Wo_Number);
                cmd.Parameters.AddWithValue("@batch", Production.Batch_Identifier);
                cmd.Parameters.AddWithValue("@start", Production.Timestamp_Start ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@end", Production.Timestamp_End ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@count", Production.Product_Count);
                cmd.Parameters.AddWithValue("@line", Production.Machine_Line);
                cmd.Parameters.AddWithValue("@name", Production.Machine_Name);
                cmd.Parameters.AddWithValue("@pline", Production.Product_Line);
                cmd.Parameters.AddWithValue("@part", Production.Part_Number);
                cmd.Parameters.AddWithValue("@desc", Production.Item_Description);
                cmd.Parameters.AddWithValue("@id", Production.Id);

                cmd.ExecuteNonQuery();
                SuccessMessage = "Production record updated successfully.";
                
                if (User.IsInRole("User"))
                {
                    return RedirectToPage("/Operator_Index");
                }
                return RedirectToPage("/ProductionReport");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating production record: {ex.Message}";
                return Page();
            }
        }
    }
}

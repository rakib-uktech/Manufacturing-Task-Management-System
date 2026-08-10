using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_UpdateModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Production_UpdateModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public ProductionInfo ProductionInfo { get; set; } = new();

        public List<string> MachineNames { get; set; } = new();

        public string ErrorMessage { get; set; }

        // ============================
        // LOAD (CREATE + EDIT)
        // ============================
        public void OnGet(long? id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                if (id.HasValue)
                {
                    string sql = "SELECT * FROM production_count WHERE id = @id";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", id.Value);

                    using var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        ProductionInfo = new ProductionInfo
                        {
                            Id = reader.GetInt64(reader.GetOrdinal("id")),
                            Shift_Id = reader.GetInt64(reader.GetOrdinal("shift_id")),
                            Shift_Letter = reader["shift_letter"]?.ToString(),

                            Wo_Number = reader["wo_number"]?.ToString(),
                            Batch_Identifier = reader["batch_identifier"]?.ToString(),

                            Timestamp_Start = reader["timestamp_start"] as DateTime?,
                            Timestamp_End = reader["timestamp_end"] as DateTime?,

                            Product_Count = reader["product_count"] == DBNull.Value
                                ? 0
                                : Convert.ToInt64(reader["product_count"]),

                            Machine_Line = reader["machine_line"]?.ToString(),
                            Machine_Name = reader["machine_name"]?.ToString(),
                            Product_Line = reader["product_line"]?.ToString(),

                            Part_Number = reader["part_number"]?.ToString(),
                            Item_Description = reader["item_description"]?.ToString()
                        };
                    }
                }
                else
                {
                    // ✅ NEW RECORD DEFAULTS
                    ProductionInfo.Timestamp_Start = DateTime.Now;
                    ProductionInfo.Timestamp_End = DateTime.Now;
                    ProductionInfo.Shift_Id = GetLatestShiftId(conn);
                }

                LoadMachineNames(conn);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        // ============================
        // SAVE (INSERT + UPDATE)
        // ============================
        public IActionResult OnPost()
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                // ✅ Ensure Shift_Id always valid
                if (ProductionInfo.Shift_Id <= 0)
                {
                    ProductionInfo.Shift_Id = GetLatestShiftId(conn);
                }

                if (string.IsNullOrWhiteSpace(ProductionInfo.Shift_Letter))
                {
                    ErrorMessage = "Shift Letter is required.";
                    LoadMachineNames(conn);
                    return Page();
                }

                if (ProductionInfo.Id > 0)
                {
                    // 🔹 UPDATE
                    string sql = @"
                        UPDATE production_count
                        SET shift_id = @ShiftId,
                            shift_letter = @ShiftLetter,
                            wo_number = @Wo,
                            batch_identifier = @Batch,
                            timestamp_start = @Start,
                            timestamp_end = @End,
                            product_count = @Count,
                            machine_line = @Line,
                            machine_name = @Name,
                            product_line = @ProductLine,
                            part_number = @Part,
                            item_description = @Desc,
                            modified_by = @User,
                            modified_on = NOW()
                        WHERE id = @Id";

                    using var cmd = new NpgsqlCommand(sql, conn);

                    AddParameters(cmd);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    // 🔹 INSERT
                    string sql = @"
                        INSERT INTO production_count
                        (shift_id, shift_letter, wo_number, batch_identifier,
                         timestamp_start, timestamp_end, product_count,
                         machine_line, machine_name, product_line,
                         part_number, item_description,
                         created_by, created_at)
                        VALUES
                        (@ShiftId, @ShiftLetter, @Wo, @Batch,
                         @Start, @End, @Count,
                         @Line, @Name, @ProductLine,
                         @Part, @Desc,
                         @User, NOW())";

                    using var cmd = new NpgsqlCommand(sql, conn);

                    AddParameters(cmd);

                    cmd.ExecuteNonQuery();
                }

                return User.IsInRole("User")
                    ? RedirectToPage("/Operator_Index")
                    : RedirectToPage("/ProductionReport");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        // ============================
        // HELPERS
        // ============================

        private void AddParameters(NpgsqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Id", ProductionInfo.Id);
            cmd.Parameters.AddWithValue("@ShiftId", ProductionInfo.Shift_Id);
            cmd.Parameters.AddWithValue("@ShiftLetter", ProductionInfo.Shift_Letter ?? "");
            cmd.Parameters.AddWithValue("@Wo", ProductionInfo.Wo_Number ?? "");
            cmd.Parameters.AddWithValue("@Batch", ProductionInfo.Batch_Identifier ?? "");
            cmd.Parameters.AddWithValue("@Start", ProductionInfo.Timestamp_Start ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@End", ProductionInfo.Timestamp_End ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Count", ProductionInfo.Product_Count);
            cmd.Parameters.AddWithValue("@Line", ProductionInfo.Machine_Line ?? "");
            cmd.Parameters.AddWithValue("@Name", ProductionInfo.Machine_Name ?? "");
            cmd.Parameters.AddWithValue("@ProductLine", ProductionInfo.Product_Line ?? "");
            cmd.Parameters.AddWithValue("@Part", ProductionInfo.Part_Number ?? "");
            cmd.Parameters.AddWithValue("@Desc", ProductionInfo.Item_Description ?? "");
            cmd.Parameters.AddWithValue("@User", User.Identity?.Name ?? "System");
        }

        private long GetLatestShiftId(NpgsqlConnection conn)
        {
            using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(id),1) FROM shift", conn);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        private void LoadMachineNames(NpgsqlConnection conn)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT DISTINCT machine_name FROM production_count ORDER BY machine_name", conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                MachineNames.Add(reader.GetString(0));
        }
    }
}
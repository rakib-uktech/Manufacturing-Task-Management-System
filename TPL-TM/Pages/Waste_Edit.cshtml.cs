using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Waste_EditModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Waste_EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public WasteInfo Waste { get; set; } = new WasteInfo();

        public List<string> WasteTypeList { get; set; } = new List<string>();

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public class WasteInfo
        {
            public int Id { get; set; }
            public long Shift_Id { get; set; }
            public decimal? Waste_Weight { get; set; }
            public string Waste_Type { get; set; } = "";
            public string Created_By { get; set; } = "";
        }

        // ------------------- OnGet -------------------
        public void OnGet(int id, long? shiftId)
        {
            try
            {
                if (shiftId.HasValue)
                    Waste.Shift_Id = shiftId.Value;

                // Load waste record if editing
                if (id > 0)
                {
                    using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    conn.Open();

                    string sql = "SELECT * FROM waste WHERE id = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        Waste = new WasteInfo
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Shift_Id = reader.GetInt64(reader.GetOrdinal("shift_id")),
                            Waste_Weight = reader.IsDBNull(reader.GetOrdinal("waste_weight")) ? null : reader.GetDecimal(reader.GetOrdinal("waste_weight")),
                            Waste_Type = reader.IsDBNull(reader.GetOrdinal("waste_type")) ? "" : reader.GetString(reader.GetOrdinal("waste_type")),
                            Created_By = reader.IsDBNull(reader.GetOrdinal("created_by")) ? "" : reader.GetString(reader.GetOrdinal("created_by"))
                        };
                    }
                }

                // Fetch Waste Types from NetSuite via ODBC
                string odbcConn = _configuration.GetConnectionString("NetSuiteOdbc");
                using var odbcConnection = new OdbcConnection(odbcConn);
                odbcConnection.Open();

                string typeSql = "SELECT name AS wastetype FROM CUSTOMLIST1426 WHERE name IS NOT NULL ORDER BY name ASC;";
                using var typeCmd = new OdbcCommand(typeSql, odbcConnection);
                using var typeReader = typeCmd.ExecuteReader();
                WasteTypeList.Clear();
                while (typeReader.Read())
                {
                    WasteTypeList.Add(typeReader["wastetype"].ToString());
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error loading waste record or waste types: " + ex.Message;
            }
        }

        // ------------------- OnPost -------------------
        public IActionResult OnPost()
        {
            try
            {
                Waste.Created_By = User.Identity?.Name ?? "Unknown";

                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = Waste.Id > 0
                    ? @"UPDATE waste
                        SET shift_id=@shift, waste_weight=@weight, waste_type=@type
                        WHERE id=@id"
                    : @"INSERT INTO waste (shift_id, waste_weight, waste_type, created_by)
                        VALUES (@shift, @weight, @type, @created_by)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@shift", Waste.Shift_Id);
                cmd.Parameters.AddWithValue("@weight", (object?)Waste.Waste_Weight ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (object?)Waste.Waste_Type ?? DBNull.Value);

                if (Waste.Id > 0)
                    cmd.Parameters.AddWithValue("@id", Waste.Id);
                else
                    cmd.Parameters.AddWithValue("@created_by", Waste.Created_By);

                cmd.ExecuteNonQuery();
                SuccessMessage = Waste.Id > 0 ? "Waste record updated successfully." : "Waste record added successfully.";

                if (User.IsInRole("User"))
                {
                    return RedirectToPage("/Operator_Index");
                }
                return RedirectToPage("/Waste_RecordsReport");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error saving waste record: " + ex.Message;
                return Page();
            }
        }
    }
}

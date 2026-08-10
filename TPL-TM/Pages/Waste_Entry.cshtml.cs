using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Waste_EntryModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        [BindProperty]
        public WasteInfo WasteInfo { get; set; } = new WasteInfo();

        private readonly IConfiguration _configuration;

        public List<string> WasteTypeList { get; set; } = new List<string>();
        public List<WasteEntryView> WasteEntries { get; set; } = new();

        public Waste_EntryModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ------------------- OnGet -------------------
        public void OnGet(long? shiftId)
        {
            try
            {
                if (shiftId.HasValue)
                    WasteInfo.Shift_Id = shiftId.Value;

                // --- Load Waste Types (already done) ---
                string odbcConn = _configuration.GetConnectionString("NetSuiteOdbc");
                using (var connection = new OdbcConnection(odbcConn))
                {
                    connection.Open();
                    string sql = "SELECT name FROM CUSTOMLIST1426 WHERE name IS NOT NULL ORDER BY name ASC;";
                    using var cmd = new OdbcCommand(sql, connection);
                    using var reader = cmd.ExecuteReader();
                    WasteTypeList.Clear();
                    while (reader.Read())
                        WasteTypeList.Add(reader["name"].ToString());
                }

                // --- Load Waste Entries ---
                using var pg = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                pg.Open();

                string wasteSql = @"
                    SELECT id, waste_weight, waste_type, created_by, created_on
                    FROM waste
                    WHERE shift_id = @ShiftId
                    ORDER BY created_on DESC";

                using var wasteCmd = new NpgsqlCommand(wasteSql, pg);
                wasteCmd.Parameters.AddWithValue("@ShiftId", WasteInfo.Shift_Id);

                using var r = wasteCmd.ExecuteReader();
                WasteEntries.Clear();
                while (r.Read())
                {
                    WasteEntries.Add(new WasteEntryView
                    {
                        Id = r.GetInt64(0),
                        Waste_Weight = r.IsDBNull(1) ? null : r.GetDecimal(1),
                        Waste_Type = r.GetString(2),
                        Created_By = r.GetString(3),
                        Created_At = r.IsDBNull(4) ? null : r.GetDateTime(4)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Failed to load waste data: " + ex.Message;
            }
        }

        // ------------------- OnPost -------------------
        public IActionResult OnPost()
        {
            try
            {
                WasteInfo.Created_By = User.Identity?.Name ?? "Unknown";

                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                string query = @"
                    INSERT INTO waste (shift_id, waste_weight, waste_type, created_by)
                    VALUES (@ShiftId, @WasteWeight, @WasteType, @CreatedBy)";

                using var cmd = new NpgsqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@ShiftId", WasteInfo.Shift_Id);
                cmd.Parameters.AddWithValue("@WasteWeight", (object?)WasteInfo.Waste_Weight ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WasteType", (object?)WasteInfo.Waste_Type ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", WasteInfo.Created_By);

                cmd.ExecuteNonQuery();

                SuccessMessage = "✅ Waste record added successfully.";
                return RedirectToPage("/Shift_Dashboard");
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Failed to insert waste record: " + ex.Message;
                return Page();
            }
        }
    }

    // ------------------- Model -------------------
    public class WasteInfo
    {
        public long Shift_Id { get; set; }           // will be auto-filled from dashboard
        public decimal? Waste_Weight { get; set; }
        public string Waste_Type { get; set; }
        public string Created_By { get; set; }
        public DateTime? Created_At { get; set; }       
    }
    public class WasteEntryView
    {
        public long Id { get; set; }
        public decimal? Waste_Weight { get; set; }
        public string Waste_Type { get; set; }
        public string Created_By { get; set; }
        public DateTime? Created_At { get; set; }
    }

}

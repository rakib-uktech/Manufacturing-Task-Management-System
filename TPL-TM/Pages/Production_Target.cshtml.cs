using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_TargetModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public ProductionTargetInfo TargetInfo { get; set; } = new ProductionTargetInfo();

        private readonly IConfiguration _configuration;
        public string ConnectionString { get; private set; }

        public Production_TargetModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public List<string> MachineLineList { get; set; } = new();
        public List<string> MachineNameList { get; set; } = new();
        public List<string> ProductLineList { get; set; } = new();

        public void OnGet(long? id)
        {
            if (id.HasValue)
            {
                // Load existing record for edit
                LoadTargetById(id.Value);
            }

            // Load dropdowns from NetSuite
            LoadDropdownDataFromNetSuite();
        }
        private void LoadTargetById(long id)
        {
            ConnectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            string query = "SELECT * FROM production_target WHERE id = @Id";
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                TargetInfo.Id = reader.GetInt64(reader.GetOrdinal("id"));
                TargetInfo.Machine_Line = reader["machine_line"]?.ToString();
                TargetInfo.Machine_Name = reader["machine_name"]?.ToString();
                TargetInfo.Product_Line = reader["product_line"]?.ToString();
                TargetInfo.Target_Count = reader.GetInt64(reader.GetOrdinal("target_count"));
                TargetInfo.Effective_Date = reader.GetDateTime(reader.GetOrdinal("effective_date"));
                TargetInfo.Is_Active = reader.GetBoolean(reader.GetOrdinal("is_active"));
            }
        }
        private void LoadDropdownDataFromNetSuite()
        {
            try
            {
                string nsConnection = _configuration.GetConnectionString("NetSuiteOdbc");

                using var connection = new OdbcConnection(nsConnection);
                connection.Open();

                // --- Machine Names ---
                string sql = "SELECT DISTINCT name FROM CUSTOMLIST_TR_MACHINE_LIST ORDER BY name";
                using (var cmd = new OdbcCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        MachineNameList.Add(reader["name"]?.ToString());
                }

                // --- Machine Lines from Product Type list ---
                var productTypeMaps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Cups", "CUP" },
            { "Straws", "QSR" },
            { "Bottles", "BOT" }
        };

                sql = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";
                using (var cmd = new OdbcCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var rawProductType = reader["name"]?.ToString();
                        var mappedProductType = rawProductType != null && productTypeMaps.ContainsKey(rawProductType)
                                                ? productTypeMaps[rawProductType]
                                                : rawProductType ?? "Unknown";

                        MachineLineList.Add(mappedProductType);
                    }
                }

                // --- Product Lines (if separate, e.g., classification) ---
                sql = "SELECT DISTINCT fullname FROM classification ORDER BY fullname";
                using (var cmd = new OdbcCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var line = reader["fullname"]?.ToString();
                        if (!string.IsNullOrEmpty(line))
                            ProductLineList.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load dropdowns from NetSuite: " + ex.Message;
            }
        }

        public IActionResult OnPostUpdate()
        {
            try
            {
                TargetInfo = new ProductionTargetInfo
                {
                    Id = long.Parse(Request.Form["Id"]),
                    Machine_Line = Request.Form["Machine_Line"],
                    Machine_Name = Request.Form["Machine_Name"],
                    Product_Line = Request.Form["Product_Line"],
                    Target_Count = long.Parse(Request.Form["Target_Count"]),
                    Effective_Date = DateTime.Parse(Request.Form["Effective_Date"]),
                    Is_Active = Request.Form["Is_Active"] == "on",
                };

                ConnectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = @"
                UPDATE production_target
                SET machine_line = @Machine_Line,
                    machine_name = @Machine_Name,
                    product_line = @Product_Line,
                    target_count = @Target_Count,
                    effective_date = @Effective_Date,
                    is_active = @Is_Active
                WHERE id = @Id";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Machine_Line", TargetInfo.Machine_Line);
                command.Parameters.AddWithValue("@Machine_Name", (object?)TargetInfo.Machine_Name ?? DBNull.Value);
                command.Parameters.AddWithValue("@Product_Line", TargetInfo.Product_Line);
                command.Parameters.AddWithValue("@Target_Count", TargetInfo.Target_Count);
                command.Parameters.AddWithValue("@Effective_Date", TargetInfo.Effective_Date);
                command.Parameters.AddWithValue("@Id", TargetInfo.Id);
                command.Parameters.AddWithValue("@Is_Active", TargetInfo.Is_Active);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Update failed: " + ex.Message;
                return Page();
            }

            SuccessMessage = "✅ Record updated successfully.";
            return RedirectToPage("/Production_Target_List");
        }

        public IActionResult OnPost()
        {
            try
            {
                TargetInfo = new ProductionTargetInfo
                {
                    Machine_Line = Request.Form["Machine_Line"],
                    Machine_Name = Request.Form["Machine_Name"],
                    Product_Line = Request.Form["Product_Line"],
                    Target_Count = long.TryParse(Request.Form["Target_Count"], out var target) ? target : 0,
                    Effective_Date = DateTime.TryParse(Request.Form["Effective_Date"], out var date) ? date : DateTime.Now,
                    Created_By = User.Identity?.Name ?? "Unknown",
                    Is_Active = Request.Form["Is_Active"] == "on",
                };

                ConnectionString = _configuration.GetConnectionString("DefaultConnection");

                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = @"
                    INSERT INTO production_target
                    (machine_line, machine_name, product_line, target_count, effective_date, is_active, created_by)
                    VALUES (@Machine_Line, @Machine_Name, @Product_Line, @Target_Count, @Effective_Date, @Is_Active, @Created_By)";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Machine_Line", (object?)TargetInfo.Machine_Line ?? DBNull.Value);
                command.Parameters.AddWithValue("@Machine_Name", (object?)TargetInfo.Machine_Name ?? DBNull.Value);
                command.Parameters.AddWithValue("@Product_Line", (object?)TargetInfo.Product_Line ?? DBNull.Value);
                command.Parameters.AddWithValue("@Target_Count", TargetInfo.Target_Count);
                command.Parameters.AddWithValue("@Effective_Date", TargetInfo.Effective_Date);
                command.Parameters.AddWithValue("@Created_By", TargetInfo.Created_By);
                command.Parameters.AddWithValue("@Is_Active", TargetInfo.Is_Active);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to insert production target: {ex.Message}";
                return Page();
            }

            SuccessMessage = "✅ Production target added successfully.";
            return RedirectToPage("/Index");
        }
    }

    public class ProductionTargetInfo
    {
        public long Id { get; set; }
        public string Machine_Line { get; set; }
        public string Machine_Name { get; set; }
        public string Product_Line { get; set; }
        public long Target_Count { get; set; }
        public DateTime Effective_Date { get; set; }
        public string Created_By { get; set; }
        public bool Is_Active { get; set; }   // ✅ NEW
    }
}

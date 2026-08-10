using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Material_ConsumptionModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public MaterialInfo MaterialInfo { get; set; } = new MaterialInfo();

        private readonly IConfiguration _configuration;
        public string ConnectionString { get; private set; }

        public List<MaterialInfo> MaterialList { get; set; } = new List<MaterialInfo>();
        public List<MaterialInfo> ConsumptionEntries { get; set; } = new();
        public long RunningTotalQuantity { get; set; }

        public Material_ConsumptionModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet(long? shiftId)
        {
            try
            {
                ConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");

                using (OdbcConnection connection = new OdbcConnection(ConnectionString))
                {
                    connection.Open();

                    string lastMonthDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd");

                        string sql = $@"
                    SELECT DISTINCT 
                        a.tranid, 
                        c.itemid, 
                        c.displayname
                    FROM 
                        transaction a
                    JOIN 
                        transactionline b ON a.id = b.transaction
                    JOIN 
                        item c ON b.item = c.id
                    WHERE 
                        RTRIM(a.recordtype) = 'workorder'
                        AND b.mainline = 'F'
                        AND b.itemtype != 'OthCharge'
                        AND b.item IS NOT NULL
                        AND a.createddate >= {{d '{lastMonthDate}'}}
                    ORDER BY 
                        a.tranid DESC";

                    using (OdbcCommand command = new OdbcCommand(sql, connection))
                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            MaterialList.Add(new MaterialInfo
                            {
                                WorkOrder = reader["tranid"].ToString(),
                                ItemId = reader["itemid"].ToString(),
                                DisplayName = reader["displayname"].ToString()
                            });
                        }
                    }
                }

                using (var pgConn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection")))
                {
                    pgConn.Open();

                    string sql = @"
                       SELECT id, wo_number, material_id, material_description,
                               batch_identifier,
                               quantity_consumed, created_by, created_at
                        FROM material_consumption
                        WHERE shift_id = @shiftId
                        ORDER BY created_at DESC";

                    using var cmd = new NpgsqlCommand(sql, pgConn);
                    cmd.Parameters.AddWithValue("@shiftId", shiftId ?? 0);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        ConsumptionEntries.Add(new MaterialInfo
                        {
                            Id = Convert.ToInt64(reader["id"]),   // ✅ MAP ID
                            Wo_Number = reader["wo_number"].ToString(),
                            Material_Id = reader["material_id"].ToString(),
                            Material_Description = reader["material_description"].ToString(),
                            Batch_Identifier = reader["batch_identifier"].ToString(), // ✅
                            Quantity_Consumed = Convert.ToInt64(reader["quantity_consumed"]),
                            Created_By = reader["created_by"].ToString(),
                            Created_At = reader["created_at"] as DateTime?
                        });
                    }
                    // ✅ RUNNING TOTAL
                    RunningTotalQuantity = ConsumptionEntries.Sum(x => x.Quantity_Consumed);
                }

                // 🟢 Autofill the Shift ID if passed in
                if (shiftId.HasValue)
                {
                    MaterialInfo.Shift_Id = shiftId.Value;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                MaterialInfo = new MaterialInfo
                {
                    Wo_Number = Request.Form["Wo_Number"],
                    Shift_Id = long.TryParse(Request.Form["Shift_Id"], out var sid) ? sid : 0,
                    Material_Id = Request.Form["Material_Id"],
                    Material_Description = Request.Form["Material_Description"],
                    Ribbon_Length = decimal.TryParse(Request.Form["Ribbon_Length"], out var rl) ? rl : 0,
                    Quantity_Consumed = long.TryParse(Request.Form["Quantity_Consumed"], out var qty) ? qty : 0,
                    Created_By = User.Identity?.Name ?? "Unknown"
                };
                

                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO material_consumption
                        (wo_number, shift_id, material_id, material_description,
                         batch_identifier, quantity_consumed, created_by)
                        VALUES
                        (@Wo_Number, @Shift_Id, @Material_Id, @Material_Description,
                         @Batch, @Quantity_Consumed, @Created_By)";

                    int noOfReels = int.TryParse(Request.Form["No_Of_Reels"], out var reels)
                     ? reels
                     : 1;

                    string batch =
                        Request.Form["Batch_Identifier"].ToString();

                    for (int i = 0; i < noOfReels; i++)
                    {
                        using (var command = new NpgsqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@Wo_Number", MaterialInfo.Wo_Number);

                            command.Parameters.AddWithValue("@Shift_Id",
                                MaterialInfo.Shift_Id);

                            command.Parameters.AddWithValue("@Material_Id",
                                MaterialInfo.Material_Id);

                            command.Parameters.AddWithValue("@Material_Description",
                                (object?)MaterialInfo.Material_Description ?? DBNull.Value);

                            command.Parameters.AddWithValue("@Batch",
                                (object?)batch ?? DBNull.Value);

                            // SINGLE REEL LENGTH
                            command.Parameters.AddWithValue("@Quantity_Consumed",
                                MaterialInfo.Quantity_Consumed);

                            command.Parameters.AddWithValue("@Created_By",
                                MaterialInfo.Created_By);

                            command.ExecuteNonQuery();
                        }
                    }
                }

                SuccessMessage = "✅ Material consumption recorded successfully.";
                TempData["MaterialSaved"] = true;

                return RedirectToPage(
                    "/Material_Consumption",
                    new
                    {
                        shiftId = MaterialInfo.Shift_Id,
                        woNumber = MaterialInfo.Wo_Number,
                        materialId = MaterialInfo.Material_Id,
                        ribbonLength = MaterialInfo.Ribbon_Length
                    });


            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to insert record: {ex.Message}";
                return Page();
            }
        }
    }
    public class MaterialInfo
    {
        public long Id { get; set; }   // ✅ ADD THIS
        public string Wo_Number { get; set; }
        public string Material_Id { get; set; }
        public string Material_Description { get; set; }
        public string Batch_Identifier { get; set; }   // ✅ NEW
        public decimal Ribbon_Length { get; set; }
        public long Shift_Id { get; set; }
        public long Quantity_Consumed { get; set; }
        public string Created_By { get; set; }
        public DateTime? Created_At { get; set; }

        // For loading dropdowns
        public string WorkOrder { get; set; }
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
    }

}

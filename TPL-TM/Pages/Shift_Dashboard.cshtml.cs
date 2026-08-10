using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using TPL_TM.Data;

namespace TPL_TM.Pages
{
    [Authorize]
    public class Shift_DashboardModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        public Shift_DashboardModel(
            IConfiguration configuration,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _context = context;
            _userManager = userManager;
        }

        public List<ShiftInfo> ActiveShifts { get; set; } = new();
        public int TotalMaterialsUsed { get; set; }
        public int TotalQualityChecked { get; set; }
        public decimal TotalWasteCollected { get; set; }
        public int TotalDowntimeMinutes { get; set; }  // ✅ REPLACED TotalActiveShifts
        public int TotalOperators { get; set; }
        public int TotalProductionCount { get; set; }
        // ✅ Get the latest quality check time
        public DateTime? LastQualityCheckedOn =>
        ActiveShifts
       .Select(s => s.LastQualityCheckTime)
       .Where(t => t.HasValue)
       .OrderByDescending(t => t)
       .FirstOrDefault();


        public async Task OnGetAsync()
        {
            try
            {
                var username = User.Identity?.Name;
                bool isSupervisor = User.IsInRole("Supervisor") || User.IsInRole("Admin");
                string shiftLetter = "N/A";
                // Get logged-in user's Identity ID
                var user = await _userManager.FindByNameAsync(username);
                if (user != null)
                {
                    var assignment = await _context.UserShiftAssignment
                        .Include(x => x.ShiftInformation)
                        .FirstOrDefaultAsync(x => x.UserId == user.Id);

                    shiftLetter = assignment?.ShiftInformation?.Name ?? "N/A";
                }

                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                // Step 1: Load active shifts
                string shiftQuery = @"
                    SELECT id, work_order_number, work_order_item, work_order_description,
                           machine_line, machine_name, product_line,
                           shift_start_time, shift_end_time,
                           shift_active, handover_rating,
                           created_by, authorized_by
                    FROM shift
                    WHERE shift_active = true";
                
                if (!isSupervisor)
                {
                    shiftQuery += " AND created_by = @username";
                }

                shiftQuery += " ORDER BY shift_start_time DESC";

                using var cmd = new NpgsqlCommand(shiftQuery, connection);
                if (!isSupervisor)
                {
                    cmd.Parameters.AddWithValue("@username", username);
                }

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    ActiveShifts.Add(new ShiftInfo
                    {
                        Shift_Id = reader["id"].ToString(),
                        WorkOrderNumber = reader["work_order_number"].ToString(),
                        WorkOrderItem = reader["work_order_item"].ToString(),
                        WorkOrderDescription = reader["work_order_description"].ToString(),
                        Machine_Line = reader["machine_line"].ToString(),
                        Machine_Name = reader["machine_name"].ToString(),
                        Product_Line = reader["product_line"].ToString(),
                        Shift_Start_Time = reader["shift_start_time"] as DateTime?,
                        Shift_End_Time = reader["shift_end_time"] as DateTime?,
                        Shift_Active = (bool)reader["shift_active"],
                        Shift_Letter = shiftLetter,
                        Handover_Rating = reader["handover_rating"] as int? ?? 0,
                        Created_By = reader["created_by"].ToString(),
                        Authorized_By = reader["authorized_by"].ToString()
                    });
                }
                reader.Close();

                if (ActiveShifts.Count > 0)
                {
                    string shiftIds = string.Join(",", ActiveShifts.Select(s => s.Shift_Id));

                    // Step 2: Load material consumption
                    string matQuery = $@"
                        SELECT shift_id, wo_number, material_id, material_description,
                               quantity_consumed, created_by, created_at
                        FROM material_consumption
                        WHERE shift_id IN ({shiftIds})
                        ORDER BY created_at DESC";

                    using var matCmd = new NpgsqlCommand(matQuery, connection);
                    using var matReader = matCmd.ExecuteReader();

                    while (matReader.Read())
                    {
                        var shift = ActiveShifts.FirstOrDefault(s => s.Shift_Id == matReader["shift_id"].ToString());
                        shift?.ConsumedMaterials.Add(new MaterialInfo
                        {
                            Wo_Number = matReader["wo_number"].ToString(),
                            Material_Id = matReader["material_id"].ToString(),
                            Material_Description = matReader["material_description"].ToString(),
                            Quantity_Consumed = Convert.ToInt64(matReader["quantity_consumed"]),
                            Created_By = matReader["created_by"].ToString(),
                            Created_At = matReader["created_at"] as DateTime?
                        });
                    }
                    matReader.Close();

                    // Step 3: Load quality check data
                    string qcQuery = $@"
                        SELECT qc.id,
                           qc.shift_id,
                           qct.test_name,
                           qct.product_category AS test_type,
                           qc.check_time,
                           qc.status,
                           qc.fail,
                           qc.weights,
                           qc.comment,
                           qc.created_by,
                           qc.created_on
                    FROM quality_check qc
                    JOIN quality_checks_template qct ON qc.test_id = qct.id
                    WHERE qc.shift_id IN ({shiftIds})
                    ORDER BY qc.shift_id, qct.test_name";

                    using var qcCmd = new NpgsqlCommand(qcQuery, connection);
                    using var qcReader = qcCmd.ExecuteReader();

                    while (qcReader.Read())
                    {
                        var shift = ActiveShifts.FirstOrDefault(s => s.Shift_Id == qcReader["shift_id"].ToString());
                        shift?.QualityChecks.Add(new QualityCheckInfo
                        {
                            Id = Convert.ToInt64(qcReader["id"]),
                            Shift_Id = Convert.ToInt64(qcReader["shift_id"]),
                            Test_Name = qcReader["test_name"].ToString(),
                            Test_Type = qcReader["test_type"].ToString(),
                            Check_Time = qcReader["check_time"] as TimeSpan?,
                            Status = qcReader["status"].ToString(),
                            Fail = qcReader["fail"] as bool? ?? false,
                            Weights = qcReader["weights"].ToString(),
                            Comment = qcReader["comment"].ToString(),
                            Created_By = qcReader["created_by"].ToString(),
                            Created_On = qcReader["created_on"] as DateTime?
                        });
                    }
                    qcReader.Close();

                    // Step 4: Load waste records
                    string wasteQuery = $@"
                        SELECT shift_id, waste_weight, waste_type, created_by, created_on
                        FROM waste
                        WHERE shift_id IN ({shiftIds})
                        ORDER BY created_on DESC";

                    using var wasteCmd = new NpgsqlCommand(wasteQuery, connection);
                    using var wasteReader = wasteCmd.ExecuteReader();

                    while (wasteReader.Read())
                    {
                        var shift = ActiveShifts.FirstOrDefault(s => s.Shift_Id == wasteReader["shift_id"].ToString());
                        shift?.WasteEntries.Add(new WasteInfo
                        {
                            Shift_Id = Convert.ToInt64(wasteReader["shift_id"]),
                            Waste_Weight = wasteReader["waste_weight"] as decimal?,
                            Waste_Type = wasteReader["waste_type"].ToString(),
                            Created_By = wasteReader["created_by"].ToString(),
                            Created_At = wasteReader["created_on"] as DateTime?
                        });
                    }
                    wasteReader.Close();

                    // ✅ Step 5: Load downtime records
                    string downtimeQuery = $@"
                        SELECT d.shift AS shift_id, d.downtime, dr.reason_name AS reason, 
                               d.created_by, d.created_on, d.comment
                        FROM downtime d
                        LEFT JOIN downtime_reason dr ON d.reason_id = dr.id
                        WHERE d.shift IN ({shiftIds})
                        ORDER BY d.created_on DESC";


                    using var downtimeCmd = new NpgsqlCommand(downtimeQuery, connection);
                    using var downtimeReader = downtimeCmd.ExecuteReader();

                    while (downtimeReader.Read())
                    {
                        var shift = ActiveShifts.FirstOrDefault(s => s.Shift_Id == downtimeReader["shift_id"].ToString());
                        shift?.DowntimeEntries.Add(new DowntimeInfo
                        {
                            Shift = Convert.ToInt64(downtimeReader["shift_id"]),
                            Downtime = Convert.ToInt32(downtimeReader["downtime"]),    // Already in minutes
                            Reason = downtimeReader["reason"].ToString(),
                            Created_By = downtimeReader["created_by"].ToString(),
                            Created_On = downtimeReader["created_on"] as DateTime?,
                            Comment = downtimeReader["comment"].ToString()
                        });

                    }
                    downtimeReader.Close();
                    // ✅ Step 6: Load production counts
                    string productionQuery = $@"
                        SELECT shift_id, wo_number, part_number, item_description, product_count, created_by, timestamp_start, timestamp_end, batch_identifier 
                        FROM production_count
                        WHERE shift_id IN ({shiftIds})
                        ORDER BY timestamp_start DESC";

                    using var productionCmd = new NpgsqlCommand(productionQuery, connection);
                    using var productionReader = productionCmd.ExecuteReader();

                    while (productionReader.Read())
                    {
                        var shift = ActiveShifts.FirstOrDefault(s => s.Shift_Id == productionReader["shift_id"].ToString());
                        shift?.ProductionEntries.Add(new ProductionInfo
                        {
                            Shift_Id = Convert.ToInt64(productionReader["shift_id"]),
                            Wo_Number = productionReader["wo_number"].ToString(),
                            Part_Number = productionReader["part_number"].ToString(),
                            Item_Description = productionReader["item_description"].ToString(),
                            Product_Count = Convert.ToInt32(productionReader["product_count"]),
                            Username = productionReader["created_by"].ToString(),
                            Timestamp_Start = productionReader["timestamp_start"] as DateTime?,
                            Timestamp_End = productionReader["timestamp_end"] as DateTime?,
                            Batch_Identifier = productionReader["batch_identifier"].ToString()
                        });
                    }
                    productionReader.Close();


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading active shifts: {ex.Message}");
            }

            // ✅ Summary Calculations
            TotalMaterialsUsed = ActiveShifts.Sum(s => s.ConsumedMaterials.Count);
            TotalQualityChecked = ActiveShifts.Sum(s =>
                s.QualityChecks
                 .GroupBy(qc => qc.Created_On?.ToString("yyyy-MM-dd HH:mm")) // group by check session time
                 .Count()
            );

            TotalWasteCollected = ActiveShifts.Sum(s => s.WasteEntries.Sum(w => w.Waste_Weight ?? 0));
            TotalDowntimeMinutes = ActiveShifts.Sum(s => s.DowntimeEntries.Sum(d => d.Downtime ?? 0));
            TotalOperators = ActiveShifts.Select(s => s.Created_By).Distinct().Count();
            TotalProductionCount = ActiveShifts.Sum(s => s.ProductionEntries.Sum(p => (int)p.Product_Count));
        }
    }
}

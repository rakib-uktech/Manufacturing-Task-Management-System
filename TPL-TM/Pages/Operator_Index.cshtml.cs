using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using TPL_TM.Data;

namespace TPL_TM.Pages
{
    [Authorize]
    public class Operator_IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public Operator_IndexModel(IConfiguration configuration, ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _context = context;
            _userManager = userManager;
        }

        public ShiftInfo CurrentShift { get; set; } = null;
        public bool ShiftActive => CurrentShift?.Shift_Active ?? false;
        public string ShiftStatus => ShiftActive ? "Active" : (CurrentShift == null ? "No Previous Shift" : "Completed");
        public DateTime? ShiftStartTime => CurrentShift?.Shift_Start_Time;
        public DateTime? ShiftEndTime => CurrentShift?.Shift_End_Time;

        public DateTime? LastQualityCheckedOn { get; set; }
        public int TotalQualityChecked { get; set; }
        public int TotalProduction { get; set; }
        public decimal TotalWasteCollected { get; set; }
        public int TotalDowntimeMinutes { get; set; }

        // ✅ Added property for Shift Letter
        public string ShiftLetter { get; set; } = "N/A";

        public void OnGet()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return;

                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                // ✅ Load Shift Letter for current logged-in user
                LoadLoggedInUserShiftLetterAsync().GetAwaiter().GetResult();

                // STEP 1: Check active shift
                string activeShiftQuery = @"SELECT id FROM shift WHERE shift_active = true AND created_by = @userName LIMIT 1";
                using var activeCmd = new NpgsqlCommand(activeShiftQuery, connection);
                activeCmd.Parameters.AddWithValue("userName", userName);
                var activeShiftExists = activeCmd.ExecuteScalar();

                if (activeShiftExists != null)
                {
                    Response.Redirect("/Shift_Dashboard");
                    return;
                }

                // STEP 2: Get last ended shift
                string lastShiftQuery = @"
                    SELECT id, machine_line, machine_name, product_line, shift_start_time,
                           shift_end_time, shift_active, handover_rating, shift_rating, created_by, authorized_by
                    FROM shift
                    WHERE shift_active = false AND created_by = @userName
                    ORDER BY shift_end_time DESC
                    LIMIT 1";

                using var lastShiftCmd = new NpgsqlCommand(lastShiftQuery, connection);
                lastShiftCmd.Parameters.AddWithValue("userName", userName);

                using var reader = lastShiftCmd.ExecuteReader();
                if (reader.Read())
                {
                    CurrentShift = new ShiftInfo
                    {
                        Shift_Id = reader["id"].ToString(),
                        Machine_Line = reader["machine_line"].ToString(),
                        Machine_Name = reader["machine_name"].ToString(),
                        Product_Line = reader["product_line"].ToString(),
                        Shift_Start_Time = reader["shift_start_time"] as DateTime?,
                        Shift_End_Time = reader["shift_end_time"] as DateTime?,
                        Shift_Active = (bool)reader["shift_active"],
                        Handover_Rating = reader["handover_rating"] as int? ?? 0,
                        Shift_Rating = reader["shift_rating"] as int? ?? 0,
                        Created_By = reader["created_by"].ToString(),
                        Authorized_By = reader["authorized_by"].ToString(),

                        QualityChecks = new List<QualityCheckInfo>(),
                        ProductionEntries = new List<ProductionInfo>(),
                        WasteEntries = new List<WasteInfo>(),
                        DowntimeEntries = new List<DowntimeInfo>()
                    };
                }
                reader.Close();

                if (CurrentShift == null)
                    return;

                long shiftId = Convert.ToInt64(CurrentShift.Shift_Id);

                // STEP 3: Load quality checks
                string qcQuery = @"
                    SELECT qc.id, qc.shift_id, qct.test_name, qct.product_category AS test_type,
                           qc.check_time, qc.status, qc.fail, qc.weights, qc.comment,
                           qc.created_by, qc.created_on
                    FROM quality_check qc
                    JOIN quality_checks_template qct ON qc.test_id = qct.id
                    WHERE qc.shift_id = @shiftId
                    ORDER BY qc.shift_id, qct.test_name";

                using var qcCmd = new NpgsqlCommand(qcQuery, connection);
                qcCmd.Parameters.AddWithValue("shiftId", shiftId);
                using var qcReader = qcCmd.ExecuteReader();
                while (qcReader.Read())
                {
                    CurrentShift.QualityChecks.Add(new QualityCheckInfo
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

                // STEP 4: Load production entries
                string prodQuery = @"
                    SELECT shift_id, wo_number, part_number, item_description, product_count, created_by, timestamp_start, timestamp_end
                    FROM production_count
                    WHERE shift_id = @shiftId
                    ORDER BY timestamp_start DESC";
                using var prodCmd = new NpgsqlCommand(prodQuery, connection);
                prodCmd.Parameters.AddWithValue("shiftId", shiftId);
                using var prodReader = prodCmd.ExecuteReader();
                while (prodReader.Read())
                {
                    CurrentShift.ProductionEntries.Add(new ProductionInfo
                    {
                        Shift_Id = Convert.ToInt64(prodReader["shift_id"]),
                        Wo_Number = prodReader["wo_number"].ToString(),
                        Part_Number = prodReader["part_number"].ToString(),
                        Item_Description = prodReader["item_description"].ToString(),
                        Product_Count = Convert.ToInt64(prodReader["product_count"]),
                        Username = prodReader["created_by"].ToString(),
                        Timestamp_Start = prodReader["timestamp_start"] as DateTime?,
                        Timestamp_End = prodReader["timestamp_end"] as DateTime?
                    });
                }
                prodReader.Close();

                // STEP 5: Load waste entries
                string wasteQuery = @"
                    SELECT shift_id, waste_weight, waste_type, created_by, created_on
                    FROM waste
                    WHERE shift_id = @shiftId
                    ORDER BY created_on DESC";
                using var wasteCmd = new NpgsqlCommand(wasteQuery, connection);
                wasteCmd.Parameters.AddWithValue("shiftId", shiftId);
                using var wasteReader = wasteCmd.ExecuteReader();
                while (wasteReader.Read())
                {
                    CurrentShift.WasteEntries.Add(new WasteInfo
                    {
                        Shift_Id = Convert.ToInt64(wasteReader["shift_id"]),
                        Waste_Weight = wasteReader["waste_weight"] as decimal?,
                        Waste_Type = wasteReader["waste_type"].ToString(),
                        Created_By = wasteReader["created_by"].ToString(),
                        Created_At = wasteReader["created_on"] as DateTime?
                    });
                }
                wasteReader.Close();

                // STEP 6: Load downtime entries
                string downtimeQuery = @"
                    SELECT d.shift AS shift_id, d.downtime, dr.reason_name AS reason, 
                           d.created_by, d.created_on, d.comment
                    FROM downtime d
                    LEFT JOIN downtime_reason dr ON d.reason_id = dr.id
                    WHERE d.shift = @shiftId
                    ORDER BY d.created_on DESC";
                using var downtimeCmd = new NpgsqlCommand(downtimeQuery, connection);
                downtimeCmd.Parameters.AddWithValue("shiftId", shiftId);
                using var downtimeReader = downtimeCmd.ExecuteReader();
                while (downtimeReader.Read())
                {
                    CurrentShift.DowntimeEntries.Add(new DowntimeInfo
                    {
                        Shift = Convert.ToInt64(downtimeReader["shift_id"]),
                        Downtime = Convert.ToInt32(downtimeReader["downtime"]),
                        Reason = downtimeReader["reason"].ToString(),
                        Created_By = downtimeReader["created_by"].ToString(),
                        Created_On = downtimeReader["created_on"] as DateTime?,
                        Comment = downtimeReader["comment"].ToString()
                    });
                }
                downtimeReader.Close();

                // STEP 7: Calculate totals
                TotalQualityChecked = CurrentShift.QualityChecks
                    .GroupBy(q => q.Created_On?.ToString("yyyy-MM-dd HH:mm"))
                    .Count();

                TotalProduction = (int)CurrentShift.ProductionEntries.Sum(p => p.Product_Count);
                TotalWasteCollected = CurrentShift.WasteEntries.Sum(w => w.Waste_Weight ?? 0);
                TotalDowntimeMinutes = CurrentShift.DowntimeEntries.Sum(d => d.Downtime ?? 0);
                LastQualityCheckedOn = CurrentShift.QualityChecks
                    .OrderByDescending(q => q.Created_On)
                    .Select(q => q.Created_On)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading operator dashboard: {ex.Message}");
            }
        }

        // ✅ New method to get Shift_Letter for current logged-in user
        private async Task LoadLoggedInUserShiftLetterAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                ShiftLetter = "N/A";
                return;
            }

            var userShift = await _context.UserShiftAssignment
                .Include(x => x.ShiftInformation)
                .FirstOrDefaultAsync(x => x.UserId == currentUser.Id);

            if (userShift != null)
                ShiftLetter = userShift.ShiftInformation.Name;
            else
                ShiftLetter = "N/A";
        }

        public IActionResult OnPostStartShift()
        {
            // Redirect to Shift Entry page to start new shift
            return RedirectToPage("/Shift_Entry");
        }
    }
}
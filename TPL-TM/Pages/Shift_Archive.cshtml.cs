using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TPL_TM.Pages
{
    [Authorize]
    public class Shift_ArchiveModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_ArchiveModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<ShiftInfo> ArchivedShifts { get; set; } = new();

        public int TotalMaterialsUsed { get; set; }
        public int TotalQualityChecked { get; set; }
        public decimal TotalWasteCollected { get; set; }
        public int TotalDowntimeMinutes { get; set; }
        public int TotalOperators { get; set; }
        public int TotalProductionCount { get; set; }

        public DateTime? LastQualityCheckedOn =>
            ArchivedShifts
                .Select(s => s.LastQualityCheckTime)
                .Where(t => t.HasValue)
                .OrderByDescending(t => t)
                .FirstOrDefault();

        public void OnGet()
        {
            try
            {
                var username = User.Identity?.Name;
                bool isSupervisor = User.IsInRole("Supervisor") || User.IsInRole("Admin");

                using var connection = new NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                // 🔹 STEP 1: Load archived (inactive) shifts
                string shiftQuery = @"
                    SELECT id, work_order_number, work_order_item, work_order_description,
                           machine_line, machine_name, product_line,
                           shift_start_time, shift_end_time,
                           shift_active, handover_rating,
                           created_by, authorized_by
                    FROM shift
                    WHERE shift_active = false";

                if (!isSupervisor)
                    shiftQuery += " AND created_by = @username";

                shiftQuery += " ORDER BY shift_end_time DESC";

                using var cmd = new NpgsqlCommand(shiftQuery, connection);
                if (!isSupervisor)
                    cmd.Parameters.AddWithValue("@username", username);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    ArchivedShifts.Add(new ShiftInfo
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
                        Handover_Rating = reader["handover_rating"] as int? ?? 0,
                        Created_By = reader["created_by"].ToString(),
                        Authorized_By = reader["authorized_by"].ToString()
                    });
                }
                reader.Close();

                if (!ArchivedShifts.Any())
                    return;

                string shiftIds = string.Join(",", ArchivedShifts.Select(s => s.Shift_Id));

                // 🔹 MATERIAL CONSUMPTION
                LoadMaterials(connection, shiftIds);

                // 🔹 QUALITY CHECKS
                LoadQualityChecks(connection, shiftIds);

                // 🔹 WASTE
                LoadWaste(connection, shiftIds);

                // 🔹 DOWNTIME
                LoadDowntime(connection, shiftIds);

                // 🔹 PRODUCTION
                LoadProduction(connection, shiftIds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading archived shifts: {ex.Message}");
            }

            // 🔹 SUMMARY TOTALS
            TotalMaterialsUsed = ArchivedShifts.Sum(s => s.ConsumedMaterials.Count);

            TotalQualityChecked = ArchivedShifts.Sum(s =>
                s.QualityChecks
                 .GroupBy(qc => qc.Created_On?.ToString("yyyy-MM-dd HH:mm"))
                 .Count());

            TotalWasteCollected =
                ArchivedShifts.Sum(s => s.WasteEntries.Sum(w => w.Waste_Weight ?? 0));

            TotalDowntimeMinutes =
                ArchivedShifts.Sum(s => s.DowntimeEntries.Sum(d => d.Downtime ?? 0));

            TotalOperators =
                ArchivedShifts.Select(s => s.Created_By).Distinct().Count();

            TotalProductionCount =
                ArchivedShifts.Sum(s => s.ProductionEntries.Sum(p => (int)p.Product_Count));
        }

        // ================= HELPER LOADERS =================

        private void LoadMaterials(NpgsqlConnection connection, string shiftIds)
        {
            string query = $@"
                SELECT shift_id, wo_number, material_id, material_description,
                       quantity_consumed, created_by, created_at
                FROM material_consumption
                WHERE shift_id IN ({shiftIds})";

            using var cmd = new NpgsqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var shift = ArchivedShifts.First(s => s.Shift_Id == reader["shift_id"].ToString());
                shift.ConsumedMaterials.Add(new MaterialInfo
                {
                    Wo_Number = reader["wo_number"].ToString(),
                    Material_Id = reader["material_id"].ToString(),
                    Material_Description = reader["material_description"].ToString(),
                    Quantity_Consumed = Convert.ToInt64(reader["quantity_consumed"]),
                    Created_By = reader["created_by"].ToString(),
                    Created_At = reader["created_at"] as DateTime?
                });
            }
        }

        private void LoadQualityChecks(NpgsqlConnection connection, string shiftIds)
        {
            string query = $@"
                SELECT qc.id, qc.shift_id, qct.test_name, qct.product_category,
                       qc.check_time, qc.status, qc.fail, qc.weights,
                       qc.comment, qc.created_by, qc.created_on
                FROM quality_check qc
                JOIN quality_checks_template qct ON qc.test_id = qct.id
                WHERE qc.shift_id IN ({shiftIds})";

            using var cmd = new NpgsqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var shift = ArchivedShifts.First(s => s.Shift_Id == reader["shift_id"].ToString());
                shift.QualityChecks.Add(new QualityCheckInfo
                {
                    Id = Convert.ToInt64(reader["id"]),
                    Shift_Id = Convert.ToInt64(reader["shift_id"]),
                    Test_Name = reader["test_name"].ToString(),
                    Test_Type = reader["product_category"].ToString(),
                    Check_Time = reader["check_time"] as TimeSpan?,
                    Status = reader["status"].ToString(),
                    Fail = reader["fail"] as bool? ?? false,
                    Weights = reader["weights"].ToString(),
                    Comment = reader["comment"].ToString(),
                    Created_By = reader["created_by"].ToString(),
                    Created_On = reader["created_on"] as DateTime?
                });
            }
        }

        private void LoadWaste(NpgsqlConnection connection, string shiftIds)
        {
            string query = $@"
                SELECT shift_id, waste_weight, waste_type, created_by, created_on
                FROM waste
                WHERE shift_id IN ({shiftIds})";

            using var cmd = new NpgsqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var shift = ArchivedShifts.First(s => s.Shift_Id == reader["shift_id"].ToString());
                shift.WasteEntries.Add(new WasteInfo
                {
                    Shift_Id = Convert.ToInt64(reader["shift_id"]),
                    Waste_Weight = reader["waste_weight"] as decimal?,
                    Waste_Type = reader["waste_type"].ToString(),
                    Created_By = reader["created_by"].ToString(),
                    Created_At = reader["created_on"] as DateTime?
                });
            }
        }

        private void LoadDowntime(NpgsqlConnection connection, string shiftIds)
        {
            string query = $@"
                SELECT d.shift AS shift_id, d.downtime, dr.reason_name,
                       d.created_by, d.created_on, d.comment
                FROM downtime d
                LEFT JOIN downtime_reason dr ON d.reason_id = dr.id
                WHERE d.shift IN ({shiftIds})";

            using var cmd = new NpgsqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var shift = ArchivedShifts.First(s => s.Shift_Id == reader["shift_id"].ToString());
                shift.DowntimeEntries.Add(new DowntimeInfo
                {
                    Shift = Convert.ToInt64(reader["shift_id"]),
                    Downtime = Convert.ToInt32(reader["downtime"]),
                    Reason = reader["reason_name"].ToString(),
                    Created_By = reader["created_by"].ToString(),
                    Created_On = reader["created_on"] as DateTime?,
                    Comment = reader["comment"].ToString()
                });
            }
        }

        private void LoadProduction(NpgsqlConnection connection, string shiftIds)
        {
            string query = $@"
                SELECT shift_id, wo_number, part_number, item_description,
                       product_count, created_by,
                       timestamp_start, timestamp_end, batch_identifier
                FROM production_count
                WHERE shift_id IN ({shiftIds})";

            using var cmd = new NpgsqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var shift = ArchivedShifts.First(s => s.Shift_Id == reader["shift_id"].ToString());
                shift.ProductionEntries.Add(new ProductionInfo
                {
                    Shift_Id = Convert.ToInt64(reader["shift_id"]),
                    Wo_Number = reader["wo_number"].ToString(),
                    Part_Number = reader["part_number"].ToString(),
                    Item_Description = reader["item_description"].ToString(),
                    Product_Count = Convert.ToInt32(reader["product_count"]),
                    Username = reader["created_by"].ToString(),
                    Timestamp_Start = reader["timestamp_start"] as DateTime?,
                    Timestamp_End = reader["timestamp_end"] as DateTime?,
                    Batch_Identifier = reader["batch_identifier"].ToString()
                });
            }
        }
    }
}
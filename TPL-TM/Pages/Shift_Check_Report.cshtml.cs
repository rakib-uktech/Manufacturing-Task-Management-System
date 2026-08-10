using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_Check_ReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_Check_ReportModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            // initial page load - no data
        }

        // 🔹 Server-side AJAX for DataTables
        public JsonResult OnGetLoadData(int draw, int start, int length, string? search, string? range, DateTime? fromDate, DateTime? toDate)
        {
            var data = new List<ShiftCheckRow>();
            int totalRecords = 0;

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT 
                        s.id AS shift_id,
                        s.machine_line,
                        s.machine_name,
                        s.product_line,
                        s.handover_rating,
                        s.shift_active,
                        s.shift_start_time,
                        s.shift_end_time,
                        s.created_by AS shift_created_by,
                        s.authorized_by,
                        s.comment AS shift_comment,
                        s.created_on AS shift_created_on,
                        
                        sc.id AS shiftcheck_id,
                        sc.check_name,
                        sc.check_status,
                        sc.comment AS check_comment,
                        sc.created_by AS check_created_by
                    FROM shift s
                    LEFT JOIN shift_checks sc ON s.id = sc.shift_id
                    WHERE 1=1
                ";

                var filters = new List<string>();
                var cmd = new NpgsqlCommand();
                cmd.Connection = conn;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    filters.Add("(sc.check_name ILIKE @search OR sc.comment ILIKE @search OR sc.created_by ILIKE @search OR s.machine_name ILIKE @search)");
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");
                }

                // Date filter
                if (range == "7")
                {
                    filters.Add("sc.created_on >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-7));
                }
                else if (range == "30")
                {
                    filters.Add("sc.created_on >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-30));
                }
                else if (range == "custom" && fromDate.HasValue && toDate.HasValue)
                {
                    filters.Add("sc.created_on >= @fromDate AND sc.created_on <= @toDate");
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Value.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Value.Date.AddDays(1).AddTicks(-1));
                }

                if (filters.Count > 0)
                    sql += " AND " + string.Join(" AND ", filters);

                // Count total records
                string countSql = "SELECT COUNT(*) FROM (" + sql + ") AS count_table";
                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    foreach (NpgsqlParameter p in cmd.Parameters)
                        countCmd.Parameters.Add(p.Clone());
                    totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                // Add ordering + paging
                sql += " ORDER BY s.id DESC OFFSET @start LIMIT @length";
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@length", length);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    data.Add(new ShiftCheckRow
                    {
                        Id = reader.IsDBNull(reader.GetOrdinal("shiftcheck_id")) ? 0 : reader.GetInt64(reader.GetOrdinal("shiftcheck_id")),
                        ShiftId = reader.GetInt64(reader.GetOrdinal("shift_id")),
                        MachineLine = reader["machine_line"]?.ToString() ?? "",
                        MachineName = reader["machine_name"]?.ToString() ?? "",
                        ProductLine = reader["product_line"]?.ToString() ?? "",
                        HandoverRating = reader.IsDBNull(reader.GetOrdinal("handover_rating")) ? null : reader.GetInt32(reader.GetOrdinal("handover_rating")),
                        ShiftActive = !reader.IsDBNull(reader.GetOrdinal("shift_active")) && reader.GetBoolean(reader.GetOrdinal("shift_active")),
                        ShiftStartTime = reader.IsDBNull(reader.GetOrdinal("shift_start_time")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_start_time")),
                        ShiftEndTime = reader.IsDBNull(reader.GetOrdinal("shift_end_time")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_end_time")),
                        ShiftCreatedBy = reader["shift_created_by"]?.ToString() ?? "",
                        AuthorizedBy = reader["authorized_by"]?.ToString() ?? "",
                        ShiftComment = reader["shift_comment"]?.ToString() ?? "",
                        ShiftCreatedOn = reader.IsDBNull(reader.GetOrdinal("shift_created_on")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_created_on")),

                        CheckName = reader["check_name"]?.ToString() ?? "",
                        CheckStatus = !reader.IsDBNull(reader.GetOrdinal("check_status")) && reader.GetBoolean(reader.GetOrdinal("check_status")),
                        CheckComment = reader["check_comment"]?.ToString() ?? "",
                        CheckCreatedBy = reader["check_created_by"]?.ToString() ?? "",
                    });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>(), error = ex.Message });
            }

            return new JsonResult(new
            {
                draw = draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = data
            });
        }

        // DTO
        public class ShiftCheckRow
        {
            public long Id { get; set; }
            public long ShiftId { get; set; }
            public string MachineLine { get; set; } = "";
            public string MachineName { get; set; } = "";
            public string ProductLine { get; set; } = "";
            public int? HandoverRating { get; set; }
            public bool ShiftActive { get; set; }
            public DateTime? ShiftStartTime { get; set; }
            public DateTime? ShiftEndTime { get; set; }
            public string ShiftCreatedBy { get; set; } = "";
            public string AuthorizedBy { get; set; } = "";
            public string ShiftComment { get; set; } = "";
            public DateTime? ShiftCreatedOn { get; set; }

            public string CheckName { get; set; } = "";
            public bool CheckStatus { get; set; }
            public string CheckComment { get; set; } = "";
            public string CheckCreatedBy { get; set; } = "";
        }
    }
}
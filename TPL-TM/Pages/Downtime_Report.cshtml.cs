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
    public class Downtime_ReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Downtime_ReportModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            // Default page load - no data needed
        }

        // 🔹 Server-side DataTables endpoint
        public JsonResult OnGetLoadData(int draw, int start, int length, string? search, string? range, DateTime? fromDate, DateTime? toDate)
        {
            var data = new List<DowntimeItem>();
            int totalRecords = 0;

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                // 🔹 Base SQL
                string sql = @"
                    SELECT 
                        d.id, d.downtime, r.reason_name, d.comment, d.created_by, d.created_on, d.shift,
                        s.id AS shift_id_full, s.machine_line, s.machine_name, s.product_line
                    FROM downtime d
                    LEFT JOIN downtime_reason r ON d.reason_id = r.id
                    LEFT JOIN shift s ON d.shift = s.id
                    WHERE 1=1
                ";

                // 🔹 Filters
                var filters = new List<string>();
                var cmd = new NpgsqlCommand();
                cmd.Connection = conn;

                if (!string.IsNullOrEmpty(search))
                {
                    filters.Add("(r.reason_name ILIKE @search OR d.comment ILIKE @search OR d.created_by ILIKE @search)");
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");
                }

                if (range == "7")
                {
                    filters.Add("d.created_on >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-7));
                }
                else if (range == "30")
                {
                    filters.Add("d.created_on >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-30));
                }
                else if (range == "custom" && fromDate.HasValue && toDate.HasValue)
                {
                    filters.Add("d.created_on >= @fromDate AND d.created_on <= @toDate");
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Value.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Value.Date.AddDays(1).AddTicks(-1));
                }

                if (filters.Count > 0)
                {
                    sql += " AND " + string.Join(" AND ", filters);
                }

                // 🔹 Count total
                string countSql = "SELECT COUNT(*) FROM (" + sql + ") AS count_table";
                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    foreach (NpgsqlParameter p in cmd.Parameters)
                        countCmd.Parameters.Add(p.Clone());
                    totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                // 🔹 Add ordering and pagination
                sql += " ORDER BY d.created_on DESC OFFSET @start LIMIT @length";
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@length", length);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    data.Add(new DowntimeItem
                    {
                        Id = reader.GetInt64(reader.GetOrdinal("id")),
                        Downtime = reader.IsDBNull(reader.GetOrdinal("downtime")) ? null : reader.GetInt32(reader.GetOrdinal("downtime")),
                        Reason = reader.IsDBNull(reader.GetOrdinal("reason_name")) ? "" : reader.GetString(reader.GetOrdinal("reason_name")),
                        Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? "" : reader.GetString(reader.GetOrdinal("comment")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? "" : reader.GetString(reader.GetOrdinal("created_by")),
                        CreatedOn = reader.GetDateTime(reader.GetOrdinal("created_on")).ToString("yyyy-MM-dd HH:mm:ss"),
                        ShiftDetails = new ShiftInfo
                        {
                            Id = reader.IsDBNull(reader.GetOrdinal("shift_id_full")) ? 0 : reader.GetInt64(reader.GetOrdinal("shift_id_full")),
                            MachineLine = reader["machine_line"]?.ToString() ?? "",
                            MachineName = reader["machine_name"]?.ToString() ?? "",
                            ProductLine = reader["product_line"]?.ToString() ?? ""
                        }
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

        // 🔹 DTOs for JSON
        public class DowntimeItem
        {
            public long Id { get; set; }
            public int? Downtime { get; set; }
            public string Reason { get; set; } = "";
            public string Comment { get; set; } = "";
            public string CreatedBy { get; set; } = "";
            public string CreatedOn { get; set; } = "";
            public ShiftInfo ShiftDetails { get; set; } = new();
        }

        public class ShiftInfo
        {
            public long Id { get; set; }
            public string MachineLine { get; set; } = "";
            public string MachineName { get; set; } = "";
            public string ProductLine { get; set; } = "";
        }
    }
}
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
    public class Waste_RecordsReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Waste_RecordsReportModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet() { }

        // 🔹 Server-side DataTable AJAX
        public JsonResult OnGetLoadData(int draw, int start, int length, string? search, string? range, DateTime? fromDate, DateTime? toDate)
        {
            var data = new List<WasteRow>();
            int totalRecords = 0;

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT 
                        w.id, w.shift_id, w.waste_type, w.waste_weight, w.created_by, w.created_on,
                        s.id AS shift_id_full, s.machine_line, s.machine_name, s.product_line
                    FROM waste w
                    LEFT JOIN shift s ON w.shift_id = s.id
                    WHERE 1=1
                ";

                var filters = new List<string>();
                var cmd = new NpgsqlCommand();
                cmd.Connection = conn;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    filters.Add("(w.waste_type ILIKE @search OR w.created_by ILIKE @search OR s.machine_name ILIKE @search OR s.product_line ILIKE @search)");
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");
                }

                // Date range filter
                if (range == "7") filters.Add("w.created_on >= @fromDate");
                else if (range == "30") filters.Add("w.created_on >= @fromDate");
                else if (range == "custom" && fromDate.HasValue && toDate.HasValue)
                    filters.Add("w.created_on >= @fromDate AND w.created_on <= @toDate");

                if (filters.Count > 0) sql += " AND " + string.Join(" AND ", filters);

                // Add parameters for date
                if (range == "7") cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-7));
                else if (range == "30") cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-30));
                else if (range == "custom" && fromDate.HasValue && toDate.HasValue)
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Value.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Value.Date.AddDays(1).AddTicks(-1));
                }

                // Total count
                using (var countCmd = new NpgsqlCommand("SELECT COUNT(*) FROM (" + sql + ") AS count_table", conn))
                {
                    foreach (NpgsqlParameter p in cmd.Parameters) countCmd.Parameters.Add(p.Clone());
                    totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                // Paging
                sql += " ORDER BY w.id DESC OFFSET @start LIMIT @length";
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@length", length);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    data.Add(new WasteRow
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        WasteType = reader["waste_type"]?.ToString() ?? "",
                        WasteWeight = reader.IsDBNull(reader.GetOrdinal("waste_weight")) ? null : reader.GetDecimal(reader.GetOrdinal("waste_weight")),
                        CreatedBy = reader["created_by"]?.ToString() ?? "",
                        CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_on")) ? null : reader.GetDateTime(reader.GetOrdinal("created_on")),
                        ShiftId = reader.IsDBNull(reader.GetOrdinal("shift_id_full")) ? 0 : reader.GetInt64(reader.GetOrdinal("shift_id_full")),
                        MachineLine = reader["machine_line"]?.ToString() ?? "",
                        MachineName = reader["machine_name"]?.ToString() ?? "",
                        ProductLine = reader["product_line"]?.ToString() ?? ""
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

        public class WasteRow
        {
            public int Id { get; set; }
            public string WasteType { get; set; } = "";
            public decimal? WasteWeight { get; set; }
            public string CreatedBy { get; set; } = "";
            public DateTime? CreatedAt { get; set; }
            public long ShiftId { get; set; }
            public string MachineLine { get; set; } = "";
            public string MachineName { get; set; } = "";
            public string ProductLine { get; set; } = "";
        }
    }
}
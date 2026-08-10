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
    public class Material_Consumption_ReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Material_Consumption_ReportModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            // Page load - no data
        }

        // 🔹 Server-side DataTables AJAX
        public JsonResult OnGetLoadData(int draw, int start, int length, string? search, string? range, DateTime? fromDate, DateTime? toDate)
        {
            var data = new List<MaterialItem>();
            int totalRecords = 0;

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, wo_number, shift_id, material_id, material_description,
                           quantity_consumed, created_by, created_at
                    FROM material_consumption
                    WHERE 1=1
                ";

                var filters = new List<string>();
                var cmd = new NpgsqlCommand();
                cmd.Connection = conn;

                if (!string.IsNullOrEmpty(search))
                {
                    filters.Add("(wo_number ILIKE @search OR material_id ILIKE @search OR material_description ILIKE @search OR created_by ILIKE @search)");
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");
                }

                if (range == "7")
                {
                    filters.Add("created_at >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-7));
                }
                else if (range == "30")
                {
                    filters.Add("created_at >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", DateTime.UtcNow.AddDays(-30));
                }
                else if (range == "custom" && fromDate.HasValue && toDate.HasValue)
                {
                    filters.Add("created_at >= @fromDate AND created_at <= @toDate");
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Value.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Value.Date.AddDays(1).AddTicks(-1));
                }

                if (filters.Count > 0)
                    sql += " AND " + string.Join(" AND ", filters);

                // 🔹 Count total records
                string countSql = "SELECT COUNT(*) FROM (" + sql + ") AS count_table";
                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    foreach (NpgsqlParameter p in cmd.Parameters)
                        countCmd.Parameters.Add(p.Clone());
                    totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                // 🔹 Add ordering + paging
                sql += " ORDER BY created_at DESC OFFSET @start LIMIT @length";
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@length", length);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    data.Add(new MaterialItem
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        WoNumber = reader["wo_number"]?.ToString() ?? "",
                        ShiftId = reader.IsDBNull(reader.GetOrdinal("shift_id")) ? 0 : reader.GetInt64(reader.GetOrdinal("shift_id")),
                        MaterialId = reader["material_id"]?.ToString() ?? "",
                        MaterialDescription = reader["material_description"]?.ToString() ?? "",
                        QuantityConsumed = reader.IsDBNull(reader.GetOrdinal("quantity_consumed")) ? 0 : reader.GetInt64(reader.GetOrdinal("quantity_consumed")),
                        CreatedBy = reader["created_by"]?.ToString() ?? "",
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd HH:mm")
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

        // 🔹 DTOs
        public class MaterialItem
        {
            public int Id { get; set; }
            public string WoNumber { get; set; } = "";
            public long ShiftId { get; set; }
            public string MaterialId { get; set; } = "";
            public string MaterialDescription { get; set; } = "";
            public long QuantityConsumed { get; set; }
            public string CreatedBy { get; set; } = "";
            public string CreatedAt { get; set; } = "";
        }
    }
}
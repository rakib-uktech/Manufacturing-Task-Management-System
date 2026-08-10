using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_Continental_SummaryModel : PageModel
    {
        private readonly IConfiguration _config;

        public Production_Continental_SummaryModel(IConfiguration config)
        {
            _config = config;
        }

        // =========================================
        // 🔹 COMMON FILTER BUILDER
        // =========================================
        private (string sql, List<NpgsqlParameter> parameters) BuildFilters(
            string search, string range,
            string fromDate, string toDate,
            string machineLine, string machineName)
        {
            var where = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add(@"(
                    wo_number ILIKE @search OR
                    machine_name ILIKE @search OR
                    machine_line ILIKE @search
                )");
                parameters.Add(new NpgsqlParameter("@search", $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(machineLine))
            {
                where.Add("machine_line = @machineLine");
                parameters.Add(new NpgsqlParameter("@machineLine", machineLine));
            }

            if (!string.IsNullOrWhiteSpace(machineName))
            {
                where.Add("machine_name = @machineName");
                parameters.Add(new NpgsqlParameter("@machineName", machineName));
            }

            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (string.IsNullOrEmpty(range) || range == "1")
            {
                var productionDay = GetProductionDayRange();

                minDate = productionDay.Start;
                maxDate = productionDay.End;
            }
            else if (range == "7")
            {
                var productionDay = GetProductionDayRange();

                minDate = productionDay.Start.AddDays(-6);
                maxDate = productionDay.End;
            }
            else if (range == "30")
            {
                var productionDay = GetProductionDayRange();

                minDate = productionDay.Start.AddDays(-29);
                maxDate = productionDay.End;
            }
            else if (range == "custom")
            {
                if (DateTime.TryParse(fromDate, out var f))
                    minDate = f.AddDays(-1).Date.AddHours(22);

                if (DateTime.TryParse(toDate, out var t))
                    maxDate = t.Date.AddHours(22);
            }

            if (minDate.HasValue)
            {
                where.Add("timestamp_start >= @minDate");
                parameters.Add(new NpgsqlParameter("@minDate", minDate.Value));
            }

            if (maxDate.HasValue)
            {
                where.Add("timestamp_start < @maxDate");
                parameters.Add(new NpgsqlParameter("@maxDate", maxDate.Value));
            }

            return (
                where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "",
                parameters
            );
        }

        // =========================================
        // 🔷 SUMMARY API (SQL ONLY)
        // =========================================
        public async Task<IActionResult> OnGetSummaryAsync(
            string search, string range,
            string fromDate, string toDate,
            string machineLine, string machineName)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var (whereSql, parameters) = BuildFilters(search, range, fromDate, toDate, machineLine, machineName);

            var sql = $@"
                SELECT
                    shift_letter,
                    machine_line,
                    machine_name,
                    wo_number,
                    part_number,
                    SUM(product_count) AS qty
                FROM production_count
                {whereSql}
                GROUP BY shift_letter, machine_line, machine_name, wo_number, part_number
                ORDER BY shift_letter, machine_line, machine_name;
            ";

            using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var p in parameters)
                cmd.Parameters.Add(new NpgsqlParameter(p.ParameterName, p.Value));

            var result = new List<object>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    shift = reader.GetString(0),
                    machineLine = reader.GetString(1),
                    machineName = reader.GetString(2),
                    wo = reader.GetString(3),
                    part = reader.GetString(4),
                    qty = reader.GetInt64(5)
                });
            }

            return new JsonResult(result);
        }

        // =========================================
        // 🔷 DATATABLE API (FIXED 🚀)
        // =========================================
        public async Task<IActionResult> OnGetLoadDataAsync(
    int? draw, int? start, int? length,
    string search, string range,
    string fromDate, string toDate,
    string machineLine, string machineName)
        {
            int _draw = draw ?? 1;
            int _start = start ?? 0;
            int _length = length ?? 10;

            using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var (whereSql, parameters) = BuildFilters(search, range, fromDate, toDate, machineLine, machineName);

            var dataSql = $@"
        SELECT id, machine_line, machine_name, wo_number, part_number, batch_identifier,
               shift_letter, timestamp_start, timestamp_end,
               product_count, created_by, created_at
        FROM production_count
        {whereSql}
        ORDER BY timestamp_start DESC
        OFFSET @start LIMIT @length;
    ";

            using var dataCmd = new NpgsqlCommand(dataSql, conn);

            foreach (var p in parameters)
                dataCmd.Parameters.AddWithValue(p.ParameterName, p.Value ?? DBNull.Value);

            dataCmd.Parameters.AddWithValue("@start", _start);
            dataCmd.Parameters.AddWithValue("@length", _length);

            var rows = new List<object>();

            using (var reader = await dataCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    rows.Add(new
                    {
                        id = reader.GetInt64(0),
                        machineLine = reader.GetString(1),
                        machineName = reader.GetString(2),
                        wo = reader.GetString(3),
                        part = reader.GetString(4),
                        batch = reader.GetString(5),
                        shift = reader.GetString(6),
                        start = reader.IsDBNull(7) ? "" : reader.GetDateTime(7).ToString("yyyy-MM-dd HH:mm"),
                        end = reader.IsDBNull(8) ? "" : reader.GetDateTime(8).ToString("yyyy-MM-dd HH:mm"),
                        qty = reader.GetInt64(9),
                        createdBy = reader.GetString(10),
                        createdAt = reader.GetDateTime(11).ToString("yyyy-MM-dd HH:mm")
                    });
                }
            }

            var countSql = $"SELECT COUNT(*) FROM production_count {whereSql}";
            using var countCmd = new NpgsqlCommand(countSql, conn);

            foreach (var p in parameters)
                countCmd.Parameters.AddWithValue(p.ParameterName, p.Value ?? DBNull.Value);

            var total = (long)await countCmd.ExecuteScalarAsync();

            return new JsonResult(new
            {
                draw = _draw,
                recordsTotal = total,
                recordsFiltered = total,
                data = rows
            });
        }
        public async Task<IActionResult> OnGetFiltersAsync(string machineLine)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var machineLines = new List<string>();
            var machineNames = new List<string>();

            // 🔹 Machine Lines
            using (var cmd = new NpgsqlCommand("SELECT DISTINCT machine_line FROM production_count ORDER BY machine_line", conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    if (!reader.IsDBNull(0))
                        machineLines.Add(reader.GetString(0));
            }

            // 🔹 Machine Names (DEPENDENT)
            string sql = "SELECT DISTINCT machine_name FROM production_count";

            if (!string.IsNullOrEmpty(machineLine))
                sql += " WHERE machine_line = @machineLine";

            sql += " ORDER BY machine_name";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(machineLine))
                    cmd.Parameters.AddWithValue("@machineLine", machineLine);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                    if (!reader.IsDBNull(0))
                        machineNames.Add(reader.GetString(0));
            }

            return new JsonResult(new
            {
                machineLines,
                machineNames
            });
        }

        private (DateTime Start, DateTime End) GetProductionDayRange()
        {
            DateTime now = DateTime.Now;

            DateTime productionStart;
            DateTime productionEnd;

            if (now.TimeOfDay < TimeSpan.FromHours(22))
            {
                // Current production day started yesterday at 22:00
                productionStart = now.Date.AddDays(-1).AddHours(22);
                productionEnd = now.Date.AddHours(22);
            }
            else
            {
                // New production day started today at 22:00
                productionStart = now.Date.AddHours(22);
                productionEnd = now.Date.AddDays(1).AddHours(22);
            }

            return (productionStart, productionEnd);
        }
    }
}

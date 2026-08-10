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
    public class Material_Consumption_SummaryModel : PageModel
    {
        private readonly IConfiguration _config;

        public Material_Consumption_SummaryModel(IConfiguration config)
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
                    mc.material_id ILIKE @search OR
                    s.machine_name ILIKE @search OR
                    s.machine_line ILIKE @search OR
                    s.work_order_number ILIKE @search
                )");
                parameters.Add(new NpgsqlParameter("@search", $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(machineLine))
            {
                where.Add("s.machine_line = @machineLine");
                parameters.Add(new NpgsqlParameter("@machineLine", machineLine));
            }

            if (!string.IsNullOrWhiteSpace(machineName))
            {
                where.Add("s.machine_name = @machineName");
                parameters.Add(new NpgsqlParameter("@machineName", machineName));
            }

            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (string.IsNullOrEmpty(range) || range == "today")
            {
                minDate = DateTime.Today;
                maxDate = DateTime.Today.AddDays(1);
            }
            else if (range == "7")
            {
                minDate = DateTime.Today.AddDays(-7);
                maxDate = DateTime.Today.AddDays(1);
            }
            else if (range == "30")
            {
                minDate = DateTime.Today.AddDays(-30);
                maxDate = DateTime.Today.AddDays(1);
            }
            else if (range == "custom")
            {
                if (DateTime.TryParse(fromDate, out var f)) minDate = f;
                if (DateTime.TryParse(toDate, out var t)) maxDate = t.AddDays(1);
            }

            if (minDate.HasValue)
            {
                where.Add("mc.created_at >= @minDate");
                parameters.Add(new NpgsqlParameter("@minDate", minDate));
            }

            if (maxDate.HasValue)
            {
                where.Add("mc.created_at < @maxDate");
                parameters.Add(new NpgsqlParameter("@maxDate", maxDate));
            }

            return (
                where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "",
                parameters
            );
        }

        // =========================================
        // 🔷 DATATABLE API (FIXED 🚀)
        // =========================================
        public async Task<IActionResult> OnGetLoadDataAsync(
            int draw, int start, int length,
            string search, string range,
            string fromDate, string toDate,
            string machineLine, string machineName)
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                var (whereSql, parameters) = BuildFilters(search, range, fromDate, toDate, machineLine, machineName);

                // 🔹 DATA QUERY
                var dataSql = $@"
                    SELECT 
                        COALESCE(si.""Name"", 'N/A') AS shift,
                        s.machine_line,
                        s.machine_name,
                        s.work_order_number,
                        mc.material_id,
                        mc.batch_identifier,
                        mc.quantity_consumed,
                        mc.created_at
                    FROM material_consumption mc
                    LEFT JOIN shift s ON s.id = mc.shift_id
                    LEFT JOIN ""AspNetUsers"" u ON u.""UserName"" = mc.created_by
                    LEFT JOIN ""UserShiftAssignment"" usa ON usa.""UserId"" = u.""Id""
                    LEFT JOIN ""ShiftInformation"" si ON si.""Id"" = usa.""ShiftInformationId""
                    {whereSql}
                    ORDER BY mc.created_at DESC
                    OFFSET @start LIMIT @length;
                ";

                var rows = new List<object>();

                using (var dataCmd = new NpgsqlCommand(dataSql, conn))
                {
                    foreach (var p in parameters)
                        dataCmd.Parameters.Add(new NpgsqlParameter(p.ParameterName, p.Value));

                    dataCmd.Parameters.AddWithValue("@start", start);
                    dataCmd.Parameters.AddWithValue("@length", length);

                    using var reader = await dataCmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        rows.Add(new
                        {
                            shift = reader.IsDBNull(0) ? "N/A" : reader.GetString(0),
                            machineLine = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            machineName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            wo = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            material = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            batch = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            qty = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                            createdAt = reader.IsDBNull(7) ? "" : reader.GetDateTime(7).ToString("yyyy-MM-dd")
                        });
                    }
                } // ✅ reader closed here

                // 🔹 COUNT QUERY (FIXED JOINS)
                var countSql = $@"
                    SELECT COUNT(*) 
                    FROM material_consumption mc
                    LEFT JOIN shift s ON s.id = mc.shift_id
                    LEFT JOIN ""AspNetUsers"" u ON u.""UserName"" = mc.created_by
                    LEFT JOIN ""UserShiftAssignment"" usa ON usa.""UserId"" = u.""Id""
                    LEFT JOIN ""ShiftInformation"" si ON si.""Id"" = usa.""ShiftInformationId""
                    {whereSql};
                ";

                long total;

                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    foreach (var p in parameters)
                        countCmd.Parameters.Add(new NpgsqlParameter(p.ParameterName, p.Value));

                    total = (long)await countCmd.ExecuteScalarAsync();
                }

                return new JsonResult(new
                {
                    draw,
                    recordsTotal = total,
                    recordsFiltered = total,
                    data = rows
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }
        }

        // =========================================
        // 🔷 SUMMARY API
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
                    COALESCE(si.""Name"", 'N/A') AS shift,
                    s.machine_line,
                    s.machine_name,
                    mc.material_id,
                    s.work_order_number,
                    mc.batch_identifier,
                    SUM(mc.quantity_consumed)
                FROM material_consumption mc
                LEFT JOIN shift s ON s.id = mc.shift_id
                LEFT JOIN ""AspNetUsers"" u ON u.""UserName"" = mc.created_by
                LEFT JOIN ""UserShiftAssignment"" usa ON usa.""UserId"" = u.""Id""
                LEFT JOIN ""ShiftInformation"" si ON si.""Id"" = usa.""ShiftInformationId""
                {whereSql}
                GROUP BY shift, s.machine_line, s.machine_name, mc.material_id, mc.batch_identifier, s.work_order_number
                ORDER BY shift;
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
                    shift = reader.IsDBNull(0) ? "N/A" : reader.GetString(0),
                    machineLine = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    machineName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    material = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    wo = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    batch = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    qty = reader.IsDBNull(6) ? 0 : reader.GetInt64(6)
                });
            }

            return new JsonResult(result);
        }
        public async Task<IActionResult> OnGetFiltersAsync(string machineLine)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var machineLines = new List<string>();
            var machineNames = new List<string>();

            // 🔹 Machine Lines
            using (var cmd = new NpgsqlCommand(@"
                SELECT DISTINCT s.machine_line 
                FROM material_consumption mc
                LEFT JOIN shift s ON s.id = mc.shift_id
                ORDER BY s.machine_line", conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    if (!reader.IsDBNull(0))
                        machineLines.Add(reader.GetString(0));
            }

            // 🔹 Machine Names (dependent on line)
            string sql = @"
                SELECT DISTINCT s.machine_name 
                FROM material_consumption mc
                LEFT JOIN shift s ON s.id = mc.shift_id
            ";

            if (!string.IsNullOrEmpty(machineLine))
                sql += " WHERE s.machine_line = @machineLine";

            sql += " ORDER BY s.machine_name";

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
    }
}
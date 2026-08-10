using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_ReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Production_ReportModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet() { }

        // ================= EXPORT =================
        public async Task<IActionResult> OnGetExportAsync(
            string search,
            string range,
            DateTime? fromDate,
            DateTime? toDate)
        {
            using var conn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (range == "7")
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
                minDate = fromDate;
                maxDate = toDate?.AddDays(1);
            }

            var where = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add(@"
                    (
                        COALESCE(wo_number,'') ILIKE @search OR
                        COALESCE(batch_identifier,'') ILIKE @search OR
                        COALESCE(machine_name,'') ILIKE @search OR
                        COALESCE(part_number,'') ILIKE @search
                    )");
            }

            if (minDate.HasValue)
                where.Add("created_at >= @minDate");

            if (maxDate.HasValue)
                where.Add("created_at < @maxDate");

            var whereSql = where.Any()
                ? "WHERE " + string.Join(" AND ", where)
                : "";

            var sql = $@"
                SELECT
                    shift_letter,
                    shift_id,
                    wo_number,
                    batch_identifier,
                    timestamp_start,
                    timestamp_end,
                    product_count,
                    machine_line,
                    machine_name,
                    product_line,
                    part_number,
                    item_description,
                    created_by,
                    created_at
                FROM production_count
                {whereSql}
                ORDER BY created_at DESC;
            ";

            using var cmd = new NpgsqlCommand(sql, conn);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", $"%{search}%");

            if (minDate.HasValue)
                cmd.Parameters.AddWithValue("@minDate", minDate);

            if (maxDate.HasValue)
                cmd.Parameters.AddWithValue("@maxDate", maxDate);

            using var reader = await cmd.ExecuteReaderAsync();

            var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Production");

            ws.Cell(1, 1).InsertTable(new[]
            {
                "Shift","Shift Id","WO Number","Batch ID","Start","End","Count",
                "Machine Line","Machine Name","Product Line",
                "Part Number","Description","Created By","Created Date"
            });

            int row = 2;

            while (await reader.ReadAsync())
            {
                ws.Cell(row, 1).Value = reader.GetString(0);
                ws.Cell(row, 2).Value = reader.GetInt32(1);
                ws.Cell(row, 3).Value = reader.GetString(2);
                ws.Cell(row, 4).Value = reader.GetString(3);
                ws.Cell(row, 5).Value = reader.IsDBNull(4) ? "" : reader.GetDateTime(4);
                ws.Cell(row, 6).Value = reader.IsDBNull(5) ? "" : reader.GetDateTime(5);
                ws.Cell(row, 7).Value = reader.GetInt64(6);
                ws.Cell(row, 8).Value = reader.GetString(7);
                ws.Cell(row, 9).Value = reader.GetString(8);
                ws.Cell(row, 10).Value = reader.GetString(9);
                ws.Cell(row, 11).Value = reader.GetString(10);
                ws.Cell(row, 12).Value = reader.GetString(11);
                ws.Cell(row, 13).Value = reader.GetString(12);
                ws.Cell(row, 14).Value = reader.GetDateTime(13);
                row++;
            }

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Production_Report_{DateTime.Now:yyyyMMddHHmm}.xlsx"
            );
        }

        // ================= LOAD DATA =================
        public async Task<IActionResult> OnGetLoadDataAsync(
            int draw,
            int start,
            int length,
            string search,
            string range,
            string fromDate,
            string toDate)
        {
            search ??= "";
            range ??= "all";

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (range == "7")
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

            var where = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add(@"
                    (
                        COALESCE(wo_number,'') ILIKE @search OR
                        COALESCE(batch_identifier,'') ILIKE @search OR
                        COALESCE(machine_name,'') ILIKE @search OR
                        COALESCE(part_number,'') ILIKE @search
                    )");
            }

            if (minDate.HasValue)
                where.Add("created_at >= @minDate");

            if (maxDate.HasValue)
                where.Add("created_at < @maxDate");

            var whereSql = where.Any()
                ? "WHERE " + string.Join(" AND ", where)
                : "";

            var dataSql = $@"
                SELECT
                    id,
                    shift_letter,
                    shift_id,
                    wo_number,
                    batch_identifier,
                    timestamp_start,
                    timestamp_end,
                    product_count,
                    machine_line,
                    machine_name,
                    product_line,
                    part_number,
                    item_description,
                    created_by,
                    created_at
                FROM production_count
                {whereSql}
                ORDER BY created_at DESC
                OFFSET @start LIMIT @length;
            ";

            var countSql = $@"
                SELECT COUNT(*)
                FROM production_count
                {whereSql};
            ";

            using var dataCmd = new NpgsqlCommand(dataSql, conn);
            using var countCmd = new NpgsqlCommand(countSql, conn);

            if (!string.IsNullOrWhiteSpace(search))
            {
                dataCmd.Parameters.AddWithValue("@search", $"%{search}%");
                countCmd.Parameters.AddWithValue("@search", $"%{search}%");
            }

            if (minDate.HasValue)
            {
                dataCmd.Parameters.AddWithValue("@minDate", minDate);
                countCmd.Parameters.AddWithValue("@minDate", minDate);
            }

            if (maxDate.HasValue)
            {
                dataCmd.Parameters.AddWithValue("@maxDate", maxDate);
                countCmd.Parameters.AddWithValue("@maxDate", maxDate);
            }

            dataCmd.Parameters.AddWithValue("@start", start);
            dataCmd.Parameters.AddWithValue("@length", length);

            var rows = new List<object>();

            using (var reader = await dataCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    rows.Add(new
                    {
                        actions = reader.GetInt32(0),
                        shift = reader.GetString(1),
                        shiftId = reader.GetInt32(2),
                        wo = reader.GetString(3),
                        batch = reader.GetString(4),
                        start = reader.IsDBNull(5) ? "" : reader.GetDateTime(5).ToString("dd/MM/yyyy HH:mm"),
                        end = reader.IsDBNull(6) ? "" : reader.GetDateTime(6).ToString("dd/MM/yyyy HH:mm"),
                        count = reader.GetInt64(7),
                        machineLine = reader.GetString(8),
                        machineName = reader.GetString(9),
                        productLine = reader.GetString(10),
                        partNumber = reader.GetString(11),
                        description = reader.GetString(12),
                        createdBy = reader.GetString(13),
                        createdAt = reader.GetDateTime(14).ToString("dd/MM/yyyy")
                    });
                }
            }

            long recordsFiltered = (long)await countCmd.ExecuteScalarAsync();

            using var totalCmd = new NpgsqlCommand("SELECT COUNT(*) FROM production_count", conn);
            long recordsTotal = (long)await totalCmd.ExecuteScalarAsync();

            return new JsonResult(new
            {
                draw,
                recordsTotal,
                recordsFiltered,
                data = rows
            });
        }
    }
}
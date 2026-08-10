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
    public class Quality_Check_ReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Quality_Check_ReportModel(IConfiguration configuration)
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
                        COALESCE(t.test_name,'') ILIKE @search OR
                        COALESCE(qc.status,'') ILIKE @search OR
                        COALESCE(qc.comment,'') ILIKE @search OR
                        COALESCE(s.machine_name,'') ILIKE @search
                    )");
            }

            if (minDate.HasValue)
                where.Add("qc.created_on >= @minDate");

            if (maxDate.HasValue)
                where.Add("qc.created_on < @maxDate");

            var whereSql = where.Any()
                ? "WHERE " + string.Join(" AND ", where)
                : "";

            var sql = $@"
                SELECT
                    COALESCE(t.test_name, qc.test_id::text),
                    qc.status,
                    qc.fail,
                    qc.weights,
                    qc.comment,
                    qc.check_time,
                    qc.created_by,
                    qc.created_on,
                    s.machine_line,
                    s.machine_name,
                    s.product_line
                FROM quality_check qc
                LEFT JOIN quality_checks_template t ON qc.test_id = t.id
                LEFT JOIN shift s ON qc.shift_id = s.id
                {whereSql}
                ORDER BY qc.created_on DESC;
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
            var ws = workbook.Worksheets.Add("Quality Checks");

            ws.Cell(1, 1).InsertTable(new[]
            {
                "Test Name","Status","Fail","Weights","Comment",
                "Check Time","Created By","Created On",
                "Machine Line","Machine Name","Product Line"
            });

            int row = 2;

            while (await reader.ReadAsync())
            {
                ws.Cell(row, 1).Value = reader.GetString(0);
                ws.Cell(row, 2).Value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                ws.Cell(row, 3).Value = reader.GetBoolean(2) ? "Yes" : "No";
                ws.Cell(row, 4).Value = reader.IsDBNull(3) ? "" : reader.GetString(3);
                ws.Cell(row, 5).Value = reader.IsDBNull(4) ? "" : reader.GetString(4);
                ws.Cell(row, 6).Value = reader.IsDBNull(5) ? "" : reader.GetTimeSpan(5).ToString();
                ws.Cell(row, 7).Value = reader.IsDBNull(6) ? "" : reader.GetString(6);
                ws.Cell(row, 8).Value = reader.GetDateTime(7);
                ws.Cell(row, 9).Value = reader.IsDBNull(8) ? "" : reader.GetString(8);
                ws.Cell(row, 10).Value = reader.IsDBNull(9) ? "" : reader.GetString(9);
                ws.Cell(row, 11).Value = reader.IsDBNull(10) ? "" : reader.GetString(10);
                row++;
            }

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Quality_Check_Report_{DateTime.Now:yyyyMMddHHmm}.xlsx"
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
                        COALESCE(t.test_name,'') ILIKE @search OR
                        COALESCE(qc.status,'') ILIKE @search OR
                        COALESCE(qc.comment,'') ILIKE @search OR
                        COALESCE(s.machine_name,'') ILIKE @search
                    )");
            }

            if (minDate.HasValue)
                where.Add("qc.created_on >= @minDate");

            if (maxDate.HasValue)
                where.Add("qc.created_on < @maxDate");

            var whereSql = where.Any()
                ? "WHERE " + string.Join(" AND ", where)
                : "";

            var dataSql = $@"
                SELECT
                    qc.id,
                    COALESCE(t.test_name, qc.test_id::text),
                    COALESCE(qc.status,''),
                    COALESCE(qc.fail,false),
                    COALESCE(qc.weights,''),
                    COALESCE(qc.comment,''),
                    qc.check_time,
                    COALESCE(qc.created_by,''),
                    qc.created_on,
                    COALESCE(s.id,0),
                    COALESCE(s.machine_line,''),
                    COALESCE(s.machine_name,''),
                    COALESCE(s.product_line,'')
                FROM quality_check qc
                LEFT JOIN quality_checks_template t ON qc.test_id = t.id
                LEFT JOIN shift s ON qc.shift_id = s.id
                {whereSql}
                ORDER BY qc.created_on DESC
                OFFSET @start LIMIT @length;
            ";

            var countSql = $@"
                SELECT COUNT(*)
                FROM quality_check qc
                LEFT JOIN quality_checks_template t ON qc.test_id = t.id
                LEFT JOIN shift s ON qc.shift_id = s.id
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
                        actions = reader.GetInt64(0),
                        testName = reader.GetString(1),
                        status = reader.GetString(2),
                        fail = reader.GetBoolean(3) ? "Yes" : "No",
                        weights = reader.GetString(4),
                        comment = reader.GetString(5),
                        checkTime = reader.IsDBNull(6) ? "" : reader.GetTimeSpan(6).ToString(@"hh\:mm\:ss"),
                        createdBy = reader.GetString(7),
                        createdOn = reader.GetDateTime(8).ToString("dd/MM/yyyy HH:mm"),
                        shiftId = reader.GetInt64(9),
                        machineLine = reader.GetString(10),
                        machineName = reader.GetString(11),
                        productLine = reader.GetString(12)
                    });
                }
            }

            long recordsFiltered = (long)await countCmd.ExecuteScalarAsync();

            using var totalCmd = new NpgsqlCommand("SELECT COUNT(*) FROM quality_check", conn);
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
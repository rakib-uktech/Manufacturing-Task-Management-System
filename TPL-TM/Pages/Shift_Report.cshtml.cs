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
    public class Shift_ReportModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_ReportModel(IConfiguration configuration)
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
                minDate = fromDate;
                maxDate = toDate?.AddDays(1);
            }

            var where = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add(@"
                    (
                        COALESCE(work_order_number,'') ILIKE @search OR
                        COALESCE(machine_name,'') ILIKE @search OR
                        COALESCE(product_line,'') ILIKE @search
                    )");
            }

            if (minDate.HasValue)
                where.Add("created_on >= @minDate");

            if (maxDate.HasValue)
                where.Add("created_on < @maxDate");

            var whereSql = where.Any() ? "WHERE " + string.Join(" AND ", where) : "";

            var sql = $@"
                SELECT *
                FROM shift
                {whereSql}
                ORDER BY created_on DESC;
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
            var ws = workbook.Worksheets.Add("Shift Report");

            ws.Cell(1, 1).InsertTable(new[]
            {
                "WO Number","Item","Description","Machine Line","Machine Name",
                "Product Line","Start","End","Active","Rating",
                "Created By","Authorized By","Comment","Created Date"
            });

            int row = 2;

            while (await reader.ReadAsync())
            {
                ws.Cell(row, 1).Value = reader["work_order_number"]?.ToString();
                ws.Cell(row, 2).Value = reader["work_order_item"]?.ToString();
                ws.Cell(row, 3).Value = reader["work_order_description"]?.ToString();
                ws.Cell(row, 4).Value = reader["machine_line"]?.ToString();
                ws.Cell(row, 5).Value = reader["machine_name"]?.ToString();
                ws.Cell(row, 6).Value = reader["product_line"]?.ToString();
                ws.Cell(row, 7).Value = reader.IsDBNull(reader.GetOrdinal("shift_start_time"))
                ? ""
                : reader.GetDateTime(reader.GetOrdinal("shift_start_time"));
                ws.Cell(row, 8).Value = reader.IsDBNull(reader.GetOrdinal("shift_end_time"))
                ? ""
                : reader.GetDateTime(reader.GetOrdinal("shift_end_time"));
                ws.Cell(row, 9).Value = reader.IsDBNull(reader.GetOrdinal("shift_active"))
                 ? ""
                 : (reader.GetBoolean(reader.GetOrdinal("shift_active")) ? "Yes" : "No");
                ws.Cell(row, 10).Value = reader.IsDBNull(reader.GetOrdinal("handover_rating"))
                ? ""
                : reader.GetInt32(reader.GetOrdinal("handover_rating"));
                ws.Cell(row, 11).Value = reader["created_by"]?.ToString();
                ws.Cell(row, 12).Value = reader["authorized_by"]?.ToString();
                ws.Cell(row, 13).Value = reader["comment"]?.ToString();
                ws.Cell(row, 10).Value = reader.GetDateTime(reader.GetOrdinal("created_on"));
                row++;
            }

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Shift_Report_{DateTime.Now:yyyyMMddHHmm}.xlsx"
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
                        COALESCE(work_order_number,'') ILIKE @search OR
                        COALESCE(machine_name,'') ILIKE @search OR
                        COALESCE(product_line,'') ILIKE @search
                    )");
            }

            if (minDate.HasValue)
                where.Add("created_on >= @minDate");

            if (maxDate.HasValue)
                where.Add("created_on < @maxDate");

            var whereSql = where.Any() ? "WHERE " + string.Join(" AND ", where) : "";

            var dataSql = $@"
                SELECT *
                FROM shift
                {whereSql}
                ORDER BY created_on DESC
                OFFSET @start LIMIT @length;
            ";

            var countSql = $@"
                SELECT COUNT(*)
                FROM shift
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
                        actions = reader.GetInt64(reader.GetOrdinal("id")),
                        wo = reader["work_order_number"]?.ToString(),
                        item = reader["work_order_item"]?.ToString(),
                        desc = reader["work_order_description"]?.ToString(),
                        machineLine = reader["machine_line"]?.ToString(),
                        machineName = reader["machine_name"]?.ToString(),
                        productLine = reader["product_line"]?.ToString(),
                        start = reader["shift_start_time"] == DBNull.Value ? "" : Convert.ToDateTime(reader["shift_start_time"]).ToString("dd/MM/yyyy HH:mm"),
                        end = reader["shift_end_time"] == DBNull.Value ? "" : Convert.ToDateTime(reader["shift_end_time"]).ToString("dd/MM/yyyy HH:mm"),
                        active = (bool)reader["shift_active"] ? "Yes" : "No",
                        rating = reader["handover_rating"],
                        createdBy = reader["created_by"]?.ToString(),
                        authorizedBy = reader["authorized_by"]?.ToString(),
                        comment = reader["comment"]?.ToString(),
                        createdAt = Convert.ToDateTime(reader["created_on"]).ToString("dd/MM/yyyy")
                    });
                }
            }

            long recordsFiltered = (long)await countCmd.ExecuteScalarAsync();
            using var totalCmd = new NpgsqlCommand("SELECT COUNT(*) FROM shift", conn);
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
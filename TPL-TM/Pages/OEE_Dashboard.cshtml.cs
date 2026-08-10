using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Npgsql;
using System.Net;
using System.Text;
using System.Text.Json;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class OEE_DashboardModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public string errorMessage = "";
        private readonly UserManager<IdentityUser> _userManager;
        public OEE_DashboardModel(IConfiguration configuration,
                    UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public List<ProductionInfo> listProduction { get; set; } = new();
        public List<MachineLineSummary> listMachineSummary { get; set; } = new();
        public List<ProductLineSummary> listProductSummary { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? FilterDate { get; set; }

        public void OnGet()
        {
            try
            {
                if (!FilterDate.HasValue)
                    FilterDate = DateTime.Today;

                var selectedDate = FilterDate.Value.Date;
                var nextDay = selectedDate.AddDays(1);

                string connString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new NpgsqlConnection(connString))
                {
                    connection.Open();

                    listProduction = LoadProduction(connection)
                        .Where(x => x.Timestamp_Start.HasValue &&
                                    x.Timestamp_Start.Value >= selectedDate &&
                                    x.Timestamp_Start.Value < nextDay)
                        .ToList();

                    listMachineSummary = listProduction
                        .GroupBy(x => x.Machine_Line)
                        .Select(g => new MachineLineSummary
                        {
                            Machine_Line = g.Key,
                            Total_Count = g.Sum(x => x.Product_Count)
                        })
                        .ToList();

                    listProductSummary = listProduction
                        .GroupBy(x => x.Product_Line)
                        .Select(g => new ProductLineSummary
                        {
                            Product_Line = g.Key,
                            Total_Count = g.Sum(x => x.Product_Count)
                        })
                        .ToList();
                }

                errorMessage = $"Loaded {listProduction.Count} records for {selectedDate:dd/MM/yyyy}";
            }
            catch (Exception ex)
            {
                errorMessage = "ERROR: " + ex.Message;
            }
        }

        private List<ProductionInfo> LoadProduction(NpgsqlConnection connection)
        {
            const string sql = @"
            SELECT machine_line, product_line, product_count, timestamp_start, shift_letter
            FROM production_count
            ORDER BY id DESC;
        ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            var list = new List<ProductionInfo>();
            while (reader.Read())
            {
                list.Add(new ProductionInfo
                {
                    Machine_Line = reader.IsDBNull(reader.GetOrdinal("machine_line")) ? "" : reader.GetString(reader.GetOrdinal("machine_line")).ToUpper(),
                    Product_Line = reader.IsDBNull(reader.GetOrdinal("product_line")) ? "" : reader.GetString(reader.GetOrdinal("product_line")),
                    Product_Count = reader.GetInt64(reader.GetOrdinal("product_count")),
                    Timestamp_Start = reader.IsDBNull(reader.GetOrdinal("timestamp_start")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("timestamp_start")),
                    Shift = reader.IsDBNull(reader.GetOrdinal("shift_letter")) ? "" : reader.GetString(reader.GetOrdinal("shift_letter"))
                });
            }
            return list;
        }

        public async Task<IActionResult> OnGetDashboardDataAsync(
            string search, string range,
            string fromDate, string toDate,
            string machineLine)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var where = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add("(machine_line ILIKE @search OR product_line ILIKE @search)");
                parameters.Add(new NpgsqlParameter("@search", $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(machineLine))
            {
                where.Add("machine_line = @machineLine");
                parameters.Add(new NpgsqlParameter("@machineLine", machineLine));
            }

            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (string.IsNullOrEmpty(range) || range == "1")
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
                where.Add("timestamp_start >= @minDate");
                parameters.Add(new NpgsqlParameter("@minDate", minDate));
            }

            if (maxDate.HasValue)
            {
                where.Add("timestamp_start < @maxDate");
                parameters.Add(new NpgsqlParameter("@maxDate", maxDate));
            }

            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            var sql = $@"
            SELECT machine_line, machine_name, product_line, product_count, timestamp_start, shift_letter
            FROM production_count
            {whereSql};
        ";

            using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var p in parameters)
                cmd.Parameters.Add(p);

            var productionList = new List<ProductionInfo>();

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    productionList.Add(new ProductionInfo
                    {
                        Machine_Line = reader.GetString(0),
                        Machine_Name = reader.GetString(1),
                        Product_Line = reader.GetString(2),
                        Product_Count = reader.GetInt64(3),
                        Timestamp_Start = reader.GetDateTime(4),
                        Shift = reader.IsDBNull(5) ? "" : reader.GetString(5)
                    });
                }
            }

            var targets = LoadProductionTargets(conn);
            var downtimeList = LoadDowntime(conn, minDate, maxDate);
            var wasteDetails = LoadWaste(conn, minDate, maxDate);

            var result = productionList
                .GroupBy(x => new { x.Machine_Line, x.Product_Line, x.Machine_Name })
                .Select(g =>
                {
                    var machineName = g.Key.Machine_Name;

                    var shifts = g.Where(x => !string.IsNullOrEmpty(x.Shift)).Select(x => x.Shift).Distinct().ToList();
                    int shiftCount = shifts.Count;

                    var targetPerShift = targets
                        .Where(t => t.Machine_Name == machineName)
                        .Select(t => t.Target_Count)
                        .DefaultIfEmpty(0).Max();

                    var totalTarget = targetPerShift * shiftCount;
                    var totalActual = g.Sum(x => x.Product_Count);

                    var downtimeFiltered = downtimeList
                        .Where(d =>
                            d.Machine_Line == g.Key.Machine_Line &&
                            d.Machine_Name == machineName &&
                            g.Key.Product_Line.StartsWith(d.Product_Line))
                        .ToList();

                    var downtimeByShift = downtimeFiltered
                        .GroupBy(d => d.Shift)
                        .Select(s => new { shift = s.Key, total = s.Sum(x => x.Total_Downtime) })
                        .ToList();

                    var wasteFiltered = wasteDetails
                        .Where(w =>
                            w.Machine_Line == g.Key.Machine_Line &&
                            w.Machine_Name == machineName &&
                            w.Product_Line == g.Key.Product_Line)
                        .ToList();

                    var totalWaste = wasteFiltered.Sum(x => x.WasteWeight);

                    return new
                    {
                        machine_Line = g.Key.Machine_Line,
                        product_Line = g.Key.Product_Line,
                        machine_name = machineName,
                        total = totalActual,
                        target = totalTarget,
                        achievement = totalTarget == 0 ? 0 : Math.Round((decimal)totalActual / totalTarget * 100, 1),
                        downtime = downtimeFiltered.Sum(x => x.Total_Downtime),
                        downtimeByShift = downtimeByShift,
                        downtimeData = downtimeFiltered,
                        waste = totalWaste,
                        wasteData = wasteFiltered,
                        data = g.ToList()
                    };
                })
                .ToList();

            return new JsonResult(result);
        }

        private List<ProductionTarget> LoadProductionTargets(NpgsqlConnection connection)
        {
            const string sql = @"
            SELECT machine_line, machine_name, product_line, target_count
            FROM production_target
            WHERE is_active = TRUE;
        ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            var list = new List<ProductionTarget>();

            while (reader.Read())
            {
                list.Add(new ProductionTarget
                {
                    Machine_Line = reader.GetString(0),
                    Machine_Name = reader.GetString(1),
                    Product_Line = reader.GetString(2),
                    Target_Count = reader.GetInt64(3)
                });
            }
            return list;
        }

        private List<DowntimeSummary> LoadDowntime(NpgsqlConnection conn, DateTime? minDate, DateTime? maxDate)
        {
            string sql = @"
            SELECT
                s.machine_line,
                s.machine_name,
                s.product_line,
                COALESCE(r.reason_name, 'Unknown') AS reason_name,
                p.shift_letter,
                DATE(d.created_on) AS created_on,
                SUM(d.downtime) AS total_downtime
            FROM downtime d
            LEFT JOIN shift s ON d.shift = s.id
            LEFT JOIN downtime_reason r ON d.reason_id = r.id
            LEFT JOIN (
                SELECT shift_id, shift_letter FROM production_count GROUP BY shift_id, shift_letter
            ) p ON d.shift = p.shift_id
            WHERE 1=1";

            var cmd = new NpgsqlCommand(sql, conn);

            if (minDate.HasValue) { sql += " AND d.created_on >= @minDate"; cmd.Parameters.AddWithValue("@minDate", minDate.Value); }
            if (maxDate.HasValue) { sql += " AND d.created_on < @maxDate"; cmd.Parameters.AddWithValue("@maxDate", maxDate.Value); }

            sql += @"
            GROUP BY s.machine_line, s.machine_name, s.product_line, r.reason_name, p.shift_letter, DATE(d.created_on)
            ORDER BY s.machine_line, s.machine_name";

            cmd.CommandText = sql;

            var list = new List<DowntimeSummary>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new DowntimeSummary
                    {
                        Machine_Line = reader["machine_line"]?.ToString() ?? "",
                        Machine_Name = reader["machine_name"]?.ToString() ?? "",
                        Product_Line = reader["product_line"]?.ToString() ?? "",
                        Reason_Name = reader["reason_name"]?.ToString() ?? "Unknown",
                        Shift = reader["shift_letter"]?.ToString() ?? "",
                        Created_On = reader["created_on"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["created_on"]),
                        Total_Downtime = reader["total_downtime"] == DBNull.Value ? 0 : Convert.ToInt32(reader["total_downtime"])
                    });
                }
            }
            return list;
        }

        private List<WasteDetail> LoadWaste(NpgsqlConnection conn, DateTime? minDate, DateTime? maxDate)
        {
            string sql = @"
            SELECT
                s.machine_line, s.machine_name, s.product_line,
                w.waste_type, w.waste_weight, p.shift_letter,
                DATE(w.created_on) AS created_on
            FROM waste w
            LEFT JOIN shift s ON w.shift_id = s.id
            LEFT JOIN (
                SELECT shift_id, shift_letter FROM production_count GROUP BY shift_id, shift_letter
            ) p ON w.shift_id = p.shift_id
            WHERE 1=1";

            var cmd = new NpgsqlCommand(sql, conn);

            if (minDate.HasValue) { sql += " AND w.created_on >= @minDate"; cmd.Parameters.AddWithValue("@minDate", minDate.Value); }
            if (maxDate.HasValue) { sql += " AND w.created_on < @maxDate"; cmd.Parameters.AddWithValue("@maxDate", maxDate.Value); }

            cmd.CommandText = sql;

            var list = new List<WasteDetail>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new WasteDetail
                {
                    Machine_Line = reader["machine_line"]?.ToString() ?? "",
                    Machine_Name = reader["machine_name"]?.ToString() ?? "",
                    Product_Line = reader["product_line"]?.ToString() ?? "",
                    WasteType = reader["waste_type"]?.ToString() ?? "",
                    WasteWeight = reader["waste_weight"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["waste_weight"]),
                    Shift = reader["shift_letter"]?.ToString() ?? "",
                    Created_On = Convert.ToDateTime(reader["created_on"])
                });
            }
            return list;
        }

        public async Task<IActionResult> OnGetAIReportAsync(
            string search, string range, string fromDate, string toDate, string machineLine)
        {
            var dashboardResult = await OnGetDashboardDataAsync(search, range, fromDate, toDate, machineLine) as JsonResult;
            var rawData = dashboardResult.Value as IEnumerable<object>;
            var data = rawData.Cast<dynamic>().ToList();

            var summary = data
                .OrderByDescending(x => ((int)(x.downtime ?? 0) + (decimal)(x.waste ?? 0)))
                .Take(10)
                .Select(x => new
                {
                    machine = x.machine_name,
                    product = x.product_Line,
                    total = x.total,
                    target = x.target,
                    achievement = x.achievement,
                    downtime = x.downtime,
                    waste = x.waste
                })
                .ToList();

            var json = JsonSerializer.Serialize(summary);
            var report = await GenerateAIReport(json);
            return Content(report);
        }

        private async Task<string> GenerateAIReport(string dashboardJson)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var body = new
            {
                model = "gpt-4o-mini",
                input = BuildAiPrompt(dashboardJson),
                temperature = 0.2,
                max_output_tokens = 400
            };

            var response = await http.PostAsJsonAsync("https://api.openai.com/v1/responses", body);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"❌ OpenAI Error:\n{raw}";

            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement
                .GetProperty("output")[0]
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "⚠️ Empty response";
        }

        private async Task<string> GenerateBedrockReport(string dashboardJson)
        {
            try
            {
                var awsConfig = _configuration.GetSection("AWS");
                var client = new AmazonBedrockRuntimeClient(
                    awsConfig["AccessKey"],
                    awsConfig["SecretKey"],
                    RegionEndpoint.GetBySystemName(awsConfig["Region"] ?? "eu-west-1"));

                var requestBody = new
                {
                    anthropic_version = "bedrock-2023-05-31",
                    max_tokens = 400,
                    temperature = 0.2,
                    messages = new[]
                    {
                    new { role = "user", content = new[] { new { type = "text", text = BuildAiPrompt(dashboardJson) } } }
                }
                };

                var request = new InvokeModelRequest
                {
                    ModelId = "anthropic.claude-3-haiku-20240307-v1:0",
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestBody)))
                };

                var response = await client.InvokeModelAsync(request);
                using var reader = new StreamReader(response.Body);
                var raw = await reader.ReadToEndAsync();

                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "⚠️ Empty response";
            }
            catch (Exception ex)
            {
                return "❌ Bedrock Error: " + ex.Message;
            }
        }

        public async Task<IActionResult> OnGetClaudeReportAsync(
            string search, string range, string fromDate, string toDate, string machineLine)
        {
            var dashboardResult = await OnGetDashboardDataAsync(search, range, fromDate, toDate, machineLine) as JsonResult;
            var jsonData = JsonSerializer.Serialize(dashboardResult?.Value);
            var data = JsonSerializer.Deserialize<List<AiSummaryDto>>(jsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var summary = data.OrderByDescending(x => x.downtime + (decimal)x.waste).Take(10).ToList();
            var finalJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var report = await GenerateBedrockReport(finalJson);
            return Content(report);
        }

        private string BuildAiPrompt(string dashboardJson)
        {
            return $@"
You are a manufacturing analytics expert.
Analyze the dataset and generate a structured management report.

RULES:
- Use ONLY provided data
- downtime is ALWAYS in minutes
- waste is ALWAYS in kg
- DO NOT convert units
- DO NOT assume missing values

OUTPUT FORMAT:
1. Executive Summary
2. Key Issues (Top 3)
3. Downtime Analysis
4. Waste Analysis
5. Underperforming Machines
6. Recommendations

DATA:
{dashboardJson}";
        }

        // ================================================================
        // 📧 SEND AI EMAIL — rich HTML report via Microsoft Graph
        // ================================================================
        public async Task<IActionResult> OnGetSendAIEmailAsync(
            string search, string range, string fromDate, string toDate, string machineLine)
        {
            try
            {
                // ── 1. Load & deserialise dashboard data ─────────────────
                var dashboardResult = await OnGetDashboardDataAsync(search, range, fromDate, toDate, machineLine) as JsonResult;
                var jsonData = JsonSerializer.Serialize(dashboardResult?.Value);
                var data = JsonSerializer.Deserialize<List<EmailReportDto>>(jsonData,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || !data.Any())
                    return Content("❌ No dashboard data found.");

                // ── 2. Generate AI insights via Bedrock (Claude) ─────────
                var aiSummary = data
                    .OrderByDescending(x => x.downtime + x.waste)
                    .Take(10)
                    .Select(x => new AiSummaryDto
                    {
                        machine = x.machine_name,
                        product = x.product_Line,
                        total = x.total,
                        target = x.target,
                        achievement = x.achievement,
                        downtime = x.downtime,
                        waste = x.waste
                    })
                    .ToList();

                var aiJson = JsonSerializer.Serialize(aiSummary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var aiReport = await GenerateBedrockReport(aiJson);

                // ── 3. Build aggregated totals for the summary cards ─────
                long totalProduction = data.Sum(x => x.total);
                int totalDowntime = data.Sum(x => x.downtime);
                decimal totalWaste = data.Sum(x => x.waste);

                long shiftA = data.SelectMany(x => x.data ?? new()).Where(d => d.shift == "A").Sum(d => d.product_Count);
                long shiftB = data.SelectMany(x => x.data ?? new()).Where(d => d.shift == "B").Sum(d => d.product_Count);
                long shiftC = data.SelectMany(x => x.data ?? new()).Where(d => d.shift == "C").Sum(d => d.product_Count);

                // ── 3b. Machine line breakdown ────────────────────────────
                var machineLineBreakdown = data
                    .GroupBy(x => x.machine_Line)
                    .Select(g => new
                    {
                        MachineLine = g.Key,
                        Total = g.Sum(x => x.total),
                        Target = g.Sum(x => x.target),
                        Achievement = g.Sum(x => x.target) == 0
                                        ? 0m
                                        : Math.Round((decimal)g.Sum(x => x.total) / g.Sum(x => x.target) * 100, 1),
                        Downtime = g.Sum(x => x.downtime),
                        Waste = g.Sum(x => x.waste),
                        ShiftA = g.SelectMany(x => x.data ?? new()).Where(d => d.shift == "A").Sum(d => d.product_Count),
                        ShiftB = g.SelectMany(x => x.data ?? new()).Where(d => d.shift == "B").Sum(d => d.product_Count),
                        ShiftC = g.SelectMany(x => x.data ?? new()).Where(d => d.shift == "C").Sum(d => d.product_Count),
                    })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var machineLineRows = new StringBuilder();
                foreach (var ml in machineLineBreakdown)
                {
                    var mlFillColor = ml.Achievement >= 90 ? "#22c55e"
                                    : ml.Achievement >= 70 ? "#f59e0b"
                                    : "#ef4444";
                    var mlPctColor = ml.Achievement >= 90 ? "#16a34a"
                                    : ml.Achievement >= 70 ? "#d97706"
                                    : "#dc2626";
                    var mlClampPct = Math.Min(100, (double)ml.Achievement);

                    machineLineRows.Append($@"
            <tr style='border-bottom:1px solid #f1f5f9;'>
                <td style='padding:11px 12px;font-weight:700;color:#0f172a;font-size:14px;'>
                    {WebUtility.HtmlEncode(ml.MachineLine)}
                </td>
                <td style='padding:11px 12px;font-size:13px;'>
                    <strong>{ml.Total:N0}</strong>
                    <span style='color:#9ca3af;font-size:11px;'> / {ml.Target:N0}</span>
                </td>
                <td style='padding:11px 12px;min-width:140px;'>
                    <div style='display:flex;align-items:center;gap:8px;'>
                        <div style='flex:1;height:6px;background:#e2e8f0;border-radius:3px;overflow:hidden;'>
                            <div style='width:{mlClampPct}%;height:100%;background:{mlFillColor};border-radius:3px;'></div>
                        </div>
                        <span style='font-size:12px;font-weight:600;color:{mlPctColor};min-width:36px;text-align:right;'>{ml.Achievement}%</span>
                    </div>
                </td>
                <td style='padding:11px 12px;font-size:12px;'>
                    <span style='background:#dcfce7;color:#15803d;padding:2px 7px;border-radius:4px;font-weight:600;margin-right:3px;'>A: {ml.ShiftA:N0}</span>
                    <span style='background:#fef3c7;color:#92400e;padding:2px 7px;border-radius:4px;font-weight:600;margin-right:3px;'>B: {ml.ShiftB:N0}</span>
                    <span style='background:#fee2e2;color:#b91c1c;padding:2px 7px;border-radius:4px;font-weight:600;'>C: {ml.ShiftC:N0}</span>
                </td>
                <td style='padding:11px 12px;font-size:13px;font-weight:600;color:#dc2626;text-align:right;'>{ml.Downtime:N0} min</td>
                <td style='padding:11px 12px;font-size:13px;font-weight:600;color:#d97706;text-align:right;'>{ml.Waste:N1} kg</td>
            </tr>");
                }

                // ── 4. Period label ───────────────────────────────────────
                var period = range switch
                {
                    "1" => "Today",
                    "7" => "Last 7 days",
                    "30" => "Last 30 days",
                    "all" => "All time",
                    "custom" => $"{fromDate} → {toDate}",
                    _ => "Today"
                };

                // ── 5. Alert band (machines below 70%) ───────────────────
                var underPerforming = data.Where(x => x.achievement < 70).ToList();
                var alertBand = underPerforming.Any()
                    ? $@"<tr><td colspan='2'>
                <div style='background:#fff8e1;border-left:4px solid #f59e0b;padding:12px 20px;font-size:13px;color:#92400e;'>
                    ⚠️ <strong>{underPerforming.Count} machine(s)</strong> below 70% achievement:
                    {string.Join(", ", underPerforming.Select(m => WebUtility.HtmlEncode(m.machine_name)))}
                </div>
                </td></tr>"
                    : "";

                // ── 6. Machine performance rows ───────────────────────────
                var machineRows = new StringBuilder();
                foreach (var m in data.OrderByDescending(x => x.achievement))
                {
                    var fillColor = m.achievement >= 90 ? "#22c55e"
                                    : m.achievement >= 70 ? "#f59e0b"
                                    : "#ef4444";
                    var pctColor = m.achievement >= 90 ? "#16a34a"
                                    : m.achievement >= 70 ? "#d97706"
                                    : "#dc2626";
                    var clampPct = Math.Min(100, (double)m.achievement);

                    var mShiftA = (m.data ?? new()).Where(d => d.shift == "A").Sum(d => d.product_Count);
                    var mShiftB = (m.data ?? new()).Where(d => d.shift == "B").Sum(d => d.product_Count);
                    var mShiftC = (m.data ?? new()).Where(d => d.shift == "C").Sum(d => d.product_Count);

                    machineRows.Append($@"
            <tr style='border-bottom:1px solid #f1f5f9;'>
                <td style='padding:11px 12px;'>
                    <div style='font-weight:600;color:#0f172a;font-size:13px;'>{WebUtility.HtmlEncode(m.machine_name)}</div>
                    <div style='font-size:11px;color:#64748b;margin-top:2px;'>{WebUtility.HtmlEncode(m.machine_Line)} · {WebUtility.HtmlEncode(m.product_Line)}</div>
                </td>
                <td style='padding:11px 12px;font-size:13px;'>
                    <strong>{m.total:N0}</strong>
                    <span style='color:#9ca3af;font-size:11px;'> / {m.target:N0}</span>
                </td>
                <td style='padding:11px 12px;min-width:140px;'>
                    <div style='display:flex;align-items:center;gap:8px;'>
                        <div style='flex:1;height:6px;background:#e2e8f0;border-radius:3px;overflow:hidden;'>
                            <div style='width:{clampPct}%;height:100%;background:{fillColor};border-radius:3px;'></div>
                        </div>
                        <span style='font-size:12px;font-weight:600;color:{pctColor};min-width:36px;text-align:right;'>{m.achievement}%</span>
                    </div>
                </td>
                <td style='padding:11px 12px;font-size:12px;'>
                    <span style='background:#dcfce7;color:#15803d;padding:2px 7px;border-radius:4px;font-weight:600;margin-right:3px;'>A: {mShiftA:N0}</span>
                    <span style='background:#fef3c7;color:#92400e;padding:2px 7px;border-radius:4px;font-weight:600;margin-right:3px;'>B: {mShiftB:N0}</span>
                    <span style='background:#fee2e2;color:#b91c1c;padding:2px 7px;border-radius:4px;font-weight:600;'>C: {mShiftC:N0}</span>
                </td>
                <td style='padding:11px 12px;font-size:13px;font-weight:600;color:#dc2626;text-align:right;'>{m.downtime:N0} min</td>
            </tr>");
                }

                // ── 7. Downtime reason rows (top 10 across all machines) ──
                var allDowntime = data
                    .SelectMany(x => (x.downtimeData ?? new()).Select(d => new { x.machine_name, d.reason_Name, d.total_Downtime }))
                    .GroupBy(d => new { d.machine_name, d.reason_Name })
                    .Select(g => new { g.Key.machine_name, reason = g.Key.reason_Name ?? "Unknown", total = g.Sum(d => d.total_Downtime) })
                    .OrderByDescending(d => d.total)
                    .Take(10);

                var downtimeRows = new StringBuilder();
                foreach (var dt in allDowntime)
                {
                    downtimeRows.Append($@"
            <tr style='border-bottom:1px solid #fef2f2;'>
                <td style='padding:10px 12px;font-weight:600;color:#374151;font-size:13px;'>{WebUtility.HtmlEncode(dt.machine_name)}</td>
                <td style='padding:10px 12px;'>
                    <span style='background:#fff1f2;color:#9f1239;border:1px solid #fecdd3;border-radius:4px;font-size:11px;padding:2px 8px;font-weight:600;'>
                        {WebUtility.HtmlEncode(dt.reason)}
                    </span>
                </td>
                <td style='padding:10px 12px;font-weight:700;color:#dc2626;font-size:14px;text-align:right;'>{dt.total:N0} min</td>
            </tr>");
                }

                if (!allDowntime.Any())
                    downtimeRows.Append("<tr><td colspan='3' style='text-align:center;color:#9ca3af;padding:20px;font-size:13px;'>No downtime recorded</td></tr>");

                // ── 8. Waste cards (top 6) ────────────────────────────────
                var allWaste = data
                    .SelectMany(x => (x.wasteData ?? new()).Select(w => new { x.machine_name, w.wasteType, w.wasteWeight }))
                    .GroupBy(w => new { w.machine_name, w.wasteType })
                    .Select(g => new { g.Key.machine_name, type = g.Key.wasteType ?? "Unknown", total = g.Sum(w => w.wasteWeight) })
                    .OrderByDescending(w => w.total)
                    .Take(6);

                var wasteCards = new StringBuilder();
                foreach (var w in allWaste)
                {
                    wasteCards.Append($@"
            <td style='width:50%;padding:6px;vertical-align:top;'>
                <div style='background:#fffbeb;border:1px solid #fde68a;border-radius:10px;padding:14px 16px;'>
                    <div style='font-size:11px;font-weight:700;color:#92400e;text-transform:uppercase;letter-spacing:0.5px;'>{WebUtility.HtmlEncode(w.type)}</div>
                    <div style='font-size:13px;color:#78350f;margin-top:2px;'>{WebUtility.HtmlEncode(w.machine_name)}</div>
                    <div style='font-size:22px;font-weight:700;color:#b45309;margin-top:4px;'>{w.total:N1}<span style='font-size:12px;font-weight:400;'> kg</span></div>
                </div>
            </td>");
                }

                if (!allWaste.Any())
                    wasteCards.Append("<td style='color:#9ca3af;font-size:13px;padding:12px;'>No waste recorded in this period.</td>");

                // ── 9. AI insight blocks ──────────────────────────────────
                var aiInsightHtml = BuildAiInsightBlocks(aiReport);

                // ── 10. Assemble final HTML ───────────────────────────────
                var html = $@"
    <!DOCTYPE html>
    <html lang='en'>
    <head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1.0'><title>Production Report</title></head>

    <body style='margin:0;padding:0;background:#f0f2f5;font-family:Segoe UI,Arial,sans-serif;color:#1a1a2e;'>
    <div style='max-width:680px;margin:0 auto;background:#ffffff;'>

        <!-- HEADER -->
        <div style='background:linear-gradient(135deg,#0f4c3a 0%,#1a7a5e 60%,#22a073 100%);padding:32px 40px;'>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td>
                        <div style='font-size:11px;font-weight:700;letter-spacing:3px;color:darkgreen;text-transform:uppercase;'>Transcend Packaging</div>
                        <div style='font-size:26px;font-weight:700;color:#8B0000;margin-top:4px;line-height:1.2;'>Management Report</div>
                        <div style='font-size:13px;color:#333333;margin-top:6px;'>Live production monitoring · Machine &amp; product line summary</div>
                    </td>
                    <td style='text-align:right;vertical-align:top;'>
                        <div style='background:rgba(0,0,0,0.05);border:1px solid rgba(0,0,0,0.1);border-radius:8px;padding:8px 16px;display:inline-block;'>
                            <div style='font-size:10px;color:rgba(0,0,0,0.5);letter-spacing:1px;text-transform:uppercase;'>Generated</div>
                            <div style='font-size:15px;font-weight:600;color:#333;margin-top:2px;'>{DateTime.Now:dd MMM yyyy, HH:mm}</div>
                            <div style='font-size:10px;color:rgba(0,0,0,0.5);margin-top:4px;'>Period: {period}</div>
                        </div>
                    </td>
                </tr>
                {alertBand}
            </table>
        </div>

        <!-- KPI CARDS -->
        <div style='padding:28px 40px;'>
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin-bottom:16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>Overview</div>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='width:33%;padding-right:8px;vertical-align:top;'>
                        <div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;padding:16px;text-align:center;'>
                            <div style='font-size:11px;font-weight:600;color:#6b7280;letter-spacing:0.5px;text-transform:uppercase;'>Total Production</div>
                            <div style='font-size:28px;font-weight:700;color:#16a34a;margin:4px 0;line-height:1;'>{totalProduction:N0}</div>
                            <div style='font-size:12px;color:#9ca3af;'>units across all machines</div>
                        </div>
                    </td>
                    <td style='width:33%;padding:0 4px;vertical-align:top;'>
                        <div style='background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:16px;text-align:center;'>
                            <div style='font-size:11px;font-weight:600;color:#6b7280;letter-spacing:0.5px;text-transform:uppercase;'>Total Downtime</div>
                            <div style='font-size:28px;font-weight:700;color:#dc2626;margin:4px 0;line-height:1;'>{totalDowntime:N0} min</div>
                            <div style='font-size:12px;color:#9ca3af;'>minutes lost</div>
                        </div>
                    </td>
                    <td style='width:33%;padding-left:8px;vertical-align:top;'>
                        <div style='background:#fffbeb;border:1px solid #fde68a;border-radius:10px;padding:16px;text-align:center;'>
                            <div style='font-size:11px;font-weight:600;color:#6b7280;letter-spacing:0.5px;text-transform:uppercase;'>Total Waste</div>
                            <div style='font-size:28px;font-weight:700;color:#d97706;margin:4px 0;line-height:1;'>{totalWaste:N1} kg</div>
                            <div style='font-size:12px;color:#9ca3af;'>kg recorded</div>
                        </div>
                    </td>
                </tr>
            </table>

            <!-- SHIFT BREAKDOWN -->
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin:24px 0 16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>Shift Breakdown</div>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='width:33%;padding-right:8px;vertical-align:top;'>
                        <div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;padding:16px;text-align:center;'>
                            <div style='font-size:11px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#15803d;'>Shift A</div>
                            <div style='font-size:24px;font-weight:700;color:#16a34a;margin:4px 0;'>{shiftA:N0}</div>
                            <div style='font-size:11px;color:#9ca3af;'>units produced</div>
                        </div>
                    </td>
                    <td style='width:33%;padding:0 4px;vertical-align:top;'>
                        <div style='background:#fffbeb;border:1px solid #fde68a;border-radius:10px;padding:16px;text-align:center;'>
                            <div style='font-size:11px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#92400e;'>Shift B</div>
                            <div style='font-size:24px;font-weight:700;color:#d97706;margin:4px 0;'>{shiftB:N0}</div>
                            <div style='font-size:11px;color:#9ca3af;'>units produced</div>
                        </div>
                    </td>
                    <td style='width:33%;padding-left:8px;vertical-align:top;'>
                        <div style='background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:16px;text-align:center;'>
                            <div style='font-size:11px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#b91c1c;'>Shift C</div>
                            <div style='font-size:24px;font-weight:700;color:#dc2626;margin:4px 0;'>{shiftC:N0}</div>
                            <div style='font-size:11px;color:#9ca3af;'>units produced</div>
                        </div>
                    </td>
                </tr>
            </table>

            <!-- MACHINE LINE BREAKDOWN -->
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin:24px 0 16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>Machine Line Breakdown</div>
            <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:13px;'>
                <thead>
                    <tr style='background:#1e3a5f;'>
                        <th style='color:#93c5fd;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;border-radius:6px 0 0 6px;'>Line</th>
                        <th style='color:#93c5fd;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Produced</th>
                        <th style='color:#93c5fd;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Achievement</th>
                        <th style='color:#93c5fd;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Shifts (A · B · C)</th>
                        <th style='color:#93c5fd;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:right;'>Downtime</th>
                        <th style='color:#93c5fd;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:right;border-radius:0 6px 6px 0;'>Waste</th>
                    </tr>
                </thead>
                <tbody>
                    {machineLineRows}
                </tbody>
            </table>
        </div>

        <div style='height:1px;background:#e5e7eb;margin:0 40px;'></div>

        <!-- MACHINE PERFORMANCE -->
        <div style='padding:28px 40px;'>
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin-bottom:16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>Machine Performance</div>
            <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:13px;'>
                <thead>
                    <tr style='background:#1e293b;'>
                        <th style='color:#94a3b8;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;border-radius:6px 0 0 6px;'>Machine / Product</th>
                        <th style='color:#94a3b8;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Produced</th>
                        <th style='color:#94a3b8;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Achievement</th>
                        <th style='color:#94a3b8;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Shifts (A · B · C)</th>
                        <th style='color:#94a3b8;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:right;border-radius:0 6px 6px 0;'>Downtime</th>
                    </tr>
                </thead>
                <tbody>
                    {machineRows}
                </tbody>
            </table>
        </div>

        <div style='height:1px;background:#e5e7eb;margin:0 40px;'></div>

        <!-- DOWNTIME -->
        <div style='padding:28px 40px;'>
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin-bottom:16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>Top Downtime Reasons</div>
            <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:13px;'>
                <thead>
                    <tr style='background:#450a0a;'>
                        <th style='color:#fca5a5;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;border-radius:6px 0 0 6px;'>Machine</th>
                        <th style='color:#fca5a5;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:left;'>Reason</th>
                        <th style='color:#fca5a5;font-size:10px;font-weight:600;letter-spacing:1px;text-transform:uppercase;padding:10px 12px;text-align:right;border-radius:0 6px 6px 0;'>Duration</th>
                    </tr>
                </thead>
                <tbody>
                    {downtimeRows}
                </tbody>
            </table>
        </div>

        <div style='height:1px;background:#e5e7eb;margin:0 40px;'></div>

        <!-- WASTE -->
        <div style='padding:28px 40px;'>
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin-bottom:16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>Waste Analysis</div>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>{wasteCards}</tr>
            </table>
        </div>

        <div style='height:1px;background:#e5e7eb;margin:0 40px;'></div>

        <!-- AI INSIGHTS -->
        <div style='padding:28px 40px;'>
            <div style='font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#6b7280;margin-bottom:16px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;'>
                AI Insights
            </div>
            <div style='background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:12px;padding:24px;'>
                <table cellpadding='0' cellspacing='0' style='margin-bottom:16px;'>
                    <tr>
                        <td style='width:32px;height:32px;background:#6366f1;border-radius:8px;text-align:center;font-size:16px;vertical-align:middle;'>
                            🧠
                        </td>
                        <td style='padding-left:10px;vertical-align:middle;'>
                            <div style='font-size:14px;font-weight:700;color:#f8fafc;'>Claude AI Analysis</div>
                            <div style='font-size:12px;color:#94a3b8;'>Auto-generated from production data</div>
                        </td>
                    </tr>
                </table>
                <div style='background:#020617;border:1px solid #1e293b;border-radius:10px;padding:18px;color:#e2e8f0;line-height:1.7;'>
                    {aiInsightHtml}
                </div>
            </div>
        </div>

        <!-- LIVE DASHBOARD -->
        <div style='padding:28px 40px;'>
            <div style='background:linear-gradient(135deg,#eff6ff 0%,#dbeafe 100%);border:1px solid #bfdbfe;border-radius:12px;padding:24px;text-align:center;'>
                <div style='font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#1d4ed8;margin-bottom:10px;'>Live Dashboard</div>
                <div style='font-size:20px;font-weight:700;color:#0f172a;margin-bottom:8px;'>View Real-Time Production Dashboard</div>
                <div style='font-size:13px;color:#475569;line-height:1.6;margin-bottom:20px;'>
                    Access live machine monitoring, downtime tracking, production KPIs,
                    waste analytics, and operational insights in real time.
                </div>
                <a href='https://tpltm.co.uk/Management_Dashboard'
                    style='display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:10px;font-size:14px;font-weight:600;box-shadow:0 4px 12px rgba(37,99,235,0.25);'>
                    Open Live Dashboard →
                </a>
            </div>
        </div>

        <!-- FOOTER -->
        <div style='background:#f8fafc;padding:24px 40px;text-align:center;border-top:1px solid #e5e7eb;'>
            <div style='font-size:12px;color:#9ca3af;line-height:1.8;'>
                This report was auto-generated by <strong style='color:#6b7280;'>TPL Production Systems</strong><br>
                {DateTime.Now:dd MMM yyyy, HH:mm} · Period: {period}<br>
                <em style='color:#c4b5fd;'>AI analysis powered by Claude (Anthropic via AWS Bedrock)</em>
            </div>
        </div>

    </div>
    </body>
    </html>";

                // ── 11. Send via Graph ────────────────────────────────────
                var recipients = await GetRecipientsByRoleAsync("Management");

                await SendEmailGraphAsync(
                    $"📊 TPL Production Report – {DateTime.Now:dd MMM yyyy HH:mm}",
                    html,
                    recipients);

                return Content($"✅ Report sent to {string.Join(", ", recipients)}");
            }
            catch (Exception ex)
            {
                return Content("❌ Email Error: " + ex.Message);
            }
        }

        // ── Parses plain-text AI report into styled insight blocks ──────
        private string BuildAiInsightBlocks(string rawReport)
        {
            var sb = new StringBuilder();
            var lines = rawReport.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Detect headings (lines starting with a digit + dot, or all-caps keywords)
                bool isHeading = (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.')
                                || trimmed.StartsWith("##")
                                || trimmed.StartsWith("**");

                if (isHeading)
                {
                    var clean = trimmed.TrimStart('#', '*', ' ').Trim();
                    sb.Append($@"
                    <div style='border-bottom:1px solid #334155;padding:10px 0 4px;'>
                        <div style='font-size:13px;font-weight:700;color:#f1f5f9;'>{WebUtility.HtmlEncode(clean)}</div>
                    </div>");
                }
                else
                {
                    // Tag detection
                    string tagHtml = "";
                    string tagColor = "#64748b";

                    if (trimmed.Contains("⚠️") || trimmed.Contains("underperform") || trimmed.Contains("issue") || trimmed.Contains("concern"))
                    {
                        tagHtml = "<span style='background:#450a0a;color:#fca5a5;font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;padding:2px 7px;border-radius:4px;margin-right:6px;'>ALERT</span>";
                        tagColor = "#fca5a5";
                    }
                    else if (trimmed.Contains("✅") || trimmed.Contains("well") || trimmed.Contains("good") || trimmed.Contains("achiev") || trimmed.Contains("exceed"))
                    {
                        tagHtml = "<span style='background:#052e16;color:#86efac;font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;padding:2px 7px;border-radius:4px;margin-right:6px;'>GOOD</span>";
                        tagColor = "#cbd5e1";
                    }
                    else
                    {
                        tagColor = "#cbd5e1";
                    }

                    sb.Append($@"
                    <div style='border-bottom:1px solid #334155;padding:10px 0;'>
                        <div style='font-size:13px;color:{tagColor};line-height:1.6;'>{tagHtml}{WebUtility.HtmlEncode(trimmed)}</div>
                    </div>");
                }
            }

            return sb.Length > 0 ? sb.ToString()
                : "<div style='font-size:13px;color:#64748b;padding:10px 0;'>No insights generated.</div>";
        }

        // ================================================================
        // GRAPH EMAIL SENDER (unchanged from your existing code)
        // ================================================================
        private async Task SendEmailGraphAsync(string subject, string htmlBody, string[] recipients)
        {
            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];
            var senderEmail = _configuration["AzureAd:SenderEmail"];

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var graphClient = new GraphServiceClient(credential);

            var message = new Microsoft.Graph.Models.Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                ToRecipients = recipients
                    .Select(email => new Recipient { EmailAddress = new EmailAddress { Address = email } })
                    .ToList()
            };

            await graphClient.Users[senderEmail]
                .SendMail
                .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                });
        }

        private async Task<string[]> GetRecipientsByRoleAsync(string role)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);

            return usersInRole
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email)
                .Distinct()
                .ToArray();
        }

        // ================================================================
        // MODELS
        // ================================================================
        public class ProductionInfo
        {
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public long Product_Count { get; set; }
            public DateTime? Timestamp_Start { get; set; }
            public string Shift { get; set; }
        }

        public class MachineLineSummary
        {
            public string Machine_Line { get; set; }
            public long Total_Count { get; set; }
        }

        public class ProductLineSummary
        {
            public string Product_Line { get; set; }
            public long Total_Count { get; set; }
        }

        public class ProductionTarget
        {
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public long Target_Count { get; set; }
        }

        public class DowntimeSummary
        {
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public string Shift { get; set; }
            public string Reason_Name { get; set; }
            public DateTime Created_On { get; set; }
            public int Total_Downtime { get; set; }
        }

        public class WasteSummary
        {
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public decimal Total_Waste { get; set; }
        }

        public class WasteDetail
        {
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public string WasteType { get; set; }
            public decimal WasteWeight { get; set; }
            public string Shift { get; set; }
            public DateTime Created_On { get; set; }
        }

        public class DowntimeDetail
        {
            public string Machine_Line { get; set; }
            public string Machine_Name { get; set; }
            public string Product_Line { get; set; }
            public string Shift { get; set; }
            public string Reason { get; set; }
            public int Downtime { get; set; }
            public DateTime Created_On { get; set; }
        }

        public class AiSummaryDto
        {
            public string machine { get; set; }
            public string product { get; set; }
            public long total { get; set; }
            public long target { get; set; }
            public decimal achievement { get; set; }
            public int downtime { get; set; }
            public decimal waste { get; set; }
        }

        // ── Used only inside OnGetSendAIEmailAsync to carry full data ───
        private class EmailReportDto
        {
            public string machine_Line { get; set; }
            public string machine_name { get; set; }
            public string product_Line { get; set; }
            public long total { get; set; }
            public long target { get; set; }
            public decimal achievement { get; set; }
            public int downtime { get; set; }
            public decimal waste { get; set; }
            public List<EmailProductionRow> data { get; set; }
            public List<EmailDowntimeRow> downtimeData { get; set; }
            public List<EmailWasteRow> wasteData { get; set; }
        }

        private class EmailProductionRow
        {
            public long product_Count { get; set; }
            public string shift { get; set; }
        }

        private class EmailDowntimeRow
        {
            public string reason_Name { get; set; }
            public int total_Downtime { get; set; }
        }

        private class EmailWasteRow
        {
            public string wasteType { get; set; }
            public decimal wasteWeight { get; set; }
        }
    }
    
}

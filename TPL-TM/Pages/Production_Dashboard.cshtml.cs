using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    public class Production_DashboardModel : PageModel
    {
        public string errorMessage = "";
        private readonly IConfiguration _configuration;

        public Production_DashboardModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 1. Last 24 hours detailed records
        public List<ProductionInfo> listProduction { get; set; } = new();

        // 2. Last 24 hours summary by machine + shift
        public List<ProductionSummary> listSummary { get; set; } = new();

        // 3. Last 7 days summary by day + shift
        public List<DailyShiftSummary> listDailySummary { get; set; } = new();
        public List<ProductionTargetSummary> listTargetSummary { get; set; } = new();


        [BindProperty(SupportsGet = true)]
        public DateTime? FilterDate { get; set; }
        public void OnGet()
        {
            try
            {
                listProduction.Clear();

                // Default to yesterday if no date selected
                if (!FilterDate.HasValue)
                    FilterDate = DateTime.Today.AddDays(-1);

                var selectedDate = FilterDate.Value.Date;
                var nextDay = selectedDate.AddDays(1);

               
                // --- Load from production_count (Default DB) ---
                string connDefault = _configuration.GetConnectionString("DefaultConnection");
                using (var connectionDefault = new NpgsqlConnection(connDefault))
                {
                    connectionDefault.Open();
                    var prodCountData = LoadProductionFromTable(connectionDefault);
                    listProduction.AddRange(prodCountData);
                }
                // --- Load Production Targets ---
                List<ProductionTarget> listTargets = new();
                using (var connectionDefault = new NpgsqlConnection(connDefault))
                {
                    connectionDefault.Open();
                    listTargets = LoadProductionTargets(connectionDefault);
                }


                // --- Apply date filter (safe range) ---
                listProduction = listProduction
                    .Where(x => x.Timestamp_Start.HasValue &&
                                x.Timestamp_Start.Value >= selectedDate &&
                                x.Timestamp_Start.Value < nextDay)
                    .ToList();

                // --- Build summaries ---
                BuildMachineSummary();
                BuildDailySummary();
                BuildTargetSummary(listTargets);


                // Debug info
                errorMessage = $"Loaded {listProduction.Count} rows from production_count. " +
                               $"Machine summaries = {listSummary.Count}, " +
                               $"Daily summaries = {listDailySummary.Count}.";
            }
            catch (Exception ex)
            {
                errorMessage = "ERROR: " + ex.Message;
            }
        }
        private static string NormalizeMachineLine(string machineLine)
        {
            if (string.IsNullOrWhiteSpace(machineLine))
                return "";

            machineLine = machineLine.Trim().ToUpper();

            return machineLine switch
            {
                "LID" or "LIDS" or "Lids" => "LIDS",
                "CUP" or "CUPS" or "Cups" => "CUPS",
                "QSR" or "Straws" or "STRAWS" => "STRAWS",
                _ => machineLine
            };
        }


        private List<ProductionInfo> LoadProductionFromTable(NpgsqlConnection connection)
        {
            string sql = @"
        SELECT
            id,
            wo_number,
            shift_letter,
            timestamp_start,
            timestamp_end,
            created_by AS username,
            product_count,
            machine_line,
            machine_name,
            product_line,
            part_number,
            item_description
        FROM production_count
        WHERE timestamp_start >= NOW() - INTERVAL '7 days'
        ORDER BY timestamp_start DESC;
    ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            var results = new List<ProductionInfo>();

            while (reader.Read())
            {
                results.Add(new ProductionInfo
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Shift_Letter = reader["shift_letter"]?.ToString() ?? "",
                    Timestamp_Start = reader["timestamp_start"] as DateTime?,
                    Timestamp_End = reader["timestamp_end"] as DateTime?,
                    Username = reader["username"]?.ToString() ?? "",
                    Product_Count = Convert.ToInt64(reader["product_count"]),
                    Machine_Line = NormalizeMachineLine(reader["machine_line"]?.ToString() ?? ""),
                    Machine_Name = reader["machine_name"]?.ToString() ?? "",
                    Product_Line = reader["product_line"]?.ToString() ?? "",
                    TimestampStartDisplay = reader["timestamp_start"] is DBNull
                        ? ""
                        : Convert.ToDateTime(reader["timestamp_start"]).ToString("dd/MM/yyyy HH:mm:ss"),
                    TimestampEndDisplay = reader["timestamp_end"] is DBNull
                        ? ""
                        : Convert.ToDateTime(reader["timestamp_end"]).ToString("dd/MM/yyyy HH:mm:ss")
                });
            }

            return results;
        }

        private void BuildMachineSummary()
        {
            listSummary = listProduction
                .GroupBy(x => x.Machine_Line)
                .Select(g => new ProductionSummary
                {
                    Machine_Line = g.Key,
                    Total_Product_Count = g.Sum(x => x.Product_Count),
                    ShiftSummaries = g.GroupBy(x => x.Shift_Letter)
                                      .Select(s => new ShiftSummary
                                      {
                                          Shift_Letter = s.Key,
                                          Total_Product_Count = s.Sum(x => x.Product_Count)
                                      }).ToList()
                })
                .ToList();
        }

        private void BuildDailySummary()
        {
            listDailySummary = listProduction
             .Where(x => x.Timestamp_Start.HasValue)
             .GroupBy(x => new { x.Machine_Line, Date = x.Timestamp_Start.Value.Date, x.Shift_Letter, x.Product_Line, x.Machine_Name })
             .Select(g => new DailyShiftSummary
             {
                 Machine_Line = g.Key.Machine_Line,
                 Machine_Name = g.Key.Machine_Name,       // <-- ADD THIS
                 Production_Date = g.Key.Date,
                 Day_Name = g.Key.Date.ToString("dddd"),
                 Shift_Letter = g.Key.Shift_Letter,
                 Product_Line = g.Key.Product_Line,
                 Total_Product_Count = g.Sum(x => x.Product_Count)
             })
             .OrderBy(x => x.Machine_Line)
             .ThenBy(x => x.Production_Date)
             .ThenBy(x => x.Shift_Letter)
             .ThenBy(x => x.Product_Line)
             .ToList();
        }
        private List<ProductionTarget> LoadProductionTargets(NpgsqlConnection connection)
        {
            const string sql = @"
                SELECT id, machine_line, machine_name, product_line, target_count, effective_date, is_active
                FROM production_target
                WHERE is_active = TRUE;
            ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            var results = new List<ProductionTarget>();
            while (reader.Read())
            {
                results.Add(new ProductionTarget
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Machine_Line = NormalizeMachineLine(
                        reader.GetString(reader.GetOrdinal("machine_line"))
                    ),
                    Machine_Name = reader.GetString(reader.GetOrdinal("machine_name")),
                    Product_Line = reader.GetString(reader.GetOrdinal("product_line")),
                    Target_Count = reader.GetInt64(reader.GetOrdinal("target_count")),
                    Effective_Date = reader.IsDBNull(reader.GetOrdinal("effective_date"))
                        ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("effective_date")),
                    Is_Active = reader.GetBoolean(reader.GetOrdinal("is_active"))
                });
            }

            return results;
        }
        private void BuildTargetSummary(List<ProductionTarget> listTargets)
        {
            var summary = from target in listTargets
                          join prod in listProduction
                              on new { target.Machine_Name, target.Product_Line }
                              equals new { prod.Machine_Name, prod.Product_Line } into prodGroup
                          from pg in prodGroup.DefaultIfEmpty()
                          group pg by new
                          {
                              target.Machine_Line,
                              target.Machine_Name
                          } into g
                          select new ProductionTargetSummary
                          {
                              Machine_Line = g.Key.Machine_Line,
                              Machine_Name = g.Key.Machine_Name,

                              // ? Sum all active product line targets * 3 shifts
                              Target_Count = listTargets
                                  .Where(t => t.Machine_Name == g.Key.Machine_Name)
                                  .Sum(t => t.Target_Count),

                              // ? Actual units
                              Actual_Count = g.Where(x => x != null).Sum(x => x.Product_Count)
                          };

            listTargetSummary = summary
                .OrderBy(x => x.Machine_Line)
                .ThenBy(x => x.Machine_Name)
                .ToList();
        }
    }
}

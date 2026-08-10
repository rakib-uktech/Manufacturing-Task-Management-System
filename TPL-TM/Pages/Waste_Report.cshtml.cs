using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Waste_ReportModel : PageModel
    {
        public List<TaskInfo> WasteList { get; set; } = new List<TaskInfo>();
        private readonly IConfiguration _configuration;

        public Waste_ReportModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            try
            {
                string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");
                string OdbcConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");

                // ✅ Step 1: Load all Bin data into Dictionary (ODBC only once)
                Dictionary<string, (string Location, string Type, string Description, decimal Weight)> binData = new();

                using (OdbcConnection odbc = new OdbcConnection(OdbcConnectionString))
                {
                    odbc.Open();
                    string binSql = @"
                SELECT a.binnumber AS binname, 
                       b.name AS wastelocation,
                       c.name AS wastetype,
                       d.name AS wastedescription,
                       custrecord166 AS binweight
                FROM bin a
                INNER JOIN CUSTOMLIST1425 b ON a.custrecord163 = b.id
                INNER JOIN CUSTOMLIST1426 c ON a.custrecord164 = c.id
                INNER JOIN CUSTOMLIST1427 d ON a.custrecord165 = d.id";

                    using (OdbcCommand binCmd = new OdbcCommand(binSql, odbc))
                    using (var binReader = binCmd.ExecuteReader())
                    {
                        while (binReader.Read())
                        {
                            string binName = binReader["binname"].ToString();
                            binData[binName] = (
                                binReader["wastelocation"].ToString(),
                                binReader["wastetype"].ToString(),
                                binReader["wastedescription"].ToString(),
                                Convert.ToDecimal(binReader["binweight"])
                            );
                        }
                    }
                }

                // ✅ Step 2: Fetch all tasks from PostgreSQL
                using (var connection = new NpgsqlConnection(DefaultConnection))
                {
                    connection.Open();
                    string sql = "SELECT * FROM task_manager WHERE Task_Type = 'Clear Waste' AND Status = 'C'";

                    using (var command = new NpgsqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var taskinfo = new TaskInfo
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Ref_No = reader.GetString(reader.GetOrdinal("Ref_No")),
                                Assign_To = reader.GetString(reader.GetOrdinal("Assign_To")), // BinName
                                Qty = reader.GetInt32(reader.GetOrdinal("Qty")),
                                Unit = reader.GetString(reader.GetOrdinal("Unit")),
                                Completed_On = reader.IsDBNull(reader.GetOrdinal("Complete_On")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Complete_On")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                Priority_Level = reader.GetString(reader.GetOrdinal("Priority_Level")),
                                Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by")) ? "N/A" : reader.GetString(reader.GetOrdinal("Created_by")),
                                Completed_by = reader.IsDBNull(reader.GetOrdinal("Completed_by")) ? "N/A" : reader.GetString(reader.GetOrdinal("Completed_by")),
                                Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? "" : reader.GetString(reader.GetOrdinal("Comments"))
                            };
                            // Handle nullable timestamp fields
                            taskinfo.Created_On = reader.IsDBNull(reader.GetOrdinal("Create_On")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Create_On"));
                            taskinfo.Completed_On = reader.IsDBNull(reader.GetOrdinal("Complete_On")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Complete_On"));

                            // Convert nullable DateTime to formatted string for display (include seconds)
                            taskinfo.Create_On = taskinfo.Created_On?.ToString("dd/MM/yyyy HH:mm:ss") ?? "";
                            taskinfo.Complete_On = taskinfo.Completed_On?.ToString("dd/MM/yyyy HH:mm:ss") ?? "";

                            // Calculate task duration in hours, minutes, and seconds
                            if (taskinfo.Created_On.HasValue && taskinfo.Completed_On.HasValue)
                            {
                                TimeSpan duration = taskinfo.Completed_On.Value - taskinfo.Created_On.Value;
                                int hours = (int)duration.TotalHours;
                                int minutes = duration.Minutes;
                                int seconds = duration.Seconds;
                                taskinfo.Duration = $"{hours} hrs {minutes} mins {seconds} secs";
                            }
                            else
                            {
                                taskinfo.Duration = "-";
                            }

                            // ✅ Step 3: Join via Dictionary
                            if (binData.TryGetValue(taskinfo.Assign_To, out var bin))
                            {
                                taskinfo.WasteLocation = bin.Location;
                                taskinfo.WasteType = bin.Type;
                                taskinfo.WasteDescription = bin.Description;
                                taskinfo.BinWeight = bin.Weight;
                            }

                            WasteList.Add(taskinfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
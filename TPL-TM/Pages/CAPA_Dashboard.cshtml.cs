using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class CAPA_DashboardModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public int TotalCAPA { get; set; }
        public int OpenCAPA { get; set; }
        public int InvestigationCAPA { get; set; }
        public int VerificationCAPA { get; set; }
        public int ClosedCAPA { get; set; }
        public int OverdueCAPA { get; set; }
        public int CriticalSeverity { get; set; }
        public int HighSeverity { get; set; }
        public int MediumSeverity { get; set; }
        public int LowSeverity { get; set; }

        public List<CAPAInfo> RecentCAPA { get; set; } = new();
        public List<string> MachineLabels { get; set; } = new();
        public List<int> MachineCounts { get; set; } = new();

        public List<string> IncidentLabels { get; set; } = new();
        public List<int> IncidentCounts { get; set; } = new();

        public CAPA_DashboardModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            string connString =
                _configuration.GetConnectionString("DefaultConnection");

            using var con = new NpgsqlConnection(connString);

            con.Open();

            TotalCAPA =
                GetCount(con,
                "SELECT COUNT(*) FROM capa_master");

            OpenCAPA =
                GetCount(con,
                "SELECT COUNT(*) FROM capa_master WHERE status='Open'");

            InvestigationCAPA =
                GetCount(con,
                "SELECT COUNT(*) FROM capa_master WHERE status='Investigation'");

            VerificationCAPA =
                GetCount(con,
                "SELECT COUNT(*) FROM capa_master WHERE status='Verification'");

            ClosedCAPA =
                GetCount(con,
                "SELECT COUNT(*) FROM capa_master WHERE status='Closed'");

            OverdueCAPA =
                GetCount(con,
                @"SELECT COUNT(*)
                  FROM capa_master
                  WHERE target_date < CURRENT_DATE
                  AND status <> 'Closed'");
            
            CriticalSeverity =            
            GetCount(con,
                "SELECT COUNT(*) FROM capa_master WHERE severity='Critical'");

                    HighSeverity =
                        GetCount(con,
                        "SELECT COUNT(*) FROM capa_master WHERE severity='Major'");

                    MediumSeverity =
                        GetCount(con,
                        "SELECT COUNT(*) FROM capa_master WHERE severity='Minor'");

                    LowSeverity =
                        GetCount(con,
                        "SELECT COUNT(*) FROM capa_master WHERE severity='Low'");

            LoadRecentCAPA(con);
            LoadMachineStats(con);
            LoadIncidentStats(con);
        }

        private int GetCount(
            NpgsqlConnection con,
            string sql)
        {
            using var cmd = new NpgsqlCommand(sql, con);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private void LoadRecentCAPA(
            NpgsqlConnection con)
        {
            string sql = @"
                SELECT *
                FROM capa_master
                ORDER BY create_on DESC
                LIMIT 10";

            using var cmd = new NpgsqlCommand(sql, con);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                RecentCAPA.Add(new CAPAInfo
                {
                    Id = Convert.ToInt32(reader["id"]),
                    CAPA_No = reader["capa_no"].ToString(),
                    Title = reader["title"].ToString(),
                    Severity = reader["severity"].ToString(),
                    Status = reader["status"].ToString(),
                    Machine_Name = reader["machine_name"].ToString()
                });
            }
        }
        private void LoadMachineStats(NpgsqlConnection con)
        {
            string sql = @"
        SELECT machine_name,
               COUNT(*) total
        FROM capa_master
        WHERE machine_name IS NOT NULL
        GROUP BY machine_name
        ORDER BY total DESC
        LIMIT 10";

            using var cmd = new NpgsqlCommand(sql, con);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                MachineLabels.Add(reader["machine_name"].ToString());
                MachineCounts.Add(Convert.ToInt32(reader["total"]));
            }
        }
        private void LoadIncidentStats(NpgsqlConnection con)
        {
            string sql = @"
        SELECT incident_type,
               COUNT(*) total
        FROM capa_master
        WHERE incident_type IS NOT NULL
        GROUP BY incident_type
        ORDER BY total DESC";

            using var cmd = new NpgsqlCommand(sql, con);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                IncidentLabels.Add(reader["incident_type"].ToString());
                IncidentCounts.Add(Convert.ToInt32(reader["total"]));
            }
        }
    }
}
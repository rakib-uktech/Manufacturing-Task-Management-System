using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class DowntimeReasonListModel : PageModel
    {
        private readonly IConfiguration _config;

        public DowntimeReasonListModel(IConfiguration config)
        {
            _config = config;
        }

        public List<DowntimeReasonInfo> DowntimeReasonList { get; set; } = new();
        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, reason_name, description, active, Machine_Type, created_on, created_by
                    FROM downtime_reason
                    ORDER BY id ASC;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    DowntimeReasonList.Add(new DowntimeReasonInfo
                    {
                        Id = reader.GetInt64(0),
                        ReasonName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Active = reader.IsDBNull(3) ? false : reader.GetBoolean(3),
                        Machine_Type = reader.IsDBNull(4) ? "" : reader.GetString(4), // <-- Bind Machine_Type
                        CreatedOn = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                        CreatedBy = reader.IsDBNull(6) ? "" : reader.GetString(6)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error loading downtime reasons: " + ex.Message;
            }
        }

        public class DowntimeReasonInfo
        {
            public long Id { get; set; }
            public string ReasonName { get; set; }
            public string Description { get; set; }
            public bool Active { get; set; }
            public string Machine_Type { get; set; } // <-- Added
            public DateTime? CreatedOn { get; set; }
            public string CreatedBy { get; set; }
        }
    }
}

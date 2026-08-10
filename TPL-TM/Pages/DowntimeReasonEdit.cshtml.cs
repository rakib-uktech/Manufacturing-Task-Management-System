using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class DowntimeReasonEditModel : PageModel
    {
        private readonly IConfiguration _config;

        public DowntimeReasonEditModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public DowntimeReasonInfo Reason { get; set; } = new();

        public List<string> MachineTypes { get; set; } = new(); // Dropdown for Machine Types
        public string ErrorMessage { get; set; } = "";
        public string SuccessMessage { get; set; } = "";

        // Load existing downtime reason and machine types
        public async Task OnGetAsync(long id)
        {
            try
            {
                // Load Machine Types from NetSuite
                string connStr = _config.GetConnectionString("NetSuiteOdbc");
                using var connOdbc = new OdbcConnection(connStr);
                await connOdbc.OpenAsync();

                string sqlMachines = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";
                using var cmdMachines = new OdbcCommand(sqlMachines, connOdbc);
                using var readerMachines = await cmdMachines.ExecuteReaderAsync();
                while (await readerMachines.ReadAsync())
                {
                    MachineTypes.Add(readerMachines["name"]?.ToString());
                }

                // Load the selected downtime reason
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string sql = @"
                    SELECT id, reason_name, description, active, Machine_Type
                    FROM downtime_reason
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Reason.Id = reader.GetInt64(0);
                    Reason.ReasonName = reader.GetString(1);
                    Reason.Description = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    Reason.Active = reader.IsDBNull(3) ? false : reader.GetBoolean(3);
                    Reason.Machine_Type = reader.IsDBNull(4) ? "" : reader.GetString(4);
                }
                else
                {
                    ErrorMessage = "Downtime reason not found.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error: " + ex.Message;
            }
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors and try again.";
                return Page();
            }

            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    UPDATE downtime_reason
                    SET reason_name = @name,
                        description = @desc,
                        active = @active,
                        Machine_Type = @machineType
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@name", Reason.ReasonName);
                cmd.Parameters.AddWithValue("@desc", Reason.Description ?? "");
                cmd.Parameters.AddWithValue("@active", Reason.Active);
                cmd.Parameters.AddWithValue("@machineType", Reason.Machine_Type ?? "");
                cmd.Parameters.AddWithValue("@id", Reason.Id);

                cmd.ExecuteNonQuery();

                SuccessMessage = "Downtime reason updated successfully!";
                return RedirectToPage("/DowntimeReasonList");
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Error updating downtime reason: " + ex.Message;
                return Page();
            }
        }

        public class DowntimeReasonInfo
        {
            public long Id { get; set; }
            public string ReasonName { get; set; }
            public string Description { get; set; }
            public bool Active { get; set; }
            public string Machine_Type { get; set; } // Added Machine_Type
        }
    }
}

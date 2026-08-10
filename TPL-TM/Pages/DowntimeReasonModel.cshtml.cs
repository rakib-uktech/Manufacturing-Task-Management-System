using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class DowntimeReasonModel : PageModel
    {
        private readonly IConfiguration _config;

        public DowntimeReasonModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public string ReasonName { get; set; }

        [BindProperty]
        public string Description { get; set; }

        [BindProperty]
        public bool Active { get; set; } = true;

        [BindProperty]
        public string Machine_Type { get; set; }  // Selected Machine Type

        public List<string> MachineTypes { get; set; } = new(); // Dropdown options

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Load Machine Types from NetSuite via ODBC
                string connStr = _config.GetConnectionString("NetSuiteOdbc");
                using var conn = new OdbcConnection(connStr);
                await conn.OpenAsync();

                string sql = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";
                using var cmd = new OdbcCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    MachineTypes.Add(reader["name"]?.ToString());
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load machine types: {ex.Message}";
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ReasonName))
                {
                    ErrorMessage = "Reason name is required.";
                    return Page();
                }

                using var con = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                con.Open();

                string sql = @"
                    INSERT INTO downtime_reason
                        (reason_name, description, active, Machine_Type, created_on, created_by)
                    VALUES
                        (@reason, @desc, @active, @machineType, NOW(), @createdBy)
                ";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@reason", ReasonName);
                cmd.Parameters.AddWithValue("@desc", Description ?? "");
                cmd.Parameters.AddWithValue("@active", Active);
                cmd.Parameters.AddWithValue("@machineType", Machine_Type ?? "");
                cmd.Parameters.AddWithValue("@createdBy", User.Identity?.Name ?? "system");

                cmd.ExecuteNonQuery();

                SuccessMessage = "Downtime reason added successfully!";
                return RedirectToPage("/DowntimeReasonList");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                return Page();
            }
        }
    }
}

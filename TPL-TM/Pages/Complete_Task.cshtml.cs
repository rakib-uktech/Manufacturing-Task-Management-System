using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;
using TPL_TM.Pages;

[Authorize]
public class Complete_TaskModel : PageModel
{
    public string errorMessage = "";
    public string successMessage = "";
    public TaskInfo taskinfo = new TaskInfo();
    private readonly IConfiguration _configuration;
    private readonly UserManager<IdentityUser> _userManager;

    public Complete_TaskModel(IConfiguration configuration, UserManager<IdentityUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public List<TaskInfo> listtask { get; set; } = new List<TaskInfo>();
    public int item_id;
    public int req_id;

    public void OnGet()
    {
        string id = Request.Query["id"];
        if (!String.IsNullOrEmpty(id))
        {
            item_id = Int32.Parse(id);
        }
        else
        {
            item_id = 1;
        }

        try
        {
            // Step 1: Fetch task info from PostgreSQL
            string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");

            using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
            {
                connection.Open();
                string sql = "SELECT * FROM task_manager WHERE id = @id;";
                using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", item_id);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            taskinfo.Id = reader.GetInt32(reader.GetOrdinal("id"));
                            taskinfo.Ref_No = reader.IsDBNull(reader.GetOrdinal("Ref_No")) ? "" : reader.GetString(reader.GetOrdinal("Ref_No"));
                            taskinfo.Task_Type = reader.IsDBNull(reader.GetOrdinal("Task_Type")) ? "" : reader.GetString(reader.GetOrdinal("Task_Type"));
                            taskinfo.Task_Name = reader.IsDBNull(reader.GetOrdinal("Task_Name")) ? "" : reader.GetString(reader.GetOrdinal("Task_Name"));
                            taskinfo.Task_Description = reader.IsDBNull(reader.GetOrdinal("Task_Description")) ? "" : reader.GetString(reader.GetOrdinal("Task_Description"));
                            taskinfo.Assign_To = reader.IsDBNull(reader.GetOrdinal("Assign_To")) ? "" : reader.GetString(reader.GetOrdinal("Assign_To"));
                            taskinfo.Qty = reader.IsDBNull(reader.GetOrdinal("Qty")) ? 0 : reader.GetInt32(reader.GetOrdinal("Qty"));
                            taskinfo.Unit = reader.IsDBNull(reader.GetOrdinal("Unit")) ? "" : reader.GetString(reader.GetOrdinal("Unit"));
                            taskinfo.Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "" : reader.GetString(reader.GetOrdinal("Status"));
                            taskinfo.Priority_Level = reader.IsDBNull(reader.GetOrdinal("Priority_Level")) ? "" : reader.GetString(reader.GetOrdinal("Priority_Level"));
                            taskinfo.Create_On = reader.IsDBNull(reader.GetOrdinal("Create_On")) ? "" : reader.GetDateTime(reader.GetOrdinal("Create_On")).ToString("dd/MM/yyyy HH:mm:ss");
                            taskinfo.Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by")) ? "N/A" : reader.GetString(reader.GetOrdinal("Created_by"));
                            taskinfo.Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? "" : reader.GetString(reader.GetOrdinal("Comments"));
                        }
                    }
                }
            }

            // Step 2: Get latitude/longitude from NetSuite (machine_location table)
            string odbcConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");

            using (OdbcConnection odbcConnection = new OdbcConnection(odbcConnectionString))
            {
                odbcConnection.Open();

                string odbcSql;
                string paramName;
                string latField;
                string lngField;

                if (taskinfo.Task_Type == "Clear Waste")
                {
                    odbcSql = @"
                                SELECT custrecordlatitude, custrecordlongitude
                                FROM bin
                                WHERE binnumber = ?";
                    paramName = "binnumber";
                    latField = "custrecordlatitude";
                    lngField = "custrecordlongitude";
                }
                else
                {
                    odbcSql = @"
                                SELECT custentitylatitude, custentitylongitude
                                FROM entitygroup
                                WHERE groupname = ?";
                    paramName = "groupname";
                    latField = "custentitylatitude";
                    lngField = "custentitylongitude";
                }

                using (OdbcCommand command = new OdbcCommand(odbcSql, odbcConnection))
                {
                    // Use "?" for ODBC placeholders (no named parameters)
                    command.Parameters.AddWithValue("?", taskinfo.Assign_To);

                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            taskinfo.Latitude = reader.IsDBNull(reader.GetOrdinal(latField))
                                ? 0
                                : reader.GetDouble(reader.GetOrdinal(latField));

                            taskinfo.Longitude = reader.IsDBNull(reader.GetOrdinal(lngField))
                                ? 0
                                : reader.GetDouble(reader.GetOrdinal(lngField));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return;
        }
    }



    public async Task<IActionResult> OnPostCompleteTaskAsync(int TaskId, decimal Qty, string? Comments)
    {
        try
        {
            string DefaultConnection = _configuration.GetConnectionString("DefaultConnection");
            var signedInUser = await _userManager.GetUserAsync(User);

            using (NpgsqlConnection connection = new NpgsqlConnection(DefaultConnection))
            {
                connection.Open();

                string sql = @"
            UPDATE task_manager
            SET Status = 'C',  
                Complete_On = NOW(),  
                Qty = @quantity,
                Completed_by = @completed_by, 
                Comments = @comments 
            WHERE id = @id;";

                using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", TaskId);
                    command.Parameters.AddWithValue("@quantity", Qty); // Add this line
                    command.Parameters.AddWithValue("@completed_by", signedInUser?.UserName ?? "Unknown");
                    command.Parameters.AddWithValue("@comments", string.IsNullOrEmpty(Comments) ? DBNull.Value : (object)Comments);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        TempData["SuccessMessage"] = "Task has been marked as completed successfully!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Task completion failed! Please try again.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToPage("/Index");
    }

}

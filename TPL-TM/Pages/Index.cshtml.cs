using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages;

[Authorize]
public class IndexModel : PageModel
{
    public string errorMessage = "";
    public string successMessage = "";
    public TaskInfo taskinfo = new TaskInfo();

    private readonly IConfiguration _configuration;
    private readonly UserManager<IdentityUser> _userManager;

    public List<TaskInfo> assign_taskCount { get; set; } = new();
    public List<TaskInfo> request_inventoryCount { get; set; } = new();
    public List<TaskInfo> move_itemCount { get; set; } = new();
    public List<TaskInfo> clear_wasteCount { get; set; } = new();
    public List<TaskInfo> quarantine_itemCount { get; set; } = new();
    public List<TaskInfo> listtask { get; set; } = new();

    public Dictionary<string, int> AssignToCounts { get; set; } = new();
    public Dictionary<string, int> TaskTypeCounts { get; set; } = new();
    public Dictionary<string, int> TaskStatusCounts { get; set; } = new();
    public Dictionary<string, int> TaskQtyCounts { get; set; } = new();

    public int HighPriorityCount { get; set; }
    public int MediumPriorityCount { get; set; }
    public int LowPriorityCount { get; set; }

    public IndexModel(IConfiguration configuration, UserManager<IdentityUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // ✅ Redirect based on role
        var user = await _userManager.GetUserAsync(User);
        if (user != null && await _userManager.IsInRoleAsync(user, "User"))
        {
            return RedirectToPage("/Operator_Index");
        }

        try
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // === Task Counts ===
            assign_taskCount = ExecuteCountQuery(connection, "Assembly Build");
            request_inventoryCount = ExecuteCountQuery(connection, "Request Inventory");
            move_itemCount = ExecuteCountQuery(connection, "Move Item");
            clear_wasteCount = ExecuteCountQuery(connection, "Clear Waste");
            quarantine_itemCount = ExecuteCountQuery(connection, "Quarantine Item");

            // === Task Details ===
            string sql = @"
                SELECT *
                FROM task_manager
                WHERE create_on >= NOW() - INTERVAL '30 days'
                ORDER BY create_on DESC";
            using var command = new NpgsqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            var assignToCounts = new Dictionary<string, int>();
            var taskTypeCounts = new Dictionary<string, int>();
            var taskQtyCounts = new Dictionary<string, int>();
            var taskStatusCounts = new Dictionary<string, int>();

            while (reader.Read())
            {
                TaskInfo taskinfo = new TaskInfo
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Create_On = reader.IsDBNull(reader.GetOrdinal("Create_On"))
                        ? ""
                        : reader.GetDateTime(reader.GetOrdinal("Create_On")).ToString("dd/MM/yyyy"),
                    Ref_No = reader.GetString(reader.GetOrdinal("Ref_No")),
                    Task_Type = reader.GetString(reader.GetOrdinal("Task_Type")),
                    Task_Name = reader.GetString(reader.GetOrdinal("Task_Name")),
                    Task_Description = reader.GetString(reader.GetOrdinal("Task_Description")),
                    Assign_To = reader.GetString(reader.GetOrdinal("Assign_To")),
                    Qty = reader.GetInt32(reader.GetOrdinal("Qty")),
                    Unit = reader.GetString(reader.GetOrdinal("Unit")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    Priority_Level = reader.GetString(reader.GetOrdinal("Priority_Level")),
                    Complete_On = reader.IsDBNull(reader.GetOrdinal("Complete_On"))
                        ? ""
                        : reader.GetDateTime(reader.GetOrdinal("Complete_On")).ToString("dd/MM/yyyy"),
                    Created_by = reader.IsDBNull(reader.GetOrdinal("Created_by"))
                        ? "N/A"
                        : reader.GetString(reader.GetOrdinal("Created_by")),
                    Completed_by = reader.IsDBNull(reader.GetOrdinal("Completed_by"))
                        ? "N/A"
                        : reader.GetString(reader.GetOrdinal("Completed_by")),
                    Comments = reader.IsDBNull(reader.GetOrdinal("Comments"))
                        ? ""
                        : reader.GetString(reader.GetOrdinal("Comments"))
                };

                // Count tracking
                IncrementCount(assignToCounts, taskinfo.Assign_To);
                IncrementCount(taskTypeCounts, taskinfo.Task_Type);
                IncrementCount(taskQtyCounts, taskinfo.Task_Name, taskinfo.Qty);
                IncrementCount(taskStatusCounts, taskinfo.Status);

                // Priority levels
                if (taskinfo.Priority_Level == "High") HighPriorityCount++;
                else if (taskinfo.Priority_Level == "Medium") MediumPriorityCount++;
                else if (taskinfo.Priority_Level == "Low") LowPriorityCount++;

                listtask.Add(taskinfo);
            }

            AssignToCounts = assignToCounts;
            TaskTypeCounts = taskTypeCounts;
            TaskStatusCounts = taskStatusCounts;
            TaskQtyCounts = taskQtyCounts;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        return Page();
    }

    // === Helper Methods ===
    private List<TaskInfo> ExecuteCountQuery(NpgsqlConnection connection, string taskType)
    {
        string sql = $@"
    SELECT COUNT(*) AS task_count
    FROM task_manager
    WHERE task_type = '{taskType}'
      AND status = 'A'
      AND create_on >= NOW() - INTERVAL '30 days'";

        var list = new List<TaskInfo>();

        using var command = new NpgsqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            list.Add(new TaskInfo
            {
                Assign_TaskCount = reader.GetInt32(reader.GetOrdinal("task_count"))
            });
        }

        return list;
    }

    private void IncrementCount(Dictionary<string, int> dict, string key, int value = 1)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (dict.ContainsKey(key)) dict[key] += value;
        else dict[key] = value;
    }
}

public class TaskInfo
{
    public int Assign_TaskCount { get; set; }
    public int Request_InventoryCount { get; set; }
    public int Move_ItemCount { get; set; }
    public int Clear_WasetCount { get; set; }
    public int Quarantine_ItemCount { get; set; }

    public int Id { get; set; }
    public string Create_On { get; set; }
    public string Ref_No { get; set; }
    public string Task_Type { get; set; }
    public string Task_Name { get; set; }
    public string Task_Description { get; set; }
    public string GTIN { get; set; }
    public string Product_Line { get; set; }
    public string ProductType { get; set; }
    public string Shift_Letter { get; set; }
    public string Assign_To { get; set; }
    public string BinName { get; set; }
    public string WasteLocation { get; set; }
    public string WasteType { get; set; }
    public string WasteDescription { get; set; }
    public decimal BinWeight { get; set; }
    public int Qty { get; set; }
    public string Unit { get; set; }
    public string Status { get; set; }
    public string Priority_Level { get; set; }
    public string Complete_On { get; set; }
    public string Created_by { get; set; }
    public string Completed_by { get; set; }
    public string Comments { get; set; }
    public string Inspection_Comments { get; set; }

    public DateTime? Created_On { get; set; }
    public DateTime? Completed_On { get; set; }
    public DateTime? Task_Date { get; set; }
    public TimeSpan? Task_StartTime { get; set; }
    public TimeSpan? Task_EndTime { get; set; }
    public string Duration { get; set; }
    public DateTime? Task_EndDate { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
 
}

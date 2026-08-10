using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql; // For PostgreSQL task data
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Data.Odbc; // For ODBC machine data (NetSuite)

namespace TPL_TM.Pages
{
    public class ScheduleCalendarModel : PageModel
    {
        public string errorMessage = "";
        public string successMessage = "";
        private readonly IConfiguration _configuration;

        [BindProperty(SupportsGet = true)]
        public DateTime SelectedStartDate { get; set; } = DateTime.Today;

        // This property holds the value selected from the machine dropdown
        [BindProperty(SupportsGet = true)]
        public string SelectedMachine { get; set; } = "All"; // Default to "All"

        public List<TaskInfo> ScheduledTasks { get; set; } = new();

        // This list populates the machine dropdown
        public List<string> AvailableMachines { get; set; } = new();

        public ScheduleCalendarModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet(DateTime? startDate, string selectedMachine)
        {
            if (startDate.HasValue)
            {
                SelectedStartDate = startDate.Value;
            }
            if (!string.IsNullOrEmpty(selectedMachine))
            {
                SelectedMachine = selectedMachine;
            }
            // Always load tasks and machines on GET requests
            LoadScheduledTasksAndMachines();
        }

        public IActionResult OnPost()
        {
            // SelectedStartDate and SelectedMachine are automatically bound here from the form submission
            // Now, call the method to load data with the updated bound properties
            LoadScheduledTasksAndMachines();
            return Page();
        }

        private void LoadScheduledTasksAndMachines()
        {
            try
            {
                // --- Part 1: Load Available Machines (from NetSuite via ODBC) ---
                string odbcConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");
                using (OdbcConnection odbcConnection = new OdbcConnection(odbcConnectionString))
                {
                    odbcConnection.Open();
                    AvailableMachines.Clear();
                    AvailableMachines.Add("All"); // Add "All" option first

                    string machineSql = "SELECT DISTINCT groupname FROM entitygroup WHERE ismanufacturingworkcenter = 'T' Order By groupname";
                    using (OdbcCommand odbcCommand = new OdbcCommand(machineSql, odbcConnection))
                    using (OdbcDataReader odbcReader = odbcCommand.ExecuteReader())
                    {
                        while (odbcReader.Read())
                        {
                            string machineName = odbcReader["groupname"].ToString();
                            if (!string.IsNullOrEmpty(machineName))
                            {
                                AvailableMachines.Add(machineName);
                            }
                        }
                    }
                }

                // --- Part 2: Load Scheduled Tasks (from PostgreSQL) ---
                string npgsqlConnectionString = _configuration.GetConnectionString("DefaultConnection");
                using (NpgsqlConnection npgsqlConnection = new NpgsqlConnection(npgsqlConnectionString))
                {
                    npgsqlConnection.Open();

                    string taskSql = @"SELECT Id, Task_Type, Task_Name, Task_Description, Assign_To, Qty, Unit, Status,
                                     Priority_Level, Created_by, Comments, Task_Date, Task_EndDate, Task_StartTime, Task_EndTime
                                     FROM task_schedule WHERE Status = 'Scheduled'";

                    // *** THIS IS THE CRITICAL FILTERING LOGIC ***
                    if (SelectedMachine != "All")
                    {
                        // Add a WHERE clause to filter by machine if a specific machine is selected
                        // Ensure "Assign_To" matches the column name in your task_schedule table exactly.
                        taskSql += " AND Assign_To=@Assign_To";
                    }

                    using (NpgsqlCommand taskCommand = new NpgsqlCommand(taskSql, npgsqlConnection))
                    {
                        // *** IMPORTANT: Add parameter ONLY if filtering by machine ***
                        if (SelectedMachine != "All")
                        {
                            taskCommand.Parameters.AddWithValue("@Assign_To", SelectedMachine);
                        }

                        using (NpgsqlDataReader taskReader = taskCommand.ExecuteReader())
                        {
                            ScheduledTasks.Clear(); // Clear existing tasks before loading new ones
                            while (taskReader.Read())
                            {
                                // Populate TaskInfo object as before
                                ScheduledTasks.Add(new TaskInfo
                                {
                                    Id = taskReader.GetInt32(taskReader.GetOrdinal("Id")),
                                    Task_Type = taskReader["Task_Type"].ToString(),
                                    Task_Name = taskReader["Task_Name"].ToString(),
                                    Task_Description = taskReader["Task_Description"].ToString(),
                                    Assign_To = taskReader["Assign_To"].ToString(), // Make sure this matches your DB column
                                    Qty = taskReader.GetInt32(taskReader.GetOrdinal("Qty")),
                                    Unit = taskReader["Unit"].ToString(),
                                    Priority_Level = taskReader["Priority_Level"].ToString(),
                                    Task_Date = Convert.ToDateTime(taskReader["Task_Date"]),
                                    Task_EndDate = Convert.ToDateTime(taskReader["Task_Date"]),
                                    Task_StartTime = TimeSpan.Parse(taskReader["Task_StartTime"].ToString()),
                                    Task_EndTime = TimeSpan.Parse(taskReader["Task_EndTime"].ToString()),
                                    Comments = taskReader["Comments"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                // You might want to log the full exception details here for debugging
                // Console.WriteLine($"Error loading tasks: {ex.Message}");
                // Console.WriteLine(ex.StackTrace);
            }
        }

    }

}
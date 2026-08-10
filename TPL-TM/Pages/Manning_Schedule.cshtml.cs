using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Npgsql;
using System.Data.Odbc;
using Microsoft.EntityFrameworkCore;
using TPL_TM.Data;


namespace TPL_TM.Pages
{
    public class Manning_ScheduleModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public Manning_ScheduleModel(
             IConfiguration configuration,
             UserManager<IdentityUser> userManager,
             ApplicationDbContext context)
        {
            _configuration = configuration;
            _userManager = userManager;
            _context = context;
        }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public List<ManningInfo> MachineLineList { get; set; } = new();
        public List<ManningInfo> MachineList { get; set; } = new();
        public List<ManningInfo> EmployeeList { get; set; } = new();
        public List<ManningInfo> ExistingSchedules { get; set; } = new();

        [BindProperty]
        public ManningInfo Schedule { get; set; } = new();

        public bool IsEdit => Schedule.Id > 0;

        public async Task OnGetAsync(
            long? id = null,
            string? machine = null,
            string? machineLine = null,
            DateTime? date = null,
            string? shift = null)
        {
            LoadMachineLines();

            LoadMachines();

            await LoadEmployees();

            LoadSchedules();


            if (id.HasValue)
            {
                LoadSchedule(id.Value);
                return;
            }


            // New schedule from calendar plus button
            if (!string.IsNullOrEmpty(machine))
            {
                Schedule = new ManningInfo
                {
                    Machine_Name = machine,
                    Machine_Line = machineLine,
                    Schedule_Date = date,
                    Shift = shift
                };
            }
        }
        private void LoadSchedule(long id)
        {
            using NpgsqlConnection conn =
                new(_configuration.GetConnectionString("DefaultConnection"));

            conn.Open();

            string sql = @"
                SELECT *
                FROM manning_schedule
                WHERE id=@id";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return;

            Schedule = new ManningInfo
            {
                Id = Convert.ToInt64(reader["id"]),
                Machine_Name = reader["machine_name"].ToString(),
                Machine_Line = reader["machine_line"].ToString(),
                Employee_UserName = reader["employee_username"].ToString(),
                Shift = reader["shift"].ToString(),
                Schedule_Date = (DateTime)reader["schedule_date"],
                Comments = reader["comments"].ToString()
            };
        }
        private void LoadMachineLines()
        {
            string connectionString =
                _configuration.GetConnectionString("NetSuiteOdbc");


            using OdbcConnection conn = new(connectionString);

            conn.Open();


            string sql = @"
                SELECT
                    recordid,
                    name AS MachineLineName

                FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST

                ORDER BY name";


            using OdbcCommand cmd = new(sql, conn);


            using OdbcDataReader reader = cmd.ExecuteReader();


            MachineLineList.Clear();


            while (reader.Read())
            {
                MachineLineList.Add(new ManningInfo
                {
                    Machine_Line =
                        reader["MachineLineName"].ToString()
                });
            }
        }
        private void LoadMachines()
        {
            string connectionString = _configuration.GetConnectionString("NetSuiteOdbc");

            using OdbcConnection conn = new(connectionString);

            conn.Open();

            string sql = @"
                SELECT
                    mach.recordid,
                    mach.name AS MachineName
                FROM CUSTOMLIST_TR_MACHINE_LIST mach
                WHERE mach.isinactive = 'F'
                ORDER BY mach.name";

            using OdbcCommand cmd = new(sql, conn);

            using OdbcDataReader reader = cmd.ExecuteReader();

            MachineList.Clear();

            while (reader.Read())
            {
                MachineList.Add(new ManningInfo
                {
                    Machine_Name = reader["MachineName"].ToString()
                });
            }
        }
        private async Task LoadEmployees()
        {
            var users = await _userManager.Users
                .OrderBy(x => x.UserName)
                .ToListAsync();


            var userShiftAssignments = await _context.UserShiftAssignment
                .Include(x => x.ShiftInformation)
                .ToListAsync();


            EmployeeList.Clear();


            foreach (var user in users)
            {
                var shift = userShiftAssignments
                    .FirstOrDefault(x => x.UserId == user.Id);


                EmployeeList.Add(new ManningInfo
                {
                    Employee_UserName = user.UserName,

                    Employee_Name = user.UserName,

                    Shift = shift?.ShiftInformation?.Name ?? "N/A"
                });
            }
        }

        private void LoadSchedules()
        {
            using NpgsqlConnection conn =
                new(_configuration.GetConnectionString("DefaultConnection"));

            conn.Open();

            string sql = @"

                SELECT *

                FROM manning_schedule

                WHERE status='Scheduled'";

            using NpgsqlCommand cmd = new(sql, conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                ExistingSchedules.Add(new ManningInfo
                {
                    Machine_Name = reader["machine_name"].ToString(),

                    Employee_UserName = reader["employee_username"].ToString(),

                    Schedule_Date = (DateTime)reader["schedule_date"],

                    End_Date = (DateTime)reader["end_date"],

                    Start_Time = (TimeSpan)reader["start_time"],

                    End_Time = (TimeSpan)reader["end_time"]
                });
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Schedule.Employee_Name");
            ModelState.Remove("Schedule.Status");
            try
            {
                if (!ModelState.IsValid)
                {
                    await OnGetAsync(Schedule.Id > 0 ? Schedule.Id : null);
                    return Page();
                }

                string machineLine = Schedule.Machine_Line;
                string machine = Schedule.Machine_Name;
                string employee = Schedule.Employee_UserName;
                string shift = Schedule.Shift;
                string comments = Schedule.Comments ?? "";

                if (!Schedule.Schedule_Date.HasValue)
                    throw new Exception("Schedule date is required.");

                DateTime scheduleDate = Schedule.Schedule_Date.Value.Date;
                DateTime endDate = scheduleDate;

                TimeSpan startTime;
                TimeSpan endTime;

                switch (shift)
                {
                    case "A":
                        startTime = new TimeSpan(6, 0, 0);
                        endTime = new TimeSpan(14, 0, 0);
                        break;

                    case "B":
                        startTime = new TimeSpan(14, 0, 0);
                        endTime = new TimeSpan(22, 0, 0);
                        break;

                    case "C":
                        startTime = new TimeSpan(22, 0, 0);
                        endTime = new TimeSpan(6, 0, 0);
                        endDate = scheduleDate.AddDays(1);
                        break;

                    default:
                        throw new Exception("Invalid shift.");
                }

                // Employee name
                var employeeUser = await _userManager.Users
                    .FirstOrDefaultAsync(x => x.UserName == employee);

                string employeeName = employeeUser?.UserName ?? employee;

                string createdBy = User.Identity?.Name ?? "System";

                using var conn = new NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                await conn.OpenAsync();

                // Ignore current record during overlap check
                //string overlapSql = @"
                //SELECT 1
                //FROM manning_schedule
                //WHERE employee_username=@employee
                //  AND status='Scheduled'
                //  AND id <> @id
                //  AND
                //  (
                //        (@scheduleDate + @startTime)
                //        <
                //        (end_date + end_time)

                //        AND

                //        (@endDate + @endTime)
                //        >
                //        (schedule_date + start_time)
                //  )
                //LIMIT 1;";

                //using (var check = new NpgsqlCommand(overlapSql, conn))
                //{
                //    check.Parameters.AddWithValue("@employee", employee);
                //    check.Parameters.AddWithValue("@id", Schedule.Id);
                //    check.Parameters.AddWithValue("@scheduleDate", scheduleDate);
                //    check.Parameters.AddWithValue("@endDate", endDate);
                //    check.Parameters.AddWithValue("@startTime", startTime);
                //    check.Parameters.AddWithValue("@endTime", endTime);

                //    if (await check.ExecuteScalarAsync() != null)
                //    {
                //        ErrorMessage = "Employee is already assigned during this time.";

                //        await OnGetAsync(Schedule.Id > 0 ? Schedule.Id : null);

                //        return Page();
                //    }
                //}

                if (Schedule.Id == 0)
                {
                    // INSERT

                    string insertSql = @"
                    INSERT INTO manning_schedule
                    (
                        machine_name,
                        machine_line,
                        employee_username,
                        employee_name,
                        shift,
                        schedule_date,
                        end_date,
                        start_time,
                        end_time,
                        status,
                        comments,
                        created_by
                    )
                    VALUES
                    (
                        @machine,
                        @machineLine,
                        @employee,
                        @employeeName,
                        @shift,
                        @scheduleDate,
                        @endDate,
                        @startTime,
                        @endTime,
                        'Scheduled',
                        @comments,
                        @createdBy
                    );";

                    using var cmd = new NpgsqlCommand(insertSql, conn);

                    cmd.Parameters.AddWithValue("@machine", machine);
                    cmd.Parameters.AddWithValue("@machineLine", machineLine);
                    cmd.Parameters.AddWithValue("@employee", employee);
                    cmd.Parameters.AddWithValue("@employeeName", employeeName);
                    cmd.Parameters.AddWithValue("@shift", shift);
                    cmd.Parameters.AddWithValue("@scheduleDate", scheduleDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    cmd.Parameters.AddWithValue("@startTime", startTime);
                    cmd.Parameters.AddWithValue("@endTime", endTime);
                    cmd.Parameters.AddWithValue("@comments", comments);
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);

                    await cmd.ExecuteNonQueryAsync();

                    SuccessMessage = "Manning schedule created successfully.";
                }
                else
                {
                    // UPDATE

                    string updateSql = @"
                    UPDATE manning_schedule
                    SET
                        machine_name=@machine,
                        machine_line=@machineLine,
                        employee_username=@employee,
                        employee_name=@employeeName,
                        shift=@shift,
                        schedule_date=@scheduleDate,
                        end_date=@endDate,
                        start_time=@startTime,
                        end_time=@endTime,
                        comments=@comments
                    WHERE id=@id;";

                    using var cmd = new NpgsqlCommand(updateSql, conn);

                    cmd.Parameters.AddWithValue("@id", Schedule.Id);
                    cmd.Parameters.AddWithValue("@machine", machine);
                    cmd.Parameters.AddWithValue("@machineLine", machineLine);
                    cmd.Parameters.AddWithValue("@employee", employee);
                    cmd.Parameters.AddWithValue("@employeeName", employeeName);
                    cmd.Parameters.AddWithValue("@shift", shift);
                    cmd.Parameters.AddWithValue("@scheduleDate", scheduleDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    cmd.Parameters.AddWithValue("@startTime", startTime);
                    cmd.Parameters.AddWithValue("@endTime", endTime);
                    cmd.Parameters.AddWithValue("@comments", comments);

                    await cmd.ExecuteNonQueryAsync();

                    SuccessMessage = "Manning schedule updated successfully.";
                }

                return RedirectToPage("/Manning_Schedule_Calendar");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;

                await OnGetAsync(Schedule.Id > 0 ? Schedule.Id : null);

                return Page();
            }
        }
        public class ManningInfo
        {
            public long Id { get; set; }

            public string? Machine_Name { get; set; }

            public string? Machine_Line { get; set; }

            public string? Employee_UserName { get; set; }

            public string? Employee_Name { get; set; }

            public string? Shift { get; set; }

            public DateTime? Schedule_Date { get; set; }

            public DateTime? End_Date { get; set; }

            public TimeSpan? Start_Time { get; set; }

            public TimeSpan? End_Time { get; set; }

            public string? Status { get; set; }

            public string? Comments { get; set; }
        }
    }
}

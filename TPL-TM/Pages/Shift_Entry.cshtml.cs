using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetSuite;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_EntryModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        [BindProperty]
        public ShiftInfo ShiftInfo { get; set; } = new ShiftInfo
        {
            Shift_Active = true,
            Shift_Start_Time = DateTime.Now
        };
        [BindProperty]
        public string WorkOrder { get; set; }


        private readonly IConfiguration _configuration;
        private readonly NetSuiteClient _netSuiteClient;
        public string ConnectionString { get; private set; }
        public string LoggedInUserRole { get; set; } = "";

        public Shift_EntryModel(IConfiguration configuration, NetSuiteClient netSuiteClient)
        {
            _configuration = configuration;
            _netSuiteClient = netSuiteClient;
        }

        public List<ShiftTaskInfo> MachineLineList { get; set; } = new();
        public List<ShiftTaskInfo> MachineNameList { get; set; } = new();
        public List<ShiftTaskInfo> ProductLineList { get; set; } = new();
        
        // Work Orders from NetSuite
        public List<WorkOrderItem> WorkOrders { get; set; } = new();
        public Dictionary<string, WorkOrderItem> WorkOrderLookup { get; set; } = new();


        public void OnGet(string? workorder)
        {
            var isAuthorized = TempData.Peek("SupervisorAuthorized") as bool?;
            var shiftId = TempData.Peek("ShiftId") as string;

            if (isAuthorized == true)
                ViewData["AuthorizationMessage"] = "✅ Supervisor authorization confirmed.";
            else
                ViewData["AuthorizationMessage"] = "⚠️ Supervisor authorization not found.";

            if (!string.IsNullOrEmpty(shiftId))
                ShiftInfo.Shift_Id = shiftId;

            ShiftInfo.Shift_Active = true;
            ShiftInfo.Shift_Start_Time = DateTime.Now;
            // Auto-fill Work Order if passed from Scheduler
            if (!string.IsNullOrWhiteSpace(workorder))
            {
                ShiftInfo.WorkOrderNumber = workorder;
            }

            try
            {
                // Identify logged-in user role
                if (User.Identity.IsAuthenticated)
                {
                    if (User.IsInRole("Admin")) LoggedInUserRole = "Admin";
                    else if (User.IsInRole("Supervisor")) LoggedInUserRole = "Supervisor";
                    else LoggedInUserRole = "User";
                }

                ConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");
                using var connection = new OdbcConnection(ConnectionString);
                connection.Open();

                // Machine Names
                string sqlMachines = "SELECT DISTINCT name FROM CUSTOMLIST_TR_MACHINE_LIST ORDER BY name";
                using (var cmd = new OdbcCommand(sqlMachines, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        MachineNameList.Add(new ShiftTaskInfo { Machine_Name = reader["name"].ToString() });
                }

                // Machine Lines
                string sqlLines = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";
                using (var cmd = new OdbcCommand(sqlLines, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        MachineLineList.Add(new ShiftTaskInfo { Machine_Line = reader["name"].ToString() });
                }

                // Product Lines
                string sqlProductLines = @"SELECT DISTINCT fullname FROM classification WHERE fullname IS NOT NULL ORDER BY fullname";
                using (var cmd = new OdbcCommand(sqlProductLines, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        ProductLineList.Add(new ShiftTaskInfo { Product_Line = reader["fullname"].ToString() });
                }

                // Load Shift Checks Template (from Postgres)
                ShiftInfo.ShiftChecks = new List<ShiftCheckInfo>();
                using var connPg = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connPg.Open();
                string createdBy = ShiftInfo.Machine_Name?.Equals("Rework", StringComparison.OrdinalIgnoreCase) == true
                    ? "Rework"
                    : "System";

                string sqlChecks = @"
                    SELECT check_name
                    FROM shift_checks_template
                    WHERE created_by = @CreatedBy
                    ORDER BY id";

                using var cmdChecks = new NpgsqlCommand(sqlChecks, connPg);
                cmdChecks.Parameters.AddWithValue("@CreatedBy", createdBy);
                //string sqlChecks = "SELECT check_name FROM shift_checks_template ORDER BY id";
                //using var cmdChecks = new NpgsqlCommand(sqlChecks, connPg);
                using var readerChecks = cmdChecks.ExecuteReader();
                while (readerChecks.Read())
                {
                    ShiftInfo.ShiftChecks.Add(new ShiftCheckInfo
                    {
                        CheckName = readerChecks["check_name"].ToString(),
                        CheckStatus = false
                    });
                }
                // Load Work Orders from NetSuite
                LoadWorkOrders();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to load data: {ex.Message}";
            }
        }
        private void LoadWorkOrders()
        {
            string connStr = _configuration.GetConnectionString("NetSuiteOdbc");
            using var conn = new OdbcConnection(connStr);
            conn.Open();

            string lastMonthDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd");

            string sql = $@"
                SELECT DISTINCT 
                        a.tranid AS WorkOrder,
                        mach.name AS MachineName,
                        ptype.name AS MachineLine,
                        cls.fullname AS ProductLine,
                        c.id AS InternalId,
                        c.itemid AS PartNumber,
                        c.displayname AS ItemDescription,
                        c.custitemproduct_spec_qtyperouter AS CaseQty
                FROM transaction a
                JOIN transactionline b 
                    ON a.id = b.transaction
                JOIN item c 
                    ON b.item = c.id
                LEFT JOIN classification cls 
                    ON cls.id = c.class
                LEFT JOIN CUSTOMLIST_TR_MACHINE_LIST mach 
                    ON a.custbody_tr_wo_machine = mach.recordid
                LEFT JOIN CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ptype 
                    ON c.custitemproduct_spec_producttype = ptype.recordid
                WHERE RTRIM(a.recordtype) = 'workorder'
                  AND b.mainline = 'T'
                  AND b.itemtype = 'Assembly'
                  AND b.item IS NOT NULL
                  AND a.createddate >= {{d '{lastMonthDate}' }}
                ORDER BY a.tranid DESC";
            

            using var cmd = new OdbcCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                var wo = new WorkOrderItem
                {
                    WorkOrder = rdr["WorkOrder"]?.ToString(),
                    MachineName = rdr["MachineName"]?.ToString(), // ✅ correct
                    MachineLine = rdr["MachineLine"]?.ToString(),   // ✅
                    ProductLine = rdr["ProductLine"]?.ToString(),
                    InternalId = rdr["InternalId"] == DBNull.Value ? null : Convert.ToInt32(rdr["InternalId"]),
                    PartNumber = rdr["PartNumber"]?.ToString(),
                    ItemDescription = rdr["ItemDescription"]?.ToString(),
                    CaseQty = rdr["CaseQty"] == DBNull.Value ? null : Convert.ToInt32(rdr["CaseQty"])
                };


                WorkOrders.Add(wo);

                if (!string.IsNullOrWhiteSpace(wo.WorkOrder))
                    WorkOrderLookup[wo.WorkOrder] = wo;
            }
        }

        public JsonResult OnGetPreviousShift(string machineLine, string machineName)
        {
            DateTime? previousShiftEnd = null;
            try
            {
                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                string sql = @"
                    SELECT shift_end_time
                    FROM shift
                    WHERE machine_line = @MachineLine AND machine_name = @MachineName
                    ORDER BY id DESC
                    LIMIT 1";

                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@MachineLine", machineLine);
                cmd.Parameters.AddWithValue("@MachineName", machineName);

                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    previousShiftEnd = (DateTime)result;
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }

            return new JsonResult(new { previousShiftEnd });
        }


        public JsonResult OnGetShiftChecks(string machineName)
        {
            var checks = new List<ShiftCheckInfo>();

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string createdBy = string.Equals(machineName, "Rework", StringComparison.OrdinalIgnoreCase)
                     ? "Rework"
                     : "System";

                string sql = @"
                    SELECT check_name
                    FROM shift_checks_template
                    WHERE created_by = @CreatedBy
                    ORDER BY id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    checks.Add(new ShiftCheckInfo
                    {
                        CheckName = reader["check_name"].ToString(),
                        CheckStatus = false
                    });
                }
            }
            catch
            {
            }

            return new JsonResult(checks);
        }

        public IActionResult OnPostStartShift()
        {
            var user = User;
            bool supervisorAuthorized = TempData.Peek("SupervisorAuthorized") as bool? ?? false;
            string authorizedBy = TempData.Peek("AuthorizedBy") as string;

            // 🛡 Enforce supervisor approval for Users
            if (!(user.IsInRole("Admin") || user.IsInRole("Supervisor") || supervisorAuthorized))
            {
                ErrorMessage = "❌ Supervisor authorization required to start a shift.";
                return Page();
            }

            try
            {
                ShiftInfo.Created_By = User.Identity?.Name ?? "Unknown";
                ShiftInfo.Shift_Start_Time = DateTime.Now;
                ShiftInfo.Authorized_By = authorizedBy ?? "Not Authorized";

                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                string shiftQuery = @"
                    INSERT INTO shift
                    (work_order_number, work_order_item, work_order_description, machine_line, machine_name, product_line, case_qty, handover_rating, shift_active, shift_start_time, created_by, authorized_by)
                    VALUES
                    (@WorkOrderNumber, @WorkOrderItem, @WorkOrderDescription, @MachineLine, @MachineName, @ProductLine,  @CaseQty, @HandoverRating, @ShiftActive, @ShiftStart, @CreatedBy, @AuthorizedBy)
                    RETURNING id";

                long shiftId;
                using (var cmd = new NpgsqlCommand(shiftQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@WorkOrderNumber", ShiftInfo.WorkOrderNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@WorkOrderItem", ShiftInfo.WorkOrderItem ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@WorkOrderDescription", ShiftInfo.WorkOrderDescription ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MachineLine", ShiftInfo.Machine_Line ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MachineName", ShiftInfo.Machine_Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ProductLine", ShiftInfo.Product_Line ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CaseQty", ShiftInfo.Case_Qty.HasValue ? ShiftInfo.Case_Qty.Value : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@HandoverRating", ShiftInfo.Handover_Rating ?? 0);
                    cmd.Parameters.AddWithValue("@ShiftActive", true);
                    cmd.Parameters.AddWithValue("@ShiftStart", ShiftInfo.Shift_Start_Time);
                    cmd.Parameters.AddWithValue("@CreatedBy", ShiftInfo.Created_By);
                    cmd.Parameters.AddWithValue("@AuthorizedBy", ShiftInfo.Authorized_By ?? (object)DBNull.Value);

                    shiftId = (long)cmd.ExecuteScalar();
                }

                // Insert shift checks
                string checkQuery = @"
                    INSERT INTO shift_checks (shift_id, check_name, check_status, comment, created_by)
                    VALUES (@ShiftId, @CheckName, @CheckStatus, @Comment, @CreatedBy)";

                foreach (var check in ShiftInfo.ShiftChecks)
                {
                    using var cmd = new NpgsqlCommand(checkQuery, connection);
                    cmd.Parameters.AddWithValue("@ShiftId", shiftId);
                    cmd.Parameters.AddWithValue("@CheckName", check.CheckName ?? "");
                    cmd.Parameters.AddWithValue("@CheckStatus", check.CheckStatus);
                    cmd.Parameters.AddWithValue("@Comment", check.Comment ?? "");
                    cmd.Parameters.AddWithValue("@CreatedBy", ShiftInfo.Created_By ?? "Unknown");
                    cmd.ExecuteNonQuery();
                }

                TempData.Remove("SupervisorAuthorized");
                TempData.Remove("AuthorizedBy");

                SuccessMessage = "✅ Shift started successfully.";
                return RedirectToPage("/Shift_Dashboard");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to start shift: {ex.Message}";
                return Page();
            }
        }
    }


    public class ShiftInfo
    {
        public long Id { get; set; }
        public string Shift_Id { get; set; }
        public string Machine_Line { get; set; }
        public string Machine_Name { get; set; }
        public string Product_Line { get; set; }
        public string WorkOrderNumber { get; set; }
        public string WorkOrderItem { get; set; }
        public string WorkOrderDescription { get; set; }
        public int? Handover_Rating { get; set; }
        public bool Shift_Active { get; set; } = true; // default checked
        public DateTime? Shift_Start_Time { get; set; }
        public DateTime? Shift_End_Time { get; set; }
        public int? Shift_Rating { get; set; }
        public string Comment { get; set; }
        public string Shift_Letter { get; set; }
        public string Created_By { get; set; }
        public string Authorized_By { get; set; }   // ✅ NEW
        public int? Case_Qty { get; set; }
        public List<MaterialInfo> ConsumedMaterials { get; set; } = new();
        public List<QualityCheckInfo> QualityChecks { get; set; } = new();
        public List<WasteInfo> WasteEntries { get; set; } = new();
        public List<DowntimeInfo> DowntimeEntries { get; set; } = new List<DowntimeInfo>();
        public List<ProductionInfo> ProductionEntries { get; set; } = new();
        public List<ShiftCheckInfo> ShiftChecks { get; set; } = new List<ShiftCheckInfo>();

        // ✅ Add computed property
        public DateTime? LastQualityCheckTime
        {
            get
            {
                if (QualityChecks == null || QualityChecks.Count == 0)
                    return null;

                return QualityChecks
                    .Select(q => q.Created_On)
                    .Where(dt => dt != null)
                    .Max();
            }
        }

    }

    public class ShiftTaskInfo
    {
        public string Machine_Line { get; set; }   // <-- used for filtering
        public string Product_Line { get; set; }   // <-- used as dropdown text/value
        public string Machine_Name { get; set; }
    }
    public class ShiftCheckInfo
    {
        public string CheckName { get; set; }
        public bool CheckStatus { get; set; }
        public string Comment { get; set; }
    }

}

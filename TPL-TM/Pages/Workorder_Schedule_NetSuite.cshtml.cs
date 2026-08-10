using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Workorder_Schedule_NetSuiteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public string errorMessage = "";
        public string successMessage = "";

        [BindProperty(SupportsGet = true)]
        public DateTime SelectedStartDate { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public string SelectedMachineLine { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public string SelectedMachineName { get; set; } = "All";

        public List<string> AvailableMachineLines { get; set; } = new();
        public List<string> AvailableMachineNames { get; set; } = new();

        public List<WorkOrderInfo> ScheduledWorkOrders { get; set; } = new();

        public Workorder_Schedule_NetSuiteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet(
            DateTime? startDate,
            string selectedMachineLine,
            string selectedMachineName)
        {
            if (startDate.HasValue)
                SelectedStartDate = startDate.Value;

            if (!string.IsNullOrWhiteSpace(selectedMachineLine))
                SelectedMachineLine = selectedMachineLine;

            if (!string.IsNullOrWhiteSpace(selectedMachineName))
                SelectedMachineName = selectedMachineName;

            LoadWorkOrdersFromNetSuite();
        }

        public IActionResult OnPost()
        {
            LoadWorkOrdersFromNetSuite();
            return Page();
        }

        private void LoadWorkOrdersFromNetSuite()
        {
            try
            {
                string connStr = _configuration.GetConnectionString("NetSuiteOdbc");
                using (var conn = new OdbcConnection(connStr))
                {
                    conn.Open();

                    // --- Load Machines ---
                    AvailableMachineLines.Clear();
                    AvailableMachineLines.Add("All");

                    string lineSql = @"
                        SELECT DISTINCT name
                        FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST
                        ORDER BY name";

                    using (var cmd = new OdbcCommand(lineSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var line = reader["name"]?.ToString();

                            if (!string.IsNullOrWhiteSpace(line))
                                AvailableMachineLines.Add(line);
                        }
                    }



                    // --- Date range ---
                    string fromDate = SelectedStartDate.ToString("yyyy-MM-dd");
                    string toDate = SelectedStartDate.AddDays(6).ToString("yyyy-MM-dd");

                    // --- Load Built Quantities (per WO) ---
                    var builtQtyByWO = new Dictionary<int, int>();


                    string builtSql = $@"
                        SELECT
                            wo.id AS WorkOrderID,
                            wo.tranid AS WorkOrderNumber,
                            SUM(tl.quantity) AS TotalBuiltQty
                        FROM transaction wo
                        JOIN transactionline tl
                            ON tl.createdfrom = wo.id
                        WHERE
                            wo.type = 'WorkOrd'
                            AND tl.itemtype = 'Assembly'
                            AND tl.mainline = 'T'
                            AND tl.quantity IS NOT NULL
                            AND wo.startdate >= {{d '{fromDate}'}} 
                            AND wo.startdate <= {{d '{toDate}'}}
                        GROUP BY
                            wo.id, wo.tranid
                        ORDER BY
                            wo.tranid";

                    using (var cmd = new OdbcCommand(builtSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int internalId = Convert.ToInt32(reader["WorkOrderID"]);
                            int builtQty = reader["TotalBuiltQty"] != DBNull.Value
                                ? Convert.ToInt32(reader["TotalBuiltQty"])
                                : 0;

                            builtQtyByWO[internalId] = builtQty;

                        }
                    }


                    // --- Load Work Orders ---
                    var machineSet = new HashSet<string>();
                    ScheduledWorkOrders.Clear();

                    string workOrderSql = $@"
                        SELECT DISTINCT
                            wo.id AS InternalId,
                            wo.tranid AS WO_Number,
                            wo.custbodyproduction_start_time AS Start_Time,
                            wo.custbodyproduction_end_time AS End_Time,
                            wo.startdate AS WO_Date,

                            itm.id AS Item_Internal_ID,
                            itm.itemid AS Description,
                            itm.displayname AS ItemName,

                            mach.name AS MachineName,
                            ptype.name AS MachineLine,

                            line.quantity AS Qty,
                            uom.abbreviation AS Unit,
                            wo.custbodyjobbag_link AS JobBag_Link

                        FROM transaction wo

                        JOIN transactionline line
                            ON wo.id = line.transaction

                        JOIN item itm
                            ON line.item = itm.id

                        JOIN unitsTypeUom uom
                            ON line.units = uom.internalid

                        LEFT JOIN CUSTOMLIST_TR_MACHINE_LIST mach
                            ON wo.custbody_tr_wo_machine = mach.recordid

                        LEFT JOIN CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ptype
                            ON itm.custitemproduct_spec_producttype = ptype.recordid

                        WHERE
                            RTRIM(wo.recordtype) = 'workorder'
                            AND line.mainline = 'T'
                            AND line.itemtype = 'Assembly'
                            AND line.item IS NOT NULL

                            AND wo.startdate >= {{d '{fromDate}'}}
                            AND wo.startdate <= {{d '{toDate}'}}

                            {(SelectedMachineLine != "All"
                                ? "AND ptype.name = ?" : "")}

                            {(SelectedMachineName != "All"
                                ? "AND mach.name = ?" : "")}

                        ORDER BY wo.tranid DESC";

                    using (var cmd = new OdbcCommand(workOrderSql, conn))
                    {
                        if (SelectedMachineLine != "All")
                        {
                            cmd.Parameters.AddWithValue("?", SelectedMachineLine);
                        }

                        if (SelectedMachineName != "All")
                        {
                            cmd.Parameters.AddWithValue("?", SelectedMachineName);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TimeSpan startTime = TimeSpan.Zero;
                                TimeSpan endTime = TimeSpan.Zero;

                                if (reader["Start_Time"] != DBNull.Value &&
                                    DateTime.TryParse(reader["Start_Time"].ToString(), out var parsedStart))
                                    startTime = parsedStart.TimeOfDay;

                                if (reader["End_Time"] != DBNull.Value &&
                                    DateTime.TryParse(reader["End_Time"].ToString(), out var parsedEnd))
                                    endTime = parsedEnd.TimeOfDay;

                                if (startTime == TimeSpan.Zero && endTime == TimeSpan.Zero)
                                    continue;

                                var machineName = reader["MachineName"]?.ToString();

                                if (!string.IsNullOrWhiteSpace(machineName))
                                {
                                    machineSet.Add(machineName);
                                }

                                int internalId = Convert.ToInt32(reader["InternalId"]);

                                ScheduledWorkOrders.Add(new WorkOrderInfo
                                {
                                    InternalId = internalId,
                                    WO_Number = reader["WO_Number"].ToString(),
                                    Description = reader["Description"].ToString(),
                                    ItemName = reader["ItemName"].ToString(),
                                    MachineLine = reader["MachineLine"]?.ToString(),
                                    MachineName = reader["MachineName"]?.ToString(),
                                    Assign_To = reader["MachineName"]?.ToString(),
                                    Qty = Convert.ToInt32(reader["Qty"]),
                                    Built_Qty = builtQtyByWO.TryGetValue(internalId, out var built) ? built : 0,
                                    Unit = reader["Unit"].ToString(),
                                    JobBag_Link = reader["JobBag_Link"] == DBNull.Value ? "" : reader["JobBag_Link"].ToString(),
                                    WO_StartDate = Convert.ToDateTime(reader["WO_StartDate"]),
                                    WO_EndDate = reader["WO_EndDate"] == DBNull.Value
                                                ? Convert.ToDateTime(reader["WO_StartDate"])
                                                : Convert.ToDateTime(reader["WO_EndDate"]),
                                    WO_StartTime = startTime,
                                    WO_EndTime = endTime
                                });
                            }
                            AvailableMachineNames.Clear();
                            AvailableMachineNames.Add("All");
                            AvailableMachineNames.AddRange(machineSet.OrderBy(x => x));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"[ERROR] {ex.Message}";
            }
        }


    }
    
}

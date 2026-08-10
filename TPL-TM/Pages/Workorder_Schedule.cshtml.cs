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
    public class Workorder_ScheduleModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public string errorMessage = "";
        public string successMessage = "";

        [BindProperty(SupportsGet = true)]
        public DateTime SelectedStartDate { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public string SelectedMachineLine { get; set; } = "Straws";

        [BindProperty(SupportsGet = true)]
        public string SelectedMachineName { get; set; } = "All";

        public List<string> AvailableMachineLines { get; set; } = new();
        public List<string> AvailableMachineNames { get; set; } = new();

        public List<WorkOrderInfo> ScheduledWorkOrders { get; set; } = new();
        public List<WorkOrderSegment> Segments { get; set; } = new();

        public Workorder_ScheduleModel(IConfiguration configuration)
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
                            wo.startdate AS WO_StartDate,
                            wo.enddate AS WO_EndDate,

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
                            
                            AND wo.startdate <= {{d '{toDate}'}}
                            AND wo.enddate >= {{d '{fromDate}'}}

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
                            BuildSegments();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"[ERROR] {ex.Message}";
            }
        }
        private void BuildSegments()
        {
            Segments.Clear();

            foreach (var wo in ScheduledWorkOrders)
            {
                var start = wo.StartDateTime;
                var end = wo.EndDateTime;

                if (end <= start)
                    continue;

                for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
                {
                    int startHour;
                    int endHour;

                    if (day == start.Date)
                    {
                        startHour = start.Hour;
                    }
                    else
                    {
                        startHour = 0;
                    }

                    if (day == end.Date)
                    {
                        endHour = end.Hour;

                        if (end.Minute > 0 ||
                            end.Second > 0)
                        {
                            endHour++;
                        }
                    }
                    else
                    {
                        endHour = 24;
                    }

                    endHour = Math.Min(24, endHour);

                    Segments.Add(new WorkOrderSegment
                    {
                        WorkOrder = wo,
                        Day = day,
                        StartHour = startHour,
                        EndHour = endHour
                    });
                }
            }
        }


    }

    public class WorkOrderInfo
    {
        public int InternalId { get; set; }
        public string WO_Number { get; set; }
        public string Description { get; set; }
        public string ItemName { get; set; }
        public string ArtworkUrl { get; set; }
        public string MachineLine { get; set; }
        public string MachineName { get; set; }

        public string Assign_To { get; set; }

        public int Qty { get; set; }
        public int Built_Qty { get; set; }

        public string Unit { get; set; }
        public string JobBag_Link { get; set; }

        public DateTime WO_StartDate { get; set; }

        public DateTime WO_EndDate { get; set; }

        public DateTime? WO_ActualStartDate { get; set; }
        public DateTime? WO_ActualEndDate { get; set; }

        public TimeSpan WO_StartTime { get; set; }

        public TimeSpan WO_EndTime { get; set; }

        public DateTime StartDateTime =>
            WO_StartDate.Date.Add(WO_StartTime);

        public DateTime EndDateTime =>
            WO_EndDate.Date.Add(WO_EndTime);
        public string Status { get; set; }


        public string ProductType { get; set; }
        public string CMYK { get; set; }
        public string Ink1 { get; set; }
        public string Ink2 { get; set; }
        public string Ink3 { get; set; }
        public string Ink4 { get; set; }
        public string Ink5 { get; set; }
        public string CyrelRef1 { get; set; }
        public string CyrelRef2 { get; set; }
        public string Varnish { get; set; }
        public string Prima { get; set; }
       
        public string Varnish2 { get; set; }

        public string Layout { get; set; }

        public string PICode { get; set; }
        public string OuterBarcode { get; set; }
        public string InnerBarcode { get; set; }
        public string InnerType { get; set; }
        public string OuterType { get; set; }
        public string QtyPerCase { get; set; }

        public string OBPartNo { get; set; }

        public string InnerPartNo { get; set; }

        public string InnerLayout { get; set; }

        public string QtyPerInnerLayer { get; set; }

        public string CaseWtNet { get; set; }

        public string CaseWtGross { get; set; }

        public string InnersPerCase { get; set; }

        public string CaseBarcode { get; set; }

        public string PalletType { get; set; }
        public string PalletBarcode { get; set; }
        public string PalletLayers { get; set; }
        public string QtyPerLayer { get; set; }
        public string QtyPerPallet { get; set; }

        public string QtyPerOuter { get; set; }
        public string CasesPerLayer { get; set; }
        public string CasesPerPallet { get; set; }
        public string PackingDescription { get; set; }
        public string PalletSize { get; set; }
        public string PalletWtNet { get; set; }
        public string PalletWtGross { get; set; }
        public string PalletHeight { get; set; }
        public string PalletDimension { get; set; }

        public string PrintNotes { get; set; }
        public string CustomerNotes { get; set; }
        public string HandStripping { get; set; }
        public string ProcessComments { get; set; }
        public string? ApprovalStatus { get; set; }


    }
    public class WorkOrderSegment
    {
        public WorkOrderInfo WorkOrder { get; set; }

        public DateTime Day { get; set; }

        public int StartHour { get; set; }

        public int EndHour { get; set; }

        public int Span =>
            Math.Max(1, EndHour - StartHour);
    }
}

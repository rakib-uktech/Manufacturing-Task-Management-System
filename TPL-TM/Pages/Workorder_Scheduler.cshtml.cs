using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Workorder_SchedulerModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public List<WorkOrderInfo> ScheduledWorkOrders { get; set; } = new();

        public Workorder_SchedulerModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
        }

        public JsonResult OnGetBom(int id)
        {
            var list = new List<object>();
            
            var connStr = _configuration.GetConnectionString("NetSuiteOdbc");

            using var conn = new OdbcConnection(connStr);
            conn.Open();

            string sql = @"
                SELECT
                    ABS(tl.quantity) AS quantity,
                    itm.itemid,
                    itm.displayname,
                    itm.description
                FROM transactionline tl
                JOIN item itm
                    ON tl.item = itm.id
                WHERE tl.transaction = ?
                  AND tl.mainline = 'F'
                  AND tl.quantity <> 0
                  AND itm.displayname NOT LIKE 'M/C %'
                  AND itm.displayname NOT LIKE '%Labour Run%'
                  AND itm.displayname NOT LIKE '%Machine Setup%'
                  AND itm.displayname NOT LIKE '%Machine Run%'
                  AND itm.displayname NOT LIKE '%Other%'
                ORDER BY ABS(tl.quantity) DESC;
                ";

            using var cmd = new OdbcCommand(sql, conn);
            cmd.Parameters.AddWithValue("@p1", id);

            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                list.Add(new
                {
                    quantity = r["quantity"],
                    item = $"{r["itemid"]}",
                    displayname = $"{r["displayname"]}",
                    description = r["description"]?.ToString()
                });
            }

            return new JsonResult(list);
        }

        public JsonResult OnGetSchedulerData()
        {
            LoadWorkOrdersFromNetSuite();

           
            var cutoff = DateTime.Today.AddMonths(-1);
            
            var machineNames = ScheduledWorkOrders
                .Where(x => x.EndDateTime >= cutoff)
                .Where(x => !string.IsNullOrWhiteSpace(x.MachineName))
                .Select(x => x.MachineName)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var resources = ScheduledWorkOrders
                .Where(x => x.EndDateTime >= cutoff)
                .Where(x => !string.IsNullOrWhiteSpace(x.MachineName))
                .GroupBy(x => x.MachineName)
                .Select(g => new
                {
                    id = g.Key,
                    title = g.Key,

                    machineLine = g
                        .Where(x => !string.IsNullOrWhiteSpace(x.MachineLine))
                        .GroupBy(x => x.MachineLine)
                        .OrderByDescending(x => x.Count())   // Most common machine line
                        .Select(x => x.Key)
                        .FirstOrDefault() ?? "Unassigned"
                })
                .OrderBy(x => x.machineLine)
                .ThenBy(x => x.title)
                .ToList();

            var events = ScheduledWorkOrders
                .Where(x => x.EndDateTime >= cutoff)
                .Select(x => new
                {
                    id = x.InternalId,
                    resourceId = x.MachineName,
                    title = x.WO_Number,

                    start = x.WO_ActualStartDate ?? x.StartDateTime,

                    end = x.WO_ActualEndDate
                        ?? (x.EndDateTime <= x.StartDateTime
                            ? x.StartDateTime.AddHours(1)
                            : x.EndDateTime),

                    plannedStart = x.StartDateTime,
                    plannedEnd = x.EndDateTime,

                    actualStart = x.WO_ActualStartDate,

                    actualEnd = x.WO_ActualEndDate,

                    // General
                    status = x.Status,
                    itemName = x.ItemName,
                    itemId = x.Description,
                    artworkUrl = x.ArtworkUrl,
                    jobBagLink = x.JobBag_Link,
                    machineName = x.MachineName,
                    machineLine = x.MachineLine,
                    qty = x.Qty,
                    builtQty = x.Built_Qty,

                    // Product Information
                    productType = x.ProductType,
                    approvalStatus = x.ApprovalStatus,
                    cmyk = x.CMYK,
                    ink1 = x.Ink1,
                    ink2 = x.Ink2,
                    ink3 = x.Ink3,
                    ink4 = x.Ink4,
                    ink5 = x.Ink5,
                    cyrelRef1 = x.CyrelRef1,
                    cyrelRef2 = x.CyrelRef2,
                    varnish = x.Varnish,
                    prima = x.Prima,                    
                    varnish2 = x.Varnish2,
                    layout = x.Layout,

                    // Case Information
                    piCode = x.PICode,
                    outerBarcode = x.OuterBarcode,
                    innerBarcode = x.InnerBarcode,
                    outerType = x.OuterType,
                    innerType = x.InnerType,
                    qtyPerCase = x.QtyPerCase,
                    obPartNo = x.OBPartNo,
                    innerPartNo = x.InnerPartNo,
                    innerLayout = x.InnerLayout,
                    qtyPerInnerLayer = x.QtyPerInnerLayer,
                    caseWtNet = x.CaseWtNet,
                    caseWtGross = x.CaseWtGross,
                    innersPerCase = x.InnersPerCase,
                    caseBarcode = x.CaseBarcode,

                    // Pallet Information
                    palletType = x.PalletType,
                    palletBarcode = x.PalletBarcode,
                    palletLayers = x.PalletLayers,
                    qtyPerLayer = x.QtyPerLayer,
                    qtyPerPallet = x.QtyPerPallet,

                    
                    qtyPerOuter = x.QtyPerOuter,
                    casesPerLayer = x.CasesPerLayer,
                    
                    casesPerPallet = x.CasesPerPallet,
                    packingDescription = x.PackingDescription,
                    
                    palletSize = x.PalletSize,
                    palletWtNet = x.PalletWtNet,
                    palletWtGross = x.PalletWtGross,
                    palletHeight = x.PalletHeight,
                    palletDimension = x.PalletDimension,
                    

                    // Notes
                    printNotes = x.PrintNotes,
                    handStripping = x.HandStripping,
                    processComments = x.ProcessComments,
                    customerNotes = x.CustomerNotes,

                    backgroundColor =
                        x.Status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
                            ? "#6c757d"
                            : x.Built_Qty >= x.Qty
                                ? "#198754"
                                : x.Built_Qty == 0
                                    ? "#dc3545"
                                    : "#ffc107",

                    borderColor =
                        x.Status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
                            ? "#6c757d"
                            : x.Built_Qty >= x.Qty
                                ? "#198754"
                                : x.Built_Qty == 0
                                    ? "#dc3545"
                                    : "#ffc107"
                })
            .ToList();

            return new JsonResult(new
            {
                resources,
                events
            });
        }

        private void LoadWorkOrdersFromNetSuite()
        {
            string connStr =
                _configuration.GetConnectionString("NetSuiteOdbc");
            
            var cutoffDate = DateTime.Today.AddMonths(-2)
                               .ToString("yyyy-MM-dd");

            using var conn = new OdbcConnection(connStr);

            conn.Open();
            var builtQtyByWO = new Dictionary<int, int>();

            var cutoffDateObj = DateTime.Today.AddMonths(-1);

            string fromDate = cutoffDateObj.ToString("yyyy-MM-dd");

            string builtSql = $@"
                SELECT
                    wo.id AS WorkOrderID,
                    SUM(tl.quantity) AS TotalBuiltQty

                FROM transaction wo

                JOIN transactionline tl
                    ON tl.createdfrom = wo.id

                WHERE
                    wo.type = 'WorkOrd'
                    AND tl.itemtype = 'Assembly'
                    AND tl.mainline = 'T'
                    AND tl.accountinglinetype = 'ASSET'
                    AND tl.quantity IS NOT NULL
                    AND wo.startdate >= {{d '{fromDate}'}}

                GROUP BY wo.id";

            using (var builtCmd = new OdbcCommand(builtSql, conn))
            using (var builtReader = builtCmd.ExecuteReader())
            {
                while (builtReader.Read())
                {
                    int woId =
                        Convert.ToInt32(
                            builtReader["WorkOrderID"]);

                    int builtQty =
                        builtReader["TotalBuiltQty"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(
                                builtReader["TotalBuiltQty"]);

                    builtQtyByWO[woId] = builtQty;
                }
            }

            string sql = $@"
                SELECT DISTINCT
                    wo.id AS InternalId,
                    wo.tranid AS WO_Number,
                    BUILTIN.DF(wo.status) AS Status,
                    wo.custbodyproduction_start_time AS Start_Time,
                    wo.custbodyproduction_end_time AS End_Time,
                    wo.startdate AS WO_StartDate,
                    wo.enddate AS WO_EndDate,
                    wo.actualproductionstartdate AS WO_ActualStartDate,
                    wo.actualproductionenddate AS WO_ActualEndDate,

                    itm.itemid AS ItemId,
                    itm.displayname AS ItemName,
                    itm.custitemproduct_spec_artwork_url AS ArtworkUrl,
                    BUILTIN.DF(itm.custitemproduct_spec_producttype)       AS ProductType,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_cmyk)         AS CMYK,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_ink1)         AS Ink1,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_ink2)         AS Ink2,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_ink3)         AS Ink3,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_ink4)         AS Ink4,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_ink5)         AS Ink5,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_cyrelref1)    AS CyrelRef1,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_cyrelref2)    AS CyrelRef2,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_varnish)      AS Varnish,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_prima)        AS Prima,
                    BUILTIN.DF(itm.custitemproduct_spec_crtn_varnish2)      AS Varnish2,
                    BUILTIN.DF(itm.custitem10)                              AS Layout,
                    
                    BUILTIN.DF(itm.custitemproduct_spec_pino) AS PICode,
                    BUILTIN.DF(itm.custitemproduct_spec_obpartno)               AS OBPartNo,

                    BUILTIN.DF(itm.custitemproduct_spec_innertype)             AS InnerType,
                    itm.custitemproduct_spec_innerpartno                       AS InnerPartNo,

                    itm.custitemproduct_spec_innerlayout                       AS InnerLayout,
                    itm.custitemproduct_spec_qtyperinnerlayer                  AS QtyPerInnerLayer,

                    itm.custitemproduct_spec_casewtnet                         AS CaseWtNet,
                    itm.custitemproduct_spec_casewtgrosskg                     AS CaseWtGross,

                    itm.custitemproduct_spec_innlaypercase                     AS InnersPerCase,
                    itm.custitem12                                             AS CaseBarcode,
                    

                    -- Pallet Information
                    BUILTIN.DF(itm.custitemproduct_spec_pallettype)      AS PalletType,
                    itm.custitemproduct_spec_palletlayers                AS PalletLayers,
                    itm.custitemproduct_spec_qtyperouter                 AS QtyPerOuter,
                    itm.custitemproduct_spec_casesperlayer              AS CasesPerLayer,
                    itm.custitemproduct_spec_qtyperpallet               AS QtyPerPallet,
                    itm.custitemproduct_spec_caseperpallet              AS CasesPerPallet,
                    itm.custitemproduct_spec_packingdesc                AS PackingDescription,
                    BUILTIN.DF(itm.custitemproduct_spec_palletsize)      AS PalletSize,
                    itm.custitemproduct_spec_palletwtnetkg             AS PalletWtNet,
                    itm.custitemproduct_spec_palletwtgrosskg           AS PalletWtGross,
                    itm.custitemproduct__spec_palletheightm            AS PalletHeight,
                    itm.custitemproduct_spec_palletdimension           AS PalletDimension,
                    itm.custitemproduct_spec_palletbarcode             AS PalletBarcode,
                    BUILTIN.DF(itm.custitemproduct_spec_hand_strip) AS HandStrip,
                    itm.custitemitem_approval_status AS ApprovalStatus,

                    mach.name AS MachineName,

                    ptype.name AS MachineLine,

                    line.quantity AS Qty,

                    wo.custbodyjobbag_link AS JobBag_Link

                FROM transaction wo

                JOIN transactionline line
                    ON wo.id = line.transaction

                JOIN item itm
                    ON line.item = itm.id

                LEFT JOIN CUSTOMLIST_TR_MACHINE_LIST mach
                    ON wo.custbody_tr_wo_machine = mach.recordid
                
                LEFT JOIN CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ptype
                    ON itm.custitemproduct_spec_producttype = ptype.recordid

                WHERE
                    RTRIM(wo.recordtype) = 'workorder'
                    AND line.mainline = 'T'
                    AND line.itemtype = 'Assembly'
                    AND line.item IS NOT NULL

                    AND (
                        wo.enddate >= {{d '{cutoffDate}'}}
                        OR wo.enddate IS NULL
                    )

                ORDER BY wo.startdate";

            using var cmd = new OdbcCommand(sql, conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                try
                {

                    TimeSpan startTime = TimeSpan.Zero;
                    TimeSpan endTime = TimeSpan.Zero;

                    if (reader["Start_Time"] != DBNull.Value &&
                        DateTime.TryParse(reader["Start_Time"].ToString(),
                        out var parsedStart))
                    {
                        startTime = parsedStart.TimeOfDay;
                    }

                    if (reader["End_Time"] != DBNull.Value &&
                        DateTime.TryParse(reader["End_Time"].ToString(),
                        out var parsedEnd))
                    {
                        endTime = parsedEnd.TimeOfDay;
                    }

                    

                    ScheduledWorkOrders.Add(new WorkOrderInfo
                    {
                        InternalId =
                            reader["InternalId"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["InternalId"]),

                        WO_Number =
                            reader["WO_Number"].ToString(),
                        Status = 
                            reader["Status"]?.ToString(),
                        ItemName =
                            reader["ItemName"]?.ToString(),

                        Description =
                            reader["ItemId"]?.ToString(),

                        ArtworkUrl =
                            reader["ArtworkUrl"]?.ToString(),

                        MachineName =
                            reader["MachineName"]?.ToString(),
                       
                        MachineLine =
                            reader["MachineLine"]?.ToString(),

                        Qty =
                            reader["Qty"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["Qty"]),

                        Built_Qty =
                            builtQtyByWO.TryGetValue(
                                Convert.ToInt32(reader["InternalId"]),
                                out var builtQty)
                                    ? builtQty
                                    : 0,

                        JobBag_Link =
                            reader["JobBag_Link"]?.ToString(),

                        WO_StartDate =
                            reader["WO_StartDate"] == DBNull.Value
                                ? DateTime.Today
                                : Convert.ToDateTime(reader["WO_StartDate"]),

                        WO_EndDate =
                            reader["WO_EndDate"] == DBNull.Value
                                ? (
                                    reader["WO_StartDate"] == DBNull.Value
                                        ? DateTime.Today
                                        : Convert.ToDateTime(reader["WO_StartDate"])
                                  )
                                : Convert.ToDateTime(reader["WO_EndDate"]),
                        WO_ActualStartDate =
                            reader["WO_ActualStartDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["WO_ActualStartDate"]),

                                                WO_ActualEndDate =
                            reader["WO_ActualEndDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["WO_ActualEndDate"]),

                        WO_StartTime = startTime,
                        WO_EndTime = endTime,
                        ProductType = reader["ProductType"]?.ToString(),
                        CMYK = reader["CMYK"]?.ToString(),
                        Ink1 = reader["Ink1"]?.ToString(),
                        Ink2 = reader["Ink2"]?.ToString(),
                        Ink3 = reader["Ink3"]?.ToString(),
                        Ink4 = reader["Ink4"]?.ToString(),
                        Ink5 = reader["Ink5"]?.ToString(),
                        CyrelRef1 = reader["CyrelRef1"]?.ToString(),
                        CyrelRef2 = reader["CyrelRef2"]?.ToString(),
                        Varnish = reader["Varnish"]?.ToString(),
                        Prima = reader["Prima"]?.ToString(),
                        Varnish2 = reader["Varnish2"]?.ToString(),
                        Layout = reader["Layout"]?.ToString(),

                        PICode = reader["PICode"]?.ToString(),
                        OBPartNo = reader["OBPartNo"]?.ToString(),

                        InnerType = reader["InnerType"]?.ToString(),
                        InnerPartNo = reader["InnerPartNo"]?.ToString(),

                        InnerLayout = reader["InnerLayout"]?.ToString(),
                        QtyPerInnerLayer = reader["QtyPerInnerLayer"]?.ToString(),

                        CaseWtNet = reader["CaseWtNet"]?.ToString(),
                        CaseWtGross = reader["CaseWtGross"]?.ToString(),

                        InnersPerCase = reader["InnersPerCase"]?.ToString(),
                        CaseBarcode = reader["CaseBarcode"]?.ToString(),

                        PalletType = reader["PalletType"]?.ToString(),
                        PalletBarcode = reader["PalletBarcode"]?.ToString(),
                        PalletLayers = reader["PalletLayers"]?.ToString(),
                        QtyPerOuter = reader["QtyPerOuter"]?.ToString(),
                        CasesPerLayer = reader["CasesPerLayer"]?.ToString(),
                        QtyPerPallet = reader["QtyPerPallet"]?.ToString(),
                        CasesPerPallet = reader["CasesPerPallet"]?.ToString(),
                        PackingDescription = reader["PackingDescription"]?.ToString(),
                        
                        
                        PalletSize = reader["PalletSize"]?.ToString(),
                        PalletWtNet = reader["PalletWtNet"]?.ToString(),
                        PalletWtGross = reader["PalletWtGross"]?.ToString(),
                        PalletHeight = reader["PalletHeight"]?.ToString(),
                        PalletDimension = reader["PalletDimension"]?.ToString(),
                        HandStripping = reader["HandStrip"]?.ToString(),
                        ApprovalStatus = reader["ApprovalStatus"]?.ToString()

                    });
                   
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }
    }
}
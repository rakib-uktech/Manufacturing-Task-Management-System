using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_EditModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty] public long Id { get; set; }
        [BindProperty] public string WorkOrderNumber { get; set; }        // new
        [BindProperty] public string WorkOrderItem { get; set; }          // new
        [BindProperty] public string WorkOrderDescription { get; set; }   // new

        [BindProperty] public string Machine_Line { get; set; }
        [BindProperty] public string Machine_Name { get; set; }
        [BindProperty] public string Product_Line { get; set; }
        [BindProperty] public DateTime? Shift_Start_Time { get; set; }
        [BindProperty] public DateTime? Shift_End_Time { get; set; }
        [BindProperty] public bool Shift_Active { get; set; }
        [BindProperty] public int? Handover_Rating { get; set; }
        [BindProperty] public string Created_By { get; set; }
        [BindProperty] public string Authorized_By { get; set; }
        [BindProperty] public string? Comment { get; set; }


        public string ErrorMessage { get; set; }

        public List<SelectListItem> MachineLineList { get; set; } = new();
        public List<SelectListItem> MachineNameList { get; set; } = new();
        public List<SelectListItem> ProductLineList { get; set; } = new();

        // Work Orders for autofill
        public List<WorkOrderItem> WorkOrders { get; set; } = new();
        public Dictionary<string, WorkOrderItem> WorkOrderLookup { get; set; } = new();


        public IActionResult OnGet()
        {
            string idStr = Request.Query["id"];
            if (!long.TryParse(idStr, out long shiftId))
            {
                ErrorMessage = "Invalid Shift ID!";
                return Page();
            }

            Id = shiftId;

            try
            {
                // Load existing shift
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT machine_line, machine_name, product_line,
                           shift_start_time, shift_end_time, shift_active,
                           handover_rating, created_by, authorized_by, comment,
                           work_order_number, work_order_item, work_order_description
                    FROM shift
                    WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", shiftId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Machine_Line = reader["machine_line"]?.ToString();
                    Machine_Name = reader["machine_name"]?.ToString();
                    Product_Line = reader["product_line"]?.ToString();
                    Shift_Start_Time = reader.IsDBNull(reader.GetOrdinal("shift_start_time")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_start_time"));
                    Shift_End_Time = reader.IsDBNull(reader.GetOrdinal("shift_end_time")) ? null : reader.GetDateTime(reader.GetOrdinal("shift_end_time"));
                    Shift_Active = reader.GetBoolean(reader.GetOrdinal("shift_active"));
                    Handover_Rating = reader.IsDBNull(reader.GetOrdinal("handover_rating")) ? null : reader.GetInt32(reader.GetOrdinal("handover_rating"));
                    Created_By = reader["created_by"]?.ToString();
                    Authorized_By = reader["authorized_by"]?.ToString();
                    Comment = reader["comment"]?.ToString();

                    // New fields
                    WorkOrderNumber = reader["work_order_number"]?.ToString();
                    WorkOrderItem = reader["work_order_item"]?.ToString();
                    WorkOrderDescription = reader["work_order_description"]?.ToString();
                }
                reader.Close();

                // Load dropdowns from ODBC
                string connStr = _configuration.GetConnectionString("NetSuiteOdbc");
                using var connOdbc = new OdbcConnection(connStr);
                connOdbc.Open();

                // Machine Lines
                string sqlLines = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";
                using var cmdL = new OdbcCommand(sqlLines, connOdbc);
                using var readerL = cmdL.ExecuteReader();
                while (readerL.Read())
                    MachineLineList.Add(new SelectListItem
                    {
                        Value = readerL["name"].ToString(),
                        Text = readerL["name"].ToString(),
                        Selected = readerL["name"].ToString() == Machine_Line
                    });

                // Machine Names
                string sqlMachines = "SELECT DISTINCT name FROM CUSTOMLIST_TR_MACHINE_LIST ORDER BY name";
                using var cmdM = new OdbcCommand(sqlMachines, connOdbc);
                using var readerM = cmdM.ExecuteReader();
                while (readerM.Read())
                    MachineNameList.Add(new SelectListItem
                    {
                        Value = readerM["name"].ToString(),
                        Text = readerM["name"].ToString(),
                        Selected = readerM["name"].ToString() == Machine_Name
                    });

                // Product Lines
                string sqlProductLines = "SELECT DISTINCT fullname, name FROM classification ORDER BY fullname";
                using var cmdP = new OdbcCommand(sqlProductLines, connOdbc);
                using var readerP = cmdP.ExecuteReader();
                while (readerP.Read())
                    ProductLineList.Add(new SelectListItem
                    {
                        Value = readerP["fullname"].ToString(),
                        Text = readerP["fullname"].ToString(),
                        Selected = readerP["fullname"].ToString() == Product_Line,
                        Group = new SelectListGroup { Name = readerP["name"].ToString() } // For filtering by machine line
                    });
                // Load Work Orders for autofill
                LoadWorkOrders();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading shift: {ex.Message}";
            }

            return Page();
        }

        private void LoadWorkOrders()
        {
            string connStr = _configuration.GetConnectionString("NetSuiteOdbc");
            using var conn = new OdbcConnection(connStr);
            conn.Open();

            string lastMonthDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");

            string sql = $@"
        SELECT DISTINCT 
            a.tranid AS WorkOrder,
            mach.name AS MachineName,
            ptype.name AS MachineLine,
            cls.fullname AS ProductLine,
            c.itemid AS PartNumber,
            c.displayname AS ItemDescription
        FROM transaction a
        JOIN transactionline b ON a.id = b.transaction
        JOIN item c ON b.item = c.id
        LEFT JOIN classification cls ON cls.id = c.class
        LEFT JOIN CUSTOMLIST_TR_MACHINE_LIST mach ON a.custbody_tr_wo_machine = mach.recordid
        LEFT JOIN CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ptype ON c.custitemproduct_spec_producttype = ptype.recordid
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
                    MachineName = rdr["MachineName"]?.ToString(),
                    MachineLine = rdr["MachineLine"]?.ToString(),
                    ProductLine = rdr["ProductLine"]?.ToString(),
                    PartNumber = rdr["PartNumber"]?.ToString(),
                    ItemDescription = rdr["ItemDescription"]?.ToString()
                };

                WorkOrders.Add(wo);
                if (!string.IsNullOrWhiteSpace(wo.WorkOrder))
                    WorkOrderLookup[wo.WorkOrder] = wo;
            }
        }


        public IActionResult OnPost()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                UPDATE shift
                SET machine_line=@line,
                    machine_name=@name,
                    product_line=@pline,
                    shift_start_time=@start,
                    shift_end_time=@end,
                    shift_active=@active,
                    handover_rating=@rating,
                    created_by=@created,
                    authorized_by=@auth,
                    comment=@comment,
                    work_order_number=@wonum,        -- new
                    work_order_item=@woitem,         -- new
                    work_order_description=@wodesc   -- new
                WHERE id=@id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@line", Machine_Line ?? "");
                cmd.Parameters.AddWithValue("@name", Machine_Name ?? "");
                cmd.Parameters.AddWithValue("@pline", Product_Line ?? "");
                cmd.Parameters.AddWithValue("@start", (object?)Shift_Start_Time ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@end", (object?)Shift_End_Time ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@active", Shift_Active);
                cmd.Parameters.AddWithValue("@rating", (object?)Handover_Rating ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@created", Created_By ?? "");
                cmd.Parameters.AddWithValue("@auth", Authorized_By ?? "");
                cmd.Parameters.AddWithValue("@comment", Comment ?? "");

                // New parameters
                cmd.Parameters.AddWithValue("@wonum", WorkOrderNumber ?? "");
                cmd.Parameters.AddWithValue("@woitem", WorkOrderItem ?? "");
                cmd.Parameters.AddWithValue("@wodesc", WorkOrderDescription ?? "");

                cmd.Parameters.AddWithValue("@id", Id);


                cmd.ExecuteNonQuery();

                return RedirectToPage("/Shift_Report");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating shift: {ex.Message}";
                return Page();
            }
        }
    }
}

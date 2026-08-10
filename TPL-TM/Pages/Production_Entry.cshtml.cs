using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TPL_TM.Data;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_EntryModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public Production_EntryModel(
            IConfiguration configuration,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _context = context;
            _userManager = userManager;
        }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        [BindProperty]
        public ProductionInfo ProductionInfo { get; set; } = new();

        // Autofill from Shift
        public ShiftAutofill ShiftData { get; set; } = new();

        public List<ShiftInfo> ActiveShifts { get; set; } = new();
        // Totals (optional, like dashboard)
        public DateTime? LastQualityCheckedOn =>
        ActiveShifts
       .Select(s => s.LastQualityCheckTime)
       .Where(t => t.HasValue)
       .OrderByDescending(t => t)
       .FirstOrDefault();

        // -------------------------------------------------
        // LOAD SHIFT DATA (Dashboard → Entry)
        // -------------------------------------------------
        public async Task<IActionResult> OnGetAsync(long? shiftId)
        {
            if (!shiftId.HasValue)
                return RedirectToPage("/Shift_Dashboard");

            ProductionInfo.Shift_Id = shiftId.Value;
            await LoadLoggedInUserShiftAsync();

            using var conn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // 1️⃣ Load the active shift (SINGLE)
            string shiftSql = @"
                SELECT id, work_order_number, work_order_item, work_order_description,
                       case_qty, machine_line, machine_name, product_line,
                       shift_start_time, shift_end_time, created_by
                FROM shift
                WHERE id = @id AND shift_active = true";

            ShiftInfo activeShift = null;

            using (var cmd = new NpgsqlCommand(shiftSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", shiftId.Value);
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return RedirectToPage("/Shift_Dashboard");

                activeShift = new ShiftInfo
                {
                    Shift_Id = reader["id"].ToString(),
                    WorkOrderNumber = reader["work_order_number"].ToString(),
                    WorkOrderItem = reader["work_order_item"].ToString(),
                    WorkOrderDescription = reader["work_order_description"].ToString(),
                    Created_By = reader["created_by"].ToString()
                };

                ShiftData.WorkOrderNumber = activeShift.WorkOrderNumber;
                ShiftData.PartNumber = activeShift.WorkOrderItem;
                ShiftData.ItemDescription = activeShift.WorkOrderDescription;
                ShiftData.CaseQty = reader["case_qty"] as int?;
                ShiftData.MachineLine = reader["machine_line"].ToString();
                ShiftData.MachineName = reader["machine_name"].ToString();
                ShiftData.ProductLine = reader["product_line"].ToString();

                ProductionInfo.Timestamp_Start = reader["shift_start_time"] as DateTime?;
                ProductionInfo.Timestamp_End = reader["shift_end_time"] as DateTime?;
            }

            // 2️⃣ Load production entries FOR THIS SHIFT
            string productionSql = @"
            SELECT id, wo_number, part_number, item_description,
                   product_count, created_by,
                   timestamp_start, timestamp_end, batch_identifier
            FROM production_count
            WHERE shift_id = @shiftId
            ORDER BY id DESC";

            using (var prodCmd = new NpgsqlCommand(productionSql, conn))
            {
                prodCmd.Parameters.AddWithValue("@shiftId", shiftId.Value);
                using var prodReader = await prodCmd.ExecuteReaderAsync();

                while (await prodReader.ReadAsync())
                {
                    activeShift.ProductionEntries.Add(new ProductionInfo
                    {
                        Id = Convert.ToInt64(prodReader["id"]),
                        Shift_Id = shiftId.Value,
                        Wo_Number = prodReader["wo_number"].ToString(),
                        Part_Number = prodReader["part_number"].ToString(),
                        Item_Description = prodReader["item_description"].ToString(),
                        Product_Count = prodReader["product_count"] == DBNull.Value
                        ? 0
                        : Convert.ToInt64(prodReader["product_count"]),

                        Username = prodReader["created_by"].ToString(),
                        Timestamp_Start = prodReader["timestamp_start"] as DateTime?,
                        Timestamp_End = prodReader["timestamp_end"] as DateTime?,
                        Batch_Identifier = prodReader["batch_identifier"].ToString()
                    });
                }
            }

            // 3️⃣ Attach to page
            ActiveShifts = new List<ShiftInfo> { activeShift };

            return Page();
        }


        // -------------------------------------------------
        // SAVE PRODUCTION
        // -------------------------------------------------
        public async Task<IActionResult> OnPostAsync()
        {
            await LoadLoggedInUserShiftAsync();

            try
            {
                using var conn = new NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                INSERT INTO production_count
                (shift_id, shift_letter, wo_number, batch_identifier,
                 timestamp_start, timestamp_end, created_by,
                 product_count, machine_line, machine_name,
                 product_line, part_number, item_description)
                VALUES
                (@ShiftId, @ShiftLetter, @WoNumber, @Batch,
                 @Start, @End, @User,
                 @Count, @MachineLine, @MachineName,
                 @ProductLine, @PartNumber, @Description)";

                using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@ShiftId", ProductionInfo.Shift_Id);
                cmd.Parameters.AddWithValue("@ShiftLetter", ProductionInfo.Shift_Letter ?? "");
                cmd.Parameters.AddWithValue("@WoNumber", Request.Form["Wo_Number"].ToString());
                cmd.Parameters.AddWithValue("@Batch", Request.Form["Batch_Identifier"].ToString());
                cmd.Parameters.AddWithValue("@Start", ProductionInfo.Timestamp_Start ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@End", ProductionInfo.Timestamp_End ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@User", User.Identity?.Name ?? "System");
                cmd.Parameters.AddWithValue("@Count",
                    long.TryParse(Request.Form["Product_Count"], out var c) ? c : 0);
                cmd.Parameters.AddWithValue("@MachineLine", Request.Form["Machine_Line"].ToString());
                cmd.Parameters.AddWithValue("@MachineName", Request.Form["Machine_Name"].ToString());
                cmd.Parameters.AddWithValue("@ProductLine", Request.Form["Product_Line"].ToString());
                cmd.Parameters.AddWithValue("@PartNumber", Request.Form["Part_Number"].ToString());
                cmd.Parameters.AddWithValue("@Description", Request.Form["Item_Description"].ToString());

                cmd.ExecuteNonQuery();
                
                TempData["ProductionSaved"] = true;

                return RedirectToPage(
                    "/Production_Entry",
                    new { shiftId = ProductionInfo.Shift_Id }
                );

            }

            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        private async Task LoadLoggedInUserShiftAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var assignment = await _context.UserShiftAssignment
                .Include(x => x.ShiftInformation)
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            ProductionInfo.Shift_Letter =
                assignment?.ShiftInformation?.Name ?? "N/A";
        }
    }

    // -------------------------------------------------
    // SUPPORT CLASSES (DO NOT DELETE)
    // -------------------------------------------------
    public class ShiftAutofill
    {
        public string WorkOrderNumber { get; set; }
        public string PartNumber { get; set; }
        public string ItemDescription { get; set; }
        public int? CaseQty { get; set; }
        public string MachineLine { get; set; }
        public string MachineName { get; set; }
        public string ProductLine { get; set; }
    }

    public class WorkOrderItem
    {
        public string WorkOrder { get; set; }
        public int? InternalId { get; set; }
        public string PartNumber { get; set; }
        public string ItemDescription { get; set; }
        public string ProductLine { get; set; }
        public string MachineLine { get; set; }
        public string MachineName { get; set; }
        public string ProductType { get; set; }
        public int? CaseQty { get; set; }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Downtime_EntryModel : PageModel
    {
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public DowntimeInfo DowntimeInfo { get; set; } = new DowntimeInfo();

        // ✅ New property to hold downtime reasons
        public List<DowntimeReason> DowntimeReasons { get; set; } = new List<DowntimeReason>();
        public List<DowntimeEntryView> DowntimeEntries { get; set; } = new();

        public string MachineType { get; set; }

        private readonly IConfiguration _configuration;
        public string ConnectionString { get; private set; }

        public Downtime_EntryModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ✅ Load reasons + shift on GET
        public void OnGet(long? shiftId)
        {
            if (!shiftId.HasValue)
                return;

            DowntimeInfo.Shift = shiftId.Value;
            ConnectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            // 1️⃣ Get machine type
            const string shiftSql = @"
        SELECT machine_line
        FROM shift
        WHERE id = @shiftId";

            using (var shiftCmd = new NpgsqlCommand(shiftSql, connection))
            {
                shiftCmd.Parameters.AddWithValue("@shiftId", shiftId.Value);
                MachineType = shiftCmd.ExecuteScalar()?.ToString();
            }

            // 2️⃣ Load downtime reasons
            const string reasonSql = @"
        SELECT id, reason_name, description, machine_type
        FROM downtime_reason
        WHERE active = TRUE
          AND (machine_type IS NULL
               OR machine_type = ''
               OR machine_type = @machineType)
        ORDER BY reason_name;";

            using (var reasonCmd = new NpgsqlCommand(reasonSql, connection))
            {
                reasonCmd.Parameters.AddWithValue("@machineType", MachineType ?? "");

                using var reader = reasonCmd.ExecuteReader();
                while (reader.Read())
                {
                    DowntimeReasons.Add(new DowntimeReason
                    {
                        Id = reader.GetInt64(0),
                        ReasonName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        MachineType = reader.IsDBNull(3) ? null : reader.GetString(3)
                    });
                }
            } // ✅ reader CLOSED here

            // 3️⃣ Load downtime entries
            const string downtimeSql = @"
        SELECT d.id,
               d.downtime,
               r.reason_name,
               d.comment,
               d.created_by,
               d.created_on
        FROM downtime d
        LEFT JOIN downtime_reason r ON r.id = d.reason_id
        WHERE d.shift = @shiftId
        ORDER BY d.created_on DESC;";

            using (var downtimeCmd = new NpgsqlCommand(downtimeSql, connection))
            {
                downtimeCmd.Parameters.AddWithValue("@shiftId", shiftId.Value);

                using var dtReader = downtimeCmd.ExecuteReader();
                while (dtReader.Read())
                {
                    DowntimeEntries.Add(new DowntimeEntryView
                    {
                        Id = dtReader.GetInt64(0),
                        Downtime = dtReader.IsDBNull(1) ? null : dtReader.GetInt32(1),
                        ReasonName = dtReader.IsDBNull(2) ? "-" : dtReader.GetString(2),
                        Comment = dtReader.IsDBNull(3) ? "" : dtReader.GetString(3),
                        CreatedBy = dtReader.IsDBNull(4) ? "" : dtReader.GetString(4),
                        CreatedOn = dtReader.IsDBNull(5) ? null : dtReader.GetDateTime(5)
                    });
                }
            }
        }


        // ✅ Save downtime entry
        public IActionResult OnPost()
        {
            try
            {
                var rawValue = Request.Form["DowntimeValue"];
                var unit = Request.Form["DowntimeUnit"];
                var reasonId = Request.Form["ReasonId"]; // from dropdown

                int? downtimeMinutes = null;
                if (int.TryParse(rawValue, out var value))
                {
                    downtimeMinutes = unit == "hours" ? value * 60 : value;
                }

                DowntimeInfo = new DowntimeInfo
                {
                    Downtime = downtimeMinutes,
                    ReasonId = long.TryParse(reasonId, out var rid) ? rid : (long?)null,
                    Shift = long.TryParse(Request.Form["Shift"], out var sh) ? sh : (long?)null,
                    Comment = Request.Form["Comment"],
                    Created_On = DateTime.Now,
                    Created_By = User.Identity?.Name ?? "Unknown"
                };

                ConnectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO downtime
                        (downtime, reason_id, created_on, created_by, shift, comment)
                        VALUES (@Downtime, @ReasonId, @Created_On, @Created_By, @Shift, @Comment)";

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Downtime", (object?)DowntimeInfo.Downtime ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ReasonId", (object?)DowntimeInfo.ReasonId ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Created_On", (object?)DowntimeInfo.Created_On ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Created_By", (object?)DowntimeInfo.Created_By ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Shift", (object?)DowntimeInfo.Shift ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Comment", (object?)DowntimeInfo.Comment ?? DBNull.Value);

                        command.ExecuteNonQuery();
                    }
                }

                SuccessMessage = "✅ Downtime record added successfully.";
                return RedirectToPage("/Shift_Dashboard");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Failed to insert record: {ex.Message}";
                return Page();
            }
        }
    }

    // ✅ Updated model classes
    public class DowntimeInfo
    {
        public int? Downtime { get; set; }
        public long? ReasonId { get; set; }     // FK value, if you need it
        public string Reason { get; set; }      // Human-readable reason name
        public DateTime? Created_On { get; set; }
        public string Created_By { get; set; }
        public long? Shift { get; set; }
        public string Comment { get; set; }
    }


    public class DowntimeReason
    {
        public long Id { get; set; }
        public string ReasonName { get; set; }
        public string Description { get; set; }
        public string MachineType { get; set; }
    }
    public class DowntimeEntryView
    {
        public long Id { get; set; }
        public int? Downtime { get; set; } // minutes
        public string ReasonName { get; set; }
        public string Comment { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
    }

}

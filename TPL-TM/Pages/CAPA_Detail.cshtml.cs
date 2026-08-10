using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Npgsql;
using System.Net;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class CAPA_DetailModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        
        [BindProperty]
        public CAPAInfo CAPA { get; set; } = new();        

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }
        [BindProperty(SupportsGet = true)]
        public bool Edit { get; set; }

        public bool IsReadOnly => !Edit || CAPA.Status == "Closed";
        public CAPA_DetailModel(
         IConfiguration configuration,
         UserManager<IdentityUser> userManager)
            {
                _configuration = configuration;
                _userManager = userManager;
            }
        public List<CorrectiveActionInfo> CorrectiveActions { get; set; } = new();

        public List<PreventiveActionInfo> PreventiveActions { get; set; } = new();

        public List<CAPAHistoryInfo> History { get; set; } = new();

        public int PercentComplete { get; set; }

        [BindProperty]
        public string RootCause { get; set; }

        [BindProperty]
        public string EffectivenessReview { get; set; }

        [BindProperty]
        public string CorrectiveActionDescription { get; set; }

        [BindProperty]
        public string CorrectiveAssignedTo { get; set; }

        [BindProperty]
        public DateTime? CorrectiveTargetDate { get; set; }


        [BindProperty]
        public string PreventiveActionDescription { get; set; }

        [BindProperty]
        public string PreventiveAssignedTo { get; set; }

        [BindProperty]
        public DateTime? PreventiveTargetDate { get; set; }

        public List<CAPAPhoto> Photos { get; set; } = new();
        public List<string> UserList { get; set; } = new();
        
        private async Task LoadUsers()
        {
            UserList = await _userManager.Users
                .OrderBy(u => u.UserName)
                .Select(u => u.UserName)
                .ToListAsync();
        }
        public async Task<IActionResult> OnGetAsync()
        {
            string connString =
                _configuration.GetConnectionString("DefaultConnection");

            using var con = new NpgsqlConnection(connString);

            con.Open();

            string sql = @"
                SELECT *
                FROM capa_master
                WHERE id=@id";

            using (var cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@id", Id);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    CAPA = new CAPAInfo
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        CAPA_No = reader["capa_no"].ToString(),
                        Incident_Type = reader["incident_type"].ToString(),
                        Severity = reader["severity"].ToString(),
                        Machine_Name = reader["machine_name"].ToString(),
                        Department = reader["department"].ToString(),
                        Title = reader["title"].ToString(),
                        Description = reader["description"].ToString(),
                        Immediate_Action = reader["immediate_action"].ToString(),
                        Root_Cause = reader["root_cause"].ToString(),
                        Status = reader["status"].ToString(),
                        Assigned_To = reader["assigned_to"].ToString(),
                        Reported_By = reader["reported_by"].ToString(),
                        Effectiveness_Review = reader["effectiveness_review"]?.ToString(),
                        Target_Date = reader["target_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["target_date"]),
                        Incident_Date = Convert.ToDateTime(reader["incident_date"])
                    };

                    RootCause = CAPA.Root_Cause;
                    EffectivenessReview = CAPA.Effectiveness_Review;
                }
            }

            LoadCorrectiveActions(con);
            LoadPreventiveActions(con);
            LoadHistory(con);
            LoadPhotos(con);

            CalculateProgress();

            await LoadUsers();

            return Page();
        }

        public IActionResult OnPostSaveCAPA()
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

             string sql = @"
                UPDATE capa_master
                SET
                    incident_type=@incident_type,
                    severity=@severity,
                    machine_name=@machine_name,
                    department=@department,
                    title=@title,
                    description=@description,
                    immediate_action=@immediate_action,
                    assigned_to=@assigned_to,
                    target_date=@target_date
                WHERE id=@id
                  AND status<>'Closed'";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", CAPA.Id);
            cmd.Parameters.AddWithValue("@incident_type", CAPA.Incident_Type ?? "");
            cmd.Parameters.AddWithValue("@severity", CAPA.Severity ?? "");
            cmd.Parameters.AddWithValue("@machine_name", CAPA.Machine_Name ?? "");
            cmd.Parameters.AddWithValue("@department", CAPA.Department ?? "");
            cmd.Parameters.AddWithValue("@title", CAPA.Title ?? "");
            cmd.Parameters.AddWithValue("@description", CAPA.Description ?? "");
            cmd.Parameters.AddWithValue("@immediate_action", CAPA.Immediate_Action ?? "");
            cmd.Parameters.AddWithValue("@assigned_to", CAPA.Assigned_To ?? "");
            cmd.Parameters.AddWithValue("@target_date",
                CAPA.Target_Date ?? (object)DBNull.Value);

            cmd.ExecuteNonQuery();

            SaveHistory(con,
                "CAPA Updated",
                "CAPA details edited");

            return RedirectToPage(new
            {
                id = CAPA.Id
            });
        }

        private void LoadPhotos(NpgsqlConnection con)
        {
            string sql = @"
        SELECT
            id,
            capa_id,
            file_name,
            content_type,
            upload_date
        FROM capa_photos
        WHERE capa_id=@capa_id
        ORDER BY id";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@capa_id", Id);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Photos.Add(new CAPAPhoto
                {
                    Id = Convert.ToInt32(reader["id"]),
                    CAPA_Id = Convert.ToInt32(reader["capa_id"]),
                    File_Name = reader["file_name"]?.ToString(),
                    Content_Type = reader["content_type"]?.ToString(),
                    Upload_Date = Convert.ToDateTime(reader["upload_date"])
                });
            }
        }

        public IActionResult OnGetPhoto(int photoId)
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                SELECT photo_data, content_type
                FROM capa_photos
                WHERE id=@id";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", photoId);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound();

            byte[] photo = (byte[])reader["photo_data"];

            string contentType =
                reader["content_type"]?.ToString()
                ?? "image/jpeg";

            return File(photo, contentType);
        }
        private void LoadCorrectiveActions(NpgsqlConnection con)
        {
            string sql = @"
                SELECT *
                FROM capa_corrective_action
                WHERE capa_id=@id
                ORDER BY id DESC";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", Id);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                CorrectiveActions.Add(new CorrectiveActionInfo
                {
                    Id = Convert.ToInt32(reader["id"]),
                    Action_Description = reader["action_description"].ToString(),
                    Assigned_To = reader["assigned_to"].ToString(),
                    Status = reader["status"].ToString(),
                    Target_Date = reader["target_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["target_date"])
                });
            }

            reader.Close();
        }

        private void LoadPreventiveActions(NpgsqlConnection con)
        {
            string sql = @"
                SELECT *
                FROM capa_preventive_action
                WHERE capa_id=@id
                ORDER BY id DESC";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", Id);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                PreventiveActions.Add(new PreventiveActionInfo
                {
                    Id = Convert.ToInt32(reader["id"]),
                    Action_Description = reader["action_description"].ToString(),
                    Assigned_To = reader["assigned_to"].ToString(),
                    Status = reader["status"].ToString(),
                    Target_Date = reader["target_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["target_date"])
                });
            }
        }

        private void LoadHistory(NpgsqlConnection con)
        {
            string sql = @"
                SELECT *
                FROM capa_history
                WHERE capa_id=@id
                ORDER BY action_date DESC";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", Id);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                History.Add(new CAPAHistoryInfo
                {
                    Id = Convert.ToInt32(reader["id"]),
                    Action_Type = reader["action_type"].ToString(),
                    Remarks = reader["remarks"].ToString(),
                    Action_By = reader["action_by"].ToString(),
                    Action_Date = Convert.ToDateTime(reader["action_date"])
                });
            }
        }
        private void CalculateProgress()
        {
            double progress = 0;

            // -------------------------
            // 1. Root Cause (25%)
            // -------------------------
            if (!string.IsNullOrWhiteSpace(CAPA.Root_Cause))
                progress += 25;

            // -------------------------
            // 2. Corrective Actions (25%)
            // -------------------------
            if (CorrectiveActions.Count > 0)
            {
                double completed = CorrectiveActions.Count(x => x.Status == "Completed");
                progress += (completed / CorrectiveActions.Count) * 25;
            }

            // -------------------------
            // 3. Preventive Actions (25%)
            // -------------------------
            if (PreventiveActions.Count > 0)
            {
                double completed = PreventiveActions.Count(x => x.Status == "Completed");
                progress += (completed / PreventiveActions.Count) * 25;
            }

            // -------------------------
            // 4. Verification (25%)
            // -------------------------
            if (!string.IsNullOrWhiteSpace(CAPA.Effectiveness_Review))
                progress += 25;

            // -------------------------
            // Final clamp
            // -------------------------
            PercentComplete = (int)Math.Round(progress);

            if (CAPA.Status == "Closed")
                PercentComplete = 100;
        }

        public IActionResult OnPostSaveInvestigation()
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                UPDATE capa_master
                SET
                    root_cause=@root_cause,
                    status='Investigation'
                WHERE id=@id";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@root_cause", RootCause ?? "");
            cmd.Parameters.AddWithValue("@id", Id);

            cmd.ExecuteNonQuery();

            return RedirectToPage(new { id = Id });
        }

        public IActionResult OnPostSaveEffectiveness()
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                UPDATE capa_master
                SET
                    effectiveness_review=@review,
                    status='Verification'
                WHERE id=@id";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@review",
                EffectivenessReview ?? "");

            cmd.Parameters.AddWithValue("@id", Id);

            cmd.ExecuteNonQuery();

            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostCloseCAPAAsync(string ClosureRemarks)
        {
            try
            {
                using var con = new NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                await con.OpenAsync();


                // Check investigation/root cause completed
                string checkSql = @"
                    SELECT root_cause
                    FROM capa_master
                    WHERE id=@id";


                string rootCause = "";

                using (var checkCmd = new NpgsqlCommand(checkSql, con))
                {
                    checkCmd.Parameters.AddWithValue("@id", Id);

                    var result = await checkCmd.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        rootCause = result.ToString();
                    }
                }


                if (string.IsNullOrWhiteSpace(rootCause))
                {
                    TempData["Error"] =
                        "CAPA cannot be closed. Investigation / Root Cause analysis is incomplete.";

                    return RedirectToPage(new { id = Id });
                }



                // Close CAPA
                string sql = @"
                    UPDATE capa_master
                    SET status='Closed',
                        closed_date=CURRENT_TIMESTAMP,
                        approved_by=@user
                    WHERE id=@id";


                using var cmd = new NpgsqlCommand(sql, con);

                cmd.Parameters.AddWithValue("@id", Id);

                cmd.Parameters.AddWithValue("@user",
                    User.Identity?.Name ?? "System");


                var rows = await cmd.ExecuteNonQueryAsync();


                if (rows > 0)
                {
                    SaveHistory(
                        con,
                        "CAPA Closed",
                        ClosureRemarks ?? "CAPA closed");


                    LoadCAPAForEmail(con);


                    await SendCAPAClosureEmailAsync(
                        ClosureRemarks);


                    TempData["Success"] =
                        "CAPA closed successfully.";
                }


                return RedirectToPage(new { id = Id });

            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;

                return RedirectToPage(new { id = Id });
            }
        }
        private void LoadCAPAForEmail(NpgsqlConnection con)
        {
            string sql = @"
                SELECT
                    id,
                    capa_no,
                    title,
                    assigned_to,
                    incident_type,
                    severity
                FROM capa_master
                WHERE id=@id";


            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", Id);


            using var reader = cmd.ExecuteReader();


            if (reader.Read())
            {
                CAPA.Id =
                    Convert.ToInt32(reader["id"]);

                CAPA.CAPA_No =
                    reader["capa_no"].ToString();

                CAPA.Title =
                    reader["title"].ToString();

                CAPA.Assigned_To =
                    reader["assigned_to"].ToString();

                CAPA.Incident_Type =
                    reader["incident_type"].ToString();

                CAPA.Severity =
                    reader["severity"].ToString();
            }
        }

        private async Task SendCAPAClosureEmailAsync(string closureRemarks)
        {
            //var recipients = await GetRecipientsByRoleAsync("Management");
            var recipients = new List<string>();


            // Get CAPA owner email
            if (!string.IsNullOrWhiteSpace(CAPA.Assigned_To))
            {
                var owner = await _userManager
                    .FindByNameAsync(CAPA.Assigned_To);

                if (owner != null &&
                    !string.IsNullOrWhiteSpace(owner.Email))
                {
                    recipients.Add(owner.Email);
                }
            }

            // Optional: Management notification
            /*
            recipients.Add("management.report@transcendpackaging.co.uk");
            */


            if (recipients.Count == 0)
            {
                return;
            }

            var html = $@"
            <!DOCTYPE html>
            <html>
            <body style='margin:0;padding:0;background:#f4f6f9;
                         font-family:Segoe UI,Arial,sans-serif;'>

            <div style='max-width:700px;
                        margin:20px auto;
                        background:#ffffff;
                        border-radius:12px;
                        overflow:hidden;
                        box-shadow:0 2px 10px rgba(0,0,0,.08);'>

                <!-- Header -->
                <div style='background:#198754;
                            padding:25px;
                            color:white;'>

                    <div style='font-size:24px;
                                font-weight:700;'>
                        ✅ CAPA Closed
                    </div>

                    <div style='font-size:13px;
                                margin-top:5px;'>
                        Corrective & Preventive Action Notification
                    </div>
                </div>

                <!-- Body -->
                <div style='padding:25px;'>

                    <p>
                        The following CAPA has been successfully closed.
                    </p>

                    <table style='width:100%;
                                  border-collapse:collapse;
                                  margin-top:15px;'>

                        <tr>
                            <td style='padding:10px;
                                       font-weight:600;
                                       width:180px;
                                       background:#f8f9fa;'>
                                CAPA ID
                            </td>
                            <td style='padding:10px;border:1px solid #eee;'>
                                {CAPA.Id}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:10px;
                                       font-weight:600;
                                       background:#f8f9fa;'>
                                Title
                            </td>
                            <td style='padding:10px;border:1px solid #eee;'>
                                {WebUtility.HtmlEncode(CAPA.Title)}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:10px;
                                       font-weight:600;
                                       background:#f8f9fa;'>
                                Status
                            </td>
                            <td style='padding:10px;border:1px solid #eee;'>
                                <span style='background:#d1e7dd;
                                             color:#0f5132;
                                             padding:4px 10px;
                                             border-radius:5px;
                                             font-weight:600;'>
                                    Closed
                                </span>
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:10px;
                                       font-weight:600;
                                       background:#f8f9fa;'>
                                Closed By
                            </td>
                            <td style='padding:10px;border:1px solid #eee;'>
                                {User.Identity?.Name}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:10px;
                                       font-weight:600;
                                       background:#f8f9fa;'>
                                Closure Date
                            </td>
                            <td style='padding:10px;border:1px solid #eee;'>
                                {DateTime.Now:dd MMM yyyy HH:mm}
                            </td>
                        </tr>
                    </table>

                    <div style='margin-top:20px;'>
                        <div style='font-weight:600;
                                    margin-bottom:8px;'>
                            Closure Remarks
                        </div>

                        <div style='background:#f8f9fa;
                                    border-left:4px solid #198754;
                                    padding:12px;
                                    border-radius:4px;'>
                            {WebUtility.HtmlEncode(closureRemarks)}
                        </div>
                    </div>

                    <div style='margin-top:25px;text-align:center;'>
                        <a href='https://tpltm.co.uk/CAPA_Detail?id={CAPA.Id}'
                           style='background:#0d6efd;
                                  color:white;
                                  text-decoration:none;
                                  padding:12px 20px;
                                  border-radius:6px;
                                  font-weight:600;'>
                            View CAPA
                        </a>
                    </div>

                </div>

                <!-- Footer -->
                <div style='background:#f8f9fa;
                            padding:15px;
                            text-align:center;
                            font-size:12px;
                            color:#6c757d;'>

                    Generated by TPL CAPA Management System<br/>
                    {DateTime.Now:dd MMM yyyy HH:mm}
                </div>

            </div>

            </body>
            </html>";

            await SendEmailGraphAsync(
                $"✅ CAPA Closed - #{CAPA.Id} - {CAPA.Title}",
                html,
                recipients);
        }

        public async Task<IActionResult> OnPostAddCorrectiveActionAsync()
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                INSERT INTO capa_corrective_action
                (
                    capa_id,
                    action_description,
                    assigned_to,
                    target_date,
                    status
                )
                VALUES
                (
                    @capa_id,
                    @action_description,
                    @assigned_to,
                    @target_date,
                    'Open'
                )";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@capa_id", Id);
            cmd.Parameters.AddWithValue("@action_description",
                CorrectiveActionDescription ?? "");

            cmd.Parameters.AddWithValue("@assigned_to",
                CorrectiveAssignedTo ?? "");

            cmd.Parameters.AddWithValue("@target_date",
                CorrectiveTargetDate ?? (object)DBNull.Value);

            cmd.ExecuteNonQuery();
            await SendActionAssignedEmailAsync(
                GetCAPANumber(Id),
                "Corrective Action",
                CorrectiveActionDescription,
                CorrectiveAssignedTo);

            SaveHistory(
                con,
                "Corrective Action Added",
                CorrectiveActionDescription);

            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostAddPreventiveActionAsync()
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                INSERT INTO capa_preventive_action
                (
                    capa_id,
                    action_description,
                    assigned_to,
                    target_date,
                    status
                )
                VALUES
                (
                    @capa_id,
                    @action_description,
                    @assigned_to,
                    @target_date,
                    'Open'
                )";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@capa_id", Id);

            cmd.Parameters.AddWithValue("@action_description",
                PreventiveActionDescription ?? "");

            cmd.Parameters.AddWithValue("@assigned_to",
                PreventiveAssignedTo ?? "");

            cmd.Parameters.AddWithValue("@target_date",
                PreventiveTargetDate ?? (object)DBNull.Value);

            cmd.ExecuteNonQuery();

            await SendActionAssignedEmailAsync(
                GetCAPANumber(Id),
                "Preventive Action",
                PreventiveActionDescription,
                PreventiveAssignedTo);
            SaveHistory(
                con,
                "Preventive Action Added",
                PreventiveActionDescription);

            return RedirectToPage(new { id = Id });
        }
        private string GetCAPANumber(int id)
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
        SELECT capa_no
        FROM capa_master
        WHERE id=@id";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteScalar()?.ToString() ?? "";
        }


        private async Task SendActionAssignedEmailAsync(
            string capaNo,
            string actionType,
            string description,
            string assignedTo)
        {
            var user = await _userManager.FindByNameAsync(assignedTo);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return;


            string html = $@"
                <!DOCTYPE html>
                <html>
                <body style='font-family:Segoe UI,Arial;background:#f4f6f9;padding:20px;'>

                <div style='max-width:650px;margin:auto;background:white;
                            border-radius:10px;padding:25px;
                            box-shadow:0 2px 10px rgba(0,0,0,.1)'>

                    <h2 style='color:#0d6efd'>
                        📌 CAPA Action Assigned
                    </h2>

                    <p>
                        A new {actionType} action has been assigned to you.
                    </p>

                    <table style='width:100%;border-collapse:collapse'>

                        <tr>
                            <td style='padding:8px;font-weight:bold'>
                                CAPA No
                            </td>
                            <td style='padding:8px'>
                                {capaNo}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:8px;font-weight:bold'>
                                Action Type
                            </td>
                            <td style='padding:8px'>
                                {actionType}
                            </td>
                        </tr>


                        <tr>
                            <td style='padding:8px;font-weight:bold'>
                                Description
                            </td>
                            <td style='padding:8px'>
                                {WebUtility.HtmlEncode(description)}
                            </td>
                        </tr>

                    </table>


                    <div style='margin-top:25px;text-align:center'>

                        <a href='https://tpltm.co.uk/CAPA_Detail?id={Id}'
                           style='background:#0d6efd;color:white;
                                  padding:12px 20px;
                                  text-decoration:none;
                                  border-radius:6px'>

                            Open CAPA

                        </a>

                    </div>


                </div>

                </body>
                </html>";


            await SendEmailGraphAsync(
                $"📌 CAPA {actionType} Assigned - {capaNo}",
                html,
                new[] { user.Email });
        }

        private void SaveHistory(
            NpgsqlConnection con,
            string actionType,
            string remarks)
        {
            string sql = @"
                INSERT INTO capa_history
                (
                    capa_id,
                    action_type,
                    remarks,
                    action_by
                )
                VALUES
                (
                    @capa_id,
                    @action_type,
                    @remarks,
                    @action_by
                )";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@capa_id", Id);

            cmd.Parameters.AddWithValue("@action_type",
                actionType);

            cmd.Parameters.AddWithValue("@remarks",
                remarks ?? "");

            cmd.Parameters.AddWithValue("@action_by",
                User.Identity?.Name ?? "System");

            cmd.ExecuteNonQuery();
        }

        private async Task SendEmailGraphAsync(string subject,string htmlBody,IEnumerable<string> recipients)
        {
            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];
            var senderEmail = _configuration["AzureAd:SenderEmail"];

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var graphClient = new GraphServiceClient(credential);

            var message = new Microsoft.Graph.Models.Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                ToRecipients = recipients
                    .Select(email => new Recipient { EmailAddress = new EmailAddress { Address = email } })
                    .ToList()
            };

            await graphClient.Users[senderEmail]
                .SendMail
                .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                });
        }

        private async Task<string[]> GetRecipientsByRoleAsync(string role)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);

            return usersInRole
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email)
                .Distinct()
                .ToArray();
        }

    }

    public class CorrectiveActionInfo
    {
        public int Id { get; set; }
        public int Capa_Id { get; set; }
        public string Action_Description { get; set; }
        public string Assigned_To { get; set; }
        public DateTime? Target_Date { get; set; }
        public DateTime? Completion_Date { get; set; }
        public string Status { get; set; }
        public string Comments { get; set; }
    }

    public class PreventiveActionInfo
    {
        public int Id { get; set; }
        public int Capa_Id { get; set; }
        public string Action_Description { get; set; }
        public string Assigned_To { get; set; }
        public DateTime? Target_Date { get; set; }
        public DateTime? Completion_Date { get; set; }
        public string Status { get; set; }
        public string Comments { get; set; }
    }

    public class CAPAHistoryInfo
    {
        public int Id { get; set; }
        public string Action_Type { get; set; }
        public string Remarks { get; set; }
        public string Action_By { get; set; }
        public DateTime Action_Date { get; set; }
    }
}
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Report_IncidentModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        
        

        public List<CAPAInfo> MachineList { get; set; } = new();
        public List<IncidentTemplate> IncidentTemplates { get; set; } = new();
        public List<string> UserList { get; set; } = new();

        [BindProperty]
        public CAPAInfo CAPA { get; set; } = new();
        [BindProperty]
        public List<IFormFile> Photos { get; set; } = new();
        public List<CAPAPhoto> ExistingPhotos { get; set; } = new();

        private readonly UserManager<IdentityUser> _userManager;
        public Report_IncidentModel(
                IConfiguration configuration,
                UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }
        private byte[] GetUploadedFileBytes(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                using var ms = new MemoryStream();

                file.CopyTo(ms);

                return ms.ToArray();
            }

            return null;
        }
        public async Task OnGetAsync(int? id)
        {
            LoadMachines();
            LoadIncidentTemplates();
            await LoadUsers();


            if (id.HasValue)
            {
                CAPA = GetCAPA(id.Value);

                if (CAPA == null)
                {
                    ErrorMessage = "CAPA record not found";
                    return;
                }

                ExistingPhotos = GetCAPAPhotos(id.Value);
            }

            else
            {
                CAPA = new CAPAInfo
                {
                    Incident_Date = DateTime.Now
                };
            }
        }
        private CAPAInfo GetCAPA(int id)
        {
            CAPAInfo capa = null;


            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();


            string sql = @"
                SELECT
                    id,
                    capa_no,
                    incident_date,
                    incident_type,
                    severity,
                    machine_name,
                    department,
                    title,
                    description,
                    immediate_action,
                    status,
                    assigned_to,
                    target_date,
                    reported_by
                FROM capa_master
                WHERE id=@id";


            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", id);


            using var reader = cmd.ExecuteReader();


            if (reader.Read())
            {
                capa = new CAPAInfo
                {
                    Id = Convert.ToInt32(reader["id"]),

                    CAPA_No = reader["capa_no"].ToString(),

                    Incident_Date =
                        Convert.ToDateTime(reader["incident_date"]),

                    Incident_Type =
                        reader["incident_type"].ToString(),

                    Severity =
                        reader["severity"].ToString(),

                    Machine_Name =
                        reader["machine_name"].ToString(),

                    Department =
                        reader["department"].ToString(),

                    Title =
                        reader["title"].ToString(),

                    Description =
                        reader["description"].ToString(),

                    Immediate_Action =
                        reader["immediate_action"].ToString(),

                    Status =
                        reader["status"].ToString(),

                    Assigned_To =
                        reader["assigned_to"].ToString(),

                    Target_Date =
                        reader["target_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["target_date"]),

                    Reported_By =
                        reader["reported_by"].ToString()
                };
            }


            return capa;
        }
        private List<CAPAPhoto> GetCAPAPhotos(int capaId)
        {
            var photos = new List<CAPAPhoto>();

            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                SELECT
                    id,
                    capa_id,
                    file_name,
                    content_type
                FROM capa_photos
                WHERE capa_id=@capa_id
                ORDER BY id";


            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@capa_id", capaId);


            using var reader = cmd.ExecuteReader();


            while (reader.Read())
            {
                photos.Add(new CAPAPhoto
                {
                    Id = Convert.ToInt32(reader["id"]),
                    CAPA_Id = Convert.ToInt32(reader["capa_id"]),
                    File_Name = reader["file_name"].ToString(),
                    Content_Type = reader["content_type"].ToString()
                });
            }


            return photos;
        }

        public IActionResult OnGetPhoto(int id)
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();


            string sql = @"
                SELECT
                    photo_data,
                    content_type
                FROM capa_photos
                WHERE id=@id";


            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", id);


            using var reader = cmd.ExecuteReader();


            if (reader.Read())
            {
                byte[] bytes = (byte[])reader["photo_data"];

                string contentType = reader["content_type"].ToString();

                return File(bytes, contentType);
            }


            return NotFound();
        }
        private async Task LoadUsers()
        {
            UserList = await _userManager.Users
                .OrderBy(u => u.UserName)
                .Select(u => u.UserName)
                .ToListAsync();
        }
        private void LoadMachines()
        {
            try
            {
                string connectionString =
                    _configuration.GetConnectionString("NetSuiteOdbc");

                using (var connection = new OdbcConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT DISTINCT
                            mach.name AS MachineName
                        FROM CUSTOMLIST_TR_MACHINE_LIST mach
                        WHERE mach.isinactive = 'F'
                        ORDER BY mach.name";

                    using (var command = new OdbcCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        MachineList.Clear();

                        while (reader.Read())
                        {
                            MachineList.Add(new CAPAInfo
                            {
                                Machine_Name = reader["MachineName"].ToString()
                            });
                        }
                    }
                }
            }
            catch
            {
            }
        }
        private void LoadIncidentTemplates()
        {
            string connString =
                _configuration.GetConnectionString("DefaultConnection");

            using var con = new NpgsqlConnection(connString);

            con.Open();

            string sql = @"
                SELECT
                    id,
                    incident_title,
                    incident_description
                FROM capa_incident_template
                WHERE is_active = true
                ORDER BY incident_title";

            using var cmd = new NpgsqlCommand(sql, con);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                IncidentTemplates.Add(new IncidentTemplate
                {
                    Id = Convert.ToInt32(reader["id"]),
                    Incident_Title = reader["incident_title"].ToString(),
                    Incident_Description = reader["incident_description"].ToString()
                });
            }
        }
        public async Task<IActionResult> OnPostDeletePhotoAsync(int id)
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            await con.OpenAsync();


            string sql = @"
                DELETE FROM capa_photos
                WHERE id=@id";


            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", id);


            await cmd.ExecuteNonQueryAsync();


            return new JsonResult(new
            {
                success = true
            });
        }
        public JsonResult OnGetIncidentDescription(int id)
        {
            string description = "";

            string connString =
                _configuration.GetConnectionString("DefaultConnection");

            using var con = new NpgsqlConnection(connString);

            con.Open();

            string sql = @"
                SELECT incident_description
                FROM capa_incident_template
                WHERE id=@id";

            using var cmd = new NpgsqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@id", id);

            var result = cmd.ExecuteScalar();

            if (result != null)
                description = result.ToString();

            return new JsonResult(description);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                LoadMachines();     
                LoadIncidentTemplates();
                await LoadUsers();

                if (CAPA.Id > 0)
                {
                    string capa_No = GetCAPANumber(CAPA.Id);


                    UpdateCAPA();

                    await SavePhotosAsync(CAPA.Id);


                    var user = await _userManager.FindByNameAsync(CAPA.Assigned_To);

                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        await SendInvestigationOwnerEmailAsync(
                            capa_No,
                            user.Email
                        );
                    }


                    SuccessMessage = "CAPA updated successfully.";

                    return RedirectToPage("/CAPA_List");
                }

                string capaNo = GenerateCAPANumber();


                string connString =
                    _configuration.GetConnectionString("DefaultConnection");

                using (var con = new NpgsqlConnection(connString))
                {
                    con.Open();

                    string sql = @"
                    INSERT INTO capa_master
                    (
                        capa_no,
                        incident_date,
                        report_date,
                        incident_type,
                        severity,
                        machine_name,
                        department,
                        title,
                        description,
                        immediate_action,
                        status,
                        assigned_to,
                        target_date,
                        reported_by
                    )
                    VALUES
                    (
                        @capa_no,
                        @incident_date,
                        CURRENT_TIMESTAMP,
                        @incident_type,
                        @severity,
                        @machine_name,
                        @department,
                        @title,
                        @description,
                        @immediate_action,
                        'Open',
                        @assigned_to,
                        @target_date,
                        @reported_by
                    )
                    RETURNING id;";

                    using (var cmd = new NpgsqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@capa_no", capaNo);
                        cmd.Parameters.AddWithValue("@incident_date", CAPA.Incident_Date);
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
                        cmd.Parameters.AddWithValue("@reported_by",
                            User.Identity?.Name ?? "System");
                        
                        
                        int capaId = Convert.ToInt32(cmd.ExecuteScalar());
                        
                        foreach (var photo in Photos)
                        {
                            if (photo == null || photo.Length == 0)
                                continue;

                            using var ms = new MemoryStream();

                            await photo.CopyToAsync(ms);

                            byte[] bytes = ms.ToArray();

                            string photoSql = @"
                                INSERT INTO capa_photos
                                (
                                    capa_id,
                                    photo_data,
                                    file_name,
                                    content_type
                                )
                                VALUES
                                (
                                    @capa_id,
                                    @photo_data,
                                    @file_name,
                                    @content_type
                                )";

                            using var photoCmd = new NpgsqlCommand(photoSql, con);

                            photoCmd.Parameters.AddWithValue(
                                "@capa_id",
                                capaId);

                            photoCmd.Parameters.AddWithValue(
                                "@photo_data",
                                bytes);

                            photoCmd.Parameters.AddWithValue(
                                "@file_name",
                                photo.FileName);

                            photoCmd.Parameters.AddWithValue(
                                "@content_type",
                                photo.ContentType);

                            photoCmd.ExecuteNonQuery();
                        }
                        // Get investigation owner email
                        var user = await _userManager.FindByNameAsync(CAPA.Assigned_To);

                        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                        {
                            await SendInvestigationOwnerEmailAsync(
                                capaNo,
                                user.Email
                            );
                        }
                    }
                }

                SuccessMessage = $"Incident successfully reported. CAPA No: {capaNo}";

                return RedirectToPage("/CAPA_List");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        private void UpdateCAPA()
        {
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();


            string sql = @"
                UPDATE capa_master
                SET
                    incident_date=@incident_date,
                    incident_type=@incident_type,
                    severity=@severity,
                    machine_name=@machine_name,
                    department=@department,
                    title=@title,
                    description=@description,
                    immediate_action=@immediate_action,
                    assigned_to=@assigned_to,
                    target_date=@target_date
                WHERE id=@id";


            using var cmd = new NpgsqlCommand(sql, con);


            cmd.Parameters.AddWithValue("@incident_date",
                CAPA.Incident_Date);

            cmd.Parameters.AddWithValue("@incident_type",
                CAPA.Incident_Type ?? "");

            cmd.Parameters.AddWithValue("@severity",
                CAPA.Severity ?? "");

            cmd.Parameters.AddWithValue("@machine_name",
                CAPA.Machine_Name ?? "");

            cmd.Parameters.AddWithValue("@department",
                CAPA.Department ?? "");

            cmd.Parameters.AddWithValue("@title",
                CAPA.Title ?? "");

            cmd.Parameters.AddWithValue("@description",
                CAPA.Description ?? "");

            cmd.Parameters.AddWithValue("@immediate_action",
                CAPA.Immediate_Action ?? "");

            cmd.Parameters.AddWithValue("@assigned_to",
                CAPA.Assigned_To ?? "");

            cmd.Parameters.AddWithValue("@target_date",
                CAPA.Target_Date ?? (object)DBNull.Value);


            cmd.Parameters.AddWithValue("@id",
                CAPA.Id);


            cmd.ExecuteNonQuery();

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


            var result = cmd.ExecuteScalar();


            return result?.ToString() ?? "";
        }

        private string GenerateCAPANumber()
        {
            string connString =
                _configuration.GetConnectionString("DefaultConnection");

            using var con = new NpgsqlConnection(connString);

            con.Open();

            string sql =
                "SELECT nextval('capa_no_seq')";

            using var cmd =
                new NpgsqlCommand(sql, con);

            long nextNo =
                Convert.ToInt64(cmd.ExecuteScalar());

            return $"CAPA-{DateTime.Now.Year}-{nextNo:D6}";
        }
        private async Task SavePhotosAsync(int capaId)
        {
            if (Photos == null || Photos.Count == 0)
                return;


            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            await con.OpenAsync();


            foreach (var photo in Photos)
            {
                if (photo == null || photo.Length == 0)
                    continue;


                using var ms = new MemoryStream();

                await photo.CopyToAsync(ms);

                byte[] bytes = ms.ToArray();


                string sql = @"
                    INSERT INTO capa_photos
                    (
                        capa_id,
                        photo_data,
                        file_name,
                        content_type
                    )
                    VALUES
                    (
                        @capa_id,
                        @photo_data,
                        @file_name,
                        @content_type
                    )";


                using var cmd = new NpgsqlCommand(sql, con);


                cmd.Parameters.AddWithValue(
                    "@capa_id",
                    capaId);


                cmd.Parameters.AddWithValue(
                    "@photo_data",
                    bytes);


                cmd.Parameters.AddWithValue(
                    "@file_name",
                    photo.FileName);


                cmd.Parameters.AddWithValue(
                    "@content_type",
                    photo.ContentType);


                await cmd.ExecuteNonQueryAsync();
            }
        }
        private async Task SendInvestigationOwnerEmailAsync(
            string capaNo,
            string recipientEmail)
        {
            string html = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:20px;'>

                <div style='max-width:700px;
                            margin:auto;
                            background:white;
                            border-radius:10px;
                            overflow:hidden;
                            box-shadow:0 2px 10px rgba(0,0,0,.08);'>

                    <div style='background:#dc3545;
                                color:white;
                                padding:25px;'>

                        <h2 style='margin:0'>
                            🚨 New CAPA Assigned
                        </h2>

                    </div>

                    <div style='padding:25px;'>

                        <p>
                            A new CAPA investigation has been assigned to you.
                        </p>

                        <table style='width:100%;
                                       border-collapse:collapse;'>

                            <tr>
                                <td style='padding:10px;font-weight:bold;width:180px'>
                                    CAPA No
                                </td>
                                <td style='padding:10px'>
                                    {capaNo}
                                </td>
                            </tr>

                            <tr>
                                <td style='padding:10px;font-weight:bold'>
                                    Incident Type
                                </td>
                                <td style='padding:10px'>
                                    {CAPA.Incident_Type}
                                </td>
                            </tr>

                            <tr>
                                <td style='padding:10px;font-weight:bold'>
                                    Severity
                                </td>
                                <td style='padding:10px'>
                                    {CAPA.Severity}
                                </td>
                            </tr>

                            <tr>
                                <td style='padding:10px;font-weight:bold'>
                                    Department
                                </td>
                                <td style='padding:10px'>
                                    {CAPA.Department}
                                </td>
                            </tr>

                            <tr>
                                <td style='padding:10px;font-weight:bold'>
                                    Title
                                </td>
                                <td style='padding:10px'>
                                    {CAPA.Title}
                                </td>
                            </tr>

                        </table>

                        <div style='margin-top:20px;'>

                            <strong>Description</strong>

                            <div style='background:#f8f9fa;
                                        padding:12px;
                                        border-radius:6px;
                                        margin-top:5px;'>

                                {CAPA.Description}

                            </div>

                        </div>

                        <div style='margin-top:25px;text-align:center;'>

                            <a href='https://tpltm.co.uk/CAPA_List'
                               style='background:#0d6efd;
                                      color:white;
                                      text-decoration:none;
                                      padding:12px 20px;
                                      border-radius:6px;'>

                                Open CAPA System

                            </a>

                        </div>

                    </div>

                </div>

            </body>
            </html>";

            await SendEmailGraphAsync(
                $"🚨 New CAPA Assigned - {capaNo}",
                html,
                new[] { recipientEmail });
        }
        private async Task SendEmailGraphAsync(
            string subject,
            string htmlBody,
            string[] recipients)
        {
            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];
            var senderEmail = _configuration["AzureAd:SenderEmail"];

            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

            var graphClient = new GraphServiceClient(credential);

            var message = new Microsoft.Graph.Models.Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = htmlBody
                },
                ToRecipients = recipients
                    .Select(email => new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = email
                        }
                    })
                    .ToList()
            };

            await graphClient.Users[senderEmail]
                .SendMail
                .PostAsync(
                    new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                    {
                        Message = message,
                        SaveToSentItems = true
                    });
        }
        public JsonResult OnGetDepartmentOwner(string department)
        {
            string owner = "";

            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            string sql = @"
                SELECT owner_email
                FROM capa_department_owner
                WHERE department=@department
                AND is_active=true
                LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@department", department);

            var result = cmd.ExecuteScalar();

            if (result != null)
                owner = result.ToString();

            return new JsonResult(owner);
        }
    }

    public class CAPAInfo
    {
        public int Id { get; set; }

        public string CAPA_No { get; set; }

        public DateTime Incident_Date { get; set; }

        public string Incident_Type { get; set; }

        public string Severity { get; set; }

        public string Machine_Name { get; set; }

        public string Department { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Immediate_Action { get; set; }

        public string Root_Cause { get; set; }

        public string Status { get; set; }

        public string Assigned_To { get; set; }

        public DateTime? Target_Date { get; set; }

        public string Reported_By { get; set; }

        public string Approved_By { get; set; }

        public string Effectiveness_Review { get; set; }
       

    }
    public class IncidentTemplate
    {
        public int Id { get; set; }

        public string Incident_Title { get; set; }

        public string Incident_Description { get; set; }
    }
    public class CAPAPhoto
    {
        public int Id { get; set; }

        public int CAPA_Id { get; set; }

        public byte[] Photo_Data { get; set; }

        public string File_Name { get; set; }

        public string Content_Type { get; set; }

        public DateTime Upload_Date { get; set; }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class CAPA_ListModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public List<CAPAInfo> CAPAList { get; set; } = new();

        public CAPA_ListModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            string connString =
                _configuration.GetConnectionString("DefaultConnection");

            using var con = new NpgsqlConnection(connString);

            con.Open();

            string sql = @"
            SELECT
                id,
                capa_no,
                incident_date,
                incident_type,
                severity,
                machine_name,
                title,
                status,
                assigned_to,
                target_date,
                reported_by
            FROM capa_master
            ORDER BY id DESC";

            using var cmd = new NpgsqlCommand(sql, con);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                CAPAList.Add(new CAPAInfo
                {
                    Id = Convert.ToInt32(reader["id"]),
                    CAPA_No = reader["capa_no"].ToString(),
                    Incident_Date = Convert.ToDateTime(reader["incident_date"]),
                    Incident_Type = reader["incident_type"].ToString(),
                    Severity = reader["severity"].ToString(),
                    Machine_Name = reader["machine_name"].ToString(),
                    Title = reader["title"].ToString(),
                    Status = reader["status"].ToString(),
                    Assigned_To = reader["assigned_to"].ToString(),
                    Reported_By = reader["reported_by"].ToString(),

                    Target_Date =
                        reader["target_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["target_date"])
                });
            }
        }
        
        public IActionResult OnPostDelete(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                TempData["Error"] =
                    "You do not have permission to delete CAPA.";

                return RedirectToPage();
            }
            using var con = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            using var transaction = con.BeginTransaction();

            try
            {

                // Delete photos
                string sqlPhotos = @"
                    DELETE FROM capa_photos
                    WHERE capa_id=@id";

                using (var cmd = new NpgsqlCommand(sqlPhotos, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }


                // Delete corrective actions
                string sqlCorrective = @"
                    DELETE FROM capa_corrective_action
                    WHERE capa_id=@id";

                using (var cmd = new NpgsqlCommand(sqlCorrective, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }


                // Delete preventive actions
                string sqlPreventive = @"
                    DELETE FROM capa_preventive_action
                    WHERE capa_id=@id";

                using (var cmd = new NpgsqlCommand(sqlPreventive, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }


                // Delete history
                string sqlHistory = @"
                    DELETE FROM capa_history
                    WHERE capa_id=@id";

                using (var cmd = new NpgsqlCommand(sqlHistory, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }


                // Finally delete CAPA master
                string sqlMaster = @"
                    DELETE FROM capa_master
                    WHERE id=@id";

                using (var cmd = new NpgsqlCommand(sqlMaster, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    int rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                    {
                        throw new Exception("CAPA record not found");
                    }
                }


                transaction.Commit();


                TempData["Success"] = "CAPA deleted successfully.";

            }
            catch (Exception ex)
            {
                transaction.Rollback();

                TempData["Error"] = ex.Message;
            }


            return RedirectToPage();
        }
    }

   
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyModel;
using Npgsql;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Production_Target_ListModel : PageModel
    {
        public List<ProductionTargetInfo> Targets { get; set; } = new();

        private readonly IConfiguration _configuration;

        public Production_Target_ListModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet(long? deleteId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            if (deleteId.HasValue)
            {
                using var deleteCmd = new NpgsqlCommand("DELETE FROM production_target WHERE id = @Id", connection);
                deleteCmd.Parameters.AddWithValue("@Id", deleteId.Value);
                deleteCmd.ExecuteNonQuery();
            }

            using var cmd = new NpgsqlCommand("SELECT * FROM production_target ORDER BY id", connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Targets.Add(new ProductionTargetInfo
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Machine_Line = reader["machine_line"]?.ToString(),
                    Machine_Name = reader["machine_name"]?.ToString(),
                    Product_Line = reader["product_line"]?.ToString(),
                    Target_Count = reader.GetInt64(reader.GetOrdinal("target_count")),
                    Effective_Date = reader.GetDateTime(reader.GetOrdinal("effective_date")),
                    Is_Active = reader.GetBoolean(reader.GetOrdinal("is_active"))
                });
            }
        }
    }
}

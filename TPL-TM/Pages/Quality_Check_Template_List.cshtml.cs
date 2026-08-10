using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Quality_Check_Template_ListModel : PageModel
    {
        private readonly IConfiguration _config;

        public Quality_Check_Template_ListModel(IConfiguration config)
        {
            _config = config;
        }

        public List<TemplateInfo> TemplateList { get; set; } = new();
        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, product_category, test_name, created_on, created_by
                    FROM quality_checks_template
                    ORDER BY created_on DESC;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    TemplateList.Add(new TemplateInfo
                    {
                        Id = reader.GetInt64(0),
                        Product_Category = reader.GetString(1),
                        Test_Name = reader.GetString(2),
                        Created_On = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                        Created_By = reader.IsDBNull(4) ? "" : reader.GetString(4)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error loading templates: " + ex.Message;
            }
        }

        public class TemplateInfo
        {
            public long Id { get; set; }
            public string Product_Category { get; set; }
            public string Test_Name { get; set; }
            public DateTime? Created_On { get; set; }
            public string Created_By { get; set; }
        }
    }
}

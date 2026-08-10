using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_Check_Template_ListModel : PageModel
    {
        private readonly IConfiguration _config;

        public Shift_Check_Template_ListModel(IConfiguration config)
        {
            _config = config;
        }

        public List<ShiftTemplateInfo> TemplateList { get; set; } = new();
        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, check_name, created_on, created_by
                    FROM public.shift_checks_template
                    ORDER BY id ASC;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    TemplateList.Add(new ShiftTemplateInfo
                    {
                        Id = reader.GetInt64(0),
                        Check_Name = reader.GetString(1),
                        Created_On = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                        Created_By = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error loading shift check templates: " + ex.Message;
            }
        }

        public class ShiftTemplateInfo
        {
            public long Id { get; set; }
            public string Check_Name { get; set; }
            public DateTime? Created_On { get; set; }
            public string Created_By { get; set; }
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class Shift_Check_Template_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Shift_Check_Template_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public TemplateItem Template = new TemplateItem();

        public class TemplateItem
        {
            public long Id { get; set; }
            public string Check_Name { get; set; }
            public DateTime Created_On { get; set; }
            public string Created_By { get; set; }
        }

        public void OnGet()
        {
            string id = Request.Query["id"];
            if (!long.TryParse(id, out long templateId))
            {
                errorMessage = "Invalid template ID!";
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, check_name, created_on, created_by
                    FROM public.shift_checks_template
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", templateId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Template.Id = reader.GetInt64(0);
                    Template.Check_Name = reader.GetString(1);
                    Template.Created_On = reader.GetDateTime(2);
                    Template.Created_By = reader.GetString(3);
                }
                else
                {
                    errorMessage = "Shift check template not found.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error loading template: " + ex.Message;
            }
        }

        public IActionResult OnPostDeleteTemplate(long Id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = "DELETE FROM public.shift_checks_template WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    TempData["SuccessMessage"] = "Shift check template deleted successfully!";
                else
                    TempData["ErrorMessage"] = "Template not found.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting template: " + ex.Message;
            }

            return RedirectToPage("/Shift_Check_Template_List");
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin")]
    public class Quality_Check_Template_DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Quality_Check_Template_DeleteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string errorMessage = "";
        public TemplateItem Template = new TemplateItem();

        public class TemplateItem
        {
            public long Id { get; set; }
            public string Product_Category { get; set; }
            public string Test_Name { get; set; }
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
                    SELECT id, product_category, test_name, created_on, created_by
                    FROM quality_checks_template
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", templateId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Template.Id = reader.GetInt64(0);
                    Template.Product_Category = reader.GetString(1);
                    Template.Test_Name = reader.GetString(2);
                    Template.Created_On = reader.GetDateTime(3);
                    Template.Created_By = reader.GetString(4);
                }
                else
                {
                    errorMessage = "Template not found.";
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

                string sql = "DELETE FROM quality_checks_template WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", Id);

                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    TempData["SuccessMessage"] = "Template deleted successfully!";
                else
                    TempData["ErrorMessage"] = "Template not found.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting template: " + ex.Message;
            }

            return RedirectToPage("/Quality_Check_Template_List");
        }
    }
}
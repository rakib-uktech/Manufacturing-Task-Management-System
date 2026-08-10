using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Quality_Check_Template_EditModel : PageModel
    {
        private readonly IConfiguration _config;

        public Quality_Check_Template_EditModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public TemplateInfo Template { get; set; } = new();

        public List<string> ProductCategoryList { get; set; } = new();

        public string ErrorMessage { get; set; } = "";
        public string SuccessMessage { get; set; } = "";

        public void OnGet(long id)
        {
            try
            {
                // Load Product Categories from NetSuite
                LoadProductCategories();

                // Load template from Postgres
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"SELECT id, product_category, test_name FROM quality_checks_template WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Template.Id = reader.GetInt64(0);
                    Template.Product_Category = reader.GetString(1);
                    Template.Test_Name = reader.GetString(2);
                }
                else
                {
                    ErrorMessage = "Template not found.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error: " + ex.Message;
            }
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors and try again.";
                // Reload product categories in case of error
                LoadProductCategories();
                return Page();
            }

            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    UPDATE quality_checks_template 
                    SET product_category = @pc, test_name = @tn 
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@pc", Template.Product_Category);
                cmd.Parameters.AddWithValue("@tn", Template.Test_Name);
                cmd.Parameters.AddWithValue("@id", Template.Id);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Error updating template: " + ex.Message;
                // Reload product categories in case of error
                LoadProductCategories();
                return Page();
            }

            // Redirect back to list
            return RedirectToPage("/Quality_Check_Template_List");
        }

        private void LoadProductCategories()
        {
            try
            {
                string connStr = _config.GetConnectionString("NetSuiteOdbc");
                using var connection = new OdbcConnection(connStr);
                connection.Open();

                string sqlCategories = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";

                using var cmd = new OdbcCommand(sqlCategories, connection);
                using var reader = cmd.ExecuteReader();
                ProductCategoryList.Clear();
                while (reader.Read())
                {
                    ProductCategoryList.Add(reader["name"].ToString());
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading product categories from NetSuite: {ex.Message}";
            }
        }

        public class TemplateInfo
        {
            public long Id { get; set; }
            public string Product_Category { get; set; }
            public string Test_Name { get; set; }
        }
    }
}

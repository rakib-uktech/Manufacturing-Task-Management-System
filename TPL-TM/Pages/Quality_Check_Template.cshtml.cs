using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Data.Odbc;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Quality_Check_TemplateModel : PageModel
    {
        [BindProperty]
        public string ProductCategory { get; set; }

        [BindProperty]
        public string TestName { get; set; }

        public List<string> ProductCategoryList { get; set; } = new List<string>();

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        private readonly IConfiguration _configuration;
        public string ConnectionString { get; }

        public Quality_Check_TemplateModel(IConfiguration configuration)
        {
            _configuration = configuration;
            ConnectionString = _configuration.GetConnectionString("NetSuiteOdbc");
        }

        public void OnGet()
        {
            try
            {
                
                using var connection = new OdbcConnection(ConnectionString);
                connection.Open();

                // Load Product Categories from Machine Lines table
                string sqlCategories = "SELECT DISTINCT name FROM CUSTOMLISTPRODUCT_SPEC_PRODUCTTYPELIST ORDER BY name";

                using var cmd = new OdbcCommand(sqlCategories, connection);
                using var reader = cmd.ExecuteReader();
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

        public IActionResult OnPost()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ProductCategory) ||
                    string.IsNullOrWhiteSpace(TestName))
                {
                    ErrorMessage = "Product category and test name are required.";
                    return Page();
                }

                using var con = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                con.Open();

                string sql = @"
                    INSERT INTO quality_checks_template
                        (product_category, test_name, created_on, created_by)
                    VALUES
                        (@cat, @name, NOW(), @createdBy)
                ";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@cat", ProductCategory);
                cmd.Parameters.AddWithValue("@name", TestName);
                cmd.Parameters.AddWithValue("@createdBy", User.Identity?.Name ?? "system");

                cmd.ExecuteNonQuery();

                SuccessMessage = "Template added successfully!";
                return RedirectToPage("/Quality_Check_Template_List");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                return Page();
            }
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_Check_TemplateModel : PageModel
    {
        [BindProperty]
        public string CheckName { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        private readonly IConfiguration _config;
        private readonly string _conn;

        public Shift_Check_TemplateModel(IConfiguration config)
        {
            _config = config;
            _conn = _config.GetConnectionString("DefaultConnection");
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CheckName))
                {
                    ErrorMessage = "Check Name is required.";
                    return Page();
                }

                using var con = new NpgsqlConnection(_conn);
                con.Open();

                string sql = @"
                    INSERT INTO public.shift_checks_template
                        (check_name, created_on, created_by)
                    VALUES
                        (@name, NOW(), @createdBy)
                ";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@name", CheckName);
                cmd.Parameters.AddWithValue("@createdBy", User.Identity?.Name ?? "system");

                cmd.ExecuteNonQuery();

                SuccessMessage = "Shift check template added successfully!";
                return RedirectToPage("/Shift_Check_Template_List");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                return Page();
            }
        }
    }
}

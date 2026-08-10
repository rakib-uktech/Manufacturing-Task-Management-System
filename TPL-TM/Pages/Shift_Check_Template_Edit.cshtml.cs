using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,User,Management")]
    public class Shift_Check_Template_EditModel : PageModel
    {
        private readonly IConfiguration _config;

        public Shift_Check_Template_EditModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public TemplateInfo Template { get; set; } = new();

        public string ErrorMessage { get; set; } = "";
        public string SuccessMessage { get; set; } = "";

        public void OnGet(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"SELECT id, check_name FROM public.shift_checks_template WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Template.Id = reader.GetInt64(0);
                    Template.Check_Name = reader.GetString(1);
                }
                else
                {
                    ErrorMessage = "Shift check template not found.";
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
                return Page();
            }

            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    UPDATE public.shift_checks_template 
                    SET check_name = @name 
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@name", Template.Check_Name);
                cmd.Parameters.AddWithValue("@id", Template.Id);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Error updating template: " + ex.Message;
                return Page();
            }

            // Redirect back to list
            return RedirectToPage("/Shift_Check_Template_List");
        }

        public class TemplateInfo
        {
            public long Id { get; set; }
            public string Check_Name { get; set; }
        }
    }
}

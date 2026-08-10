using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace TPL_TM.Pages
{
    [Authorize(Roles = "Admin,Supervisor,Management")]
    public class Shift_Check_EditModel : PageModel
    {
        private readonly IConfiguration _config;

        public Shift_Check_EditModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public ShiftCheckInfo ShiftCheck { get; set; } = new();

        public string ErrorMessage { get; set; } = "";

        public void OnGet(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                string sql = @"
                    SELECT id, check_name, check_status, comment, created_by 
                    FROM public.shift_checks 
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    ShiftCheck.Id = reader.GetInt64(0);
                    ShiftCheck.Check_Name = reader["check_name"]?.ToString();
                    ShiftCheck.Check_Status = reader.GetBoolean(reader.GetOrdinal("check_status"));
                    ShiftCheck.Check_Comment = reader["comment"]?.ToString();
                    ShiftCheck.Check_Created_By = reader["created_by"]?.ToString();
                }
                else
                {
                    ErrorMessage = "Shift check not found.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Error loading shift check: " + ex.Message;
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
                    UPDATE public.shift_checks
                    SET check_name = @name,
                        check_status = @status,
                        comment = @comment,
                        created_by = @createdBy
                    WHERE id = @id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", ShiftCheck.Id);
                cmd.Parameters.AddWithValue("@name", ShiftCheck.Check_Name ?? "");
                cmd.Parameters.AddWithValue("@status", ShiftCheck.Check_Status);
                cmd.Parameters.AddWithValue("@comment", ShiftCheck.Check_Comment ?? "");
                cmd.Parameters.AddWithValue("@createdBy", ShiftCheck.Check_Created_By ?? "");

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Update error: " + ex.Message;
                return Page();
            }

            return RedirectToPage("/Shift_Check_Report");
        }

        public class ShiftCheckInfo
        {
            public long Id { get; set; }
            public string Check_Name { get; set; }
            public bool Check_Status { get; set; }
            public string Check_Comment { get; set; }
            public string Check_Created_By { get; set; }
        }
    }
}

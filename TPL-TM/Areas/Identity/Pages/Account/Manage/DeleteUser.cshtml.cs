using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace TPLPLM.Areas.Identity.Pages.Account.Manage
{
    [Authorize(Roles = "Admin")]
    public class DeleteUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<DeleteUserModel> _logger;

        public DeleteUserModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<DeleteUserModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string UserName { get; set; }
        public string UserEmail { get; set; }

        public class InputModel
        {
            [Required]
            public string UserId { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Admin Password")]
            public string AdminPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("User ID is required.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("User not found.");

            UserName = user.UserName;
            UserEmail = user.Email;

            Input = new InputModel { UserId = id };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Verify admin password
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
                return Unauthorized();

            var passwordValid = await _userManager.CheckPasswordAsync(admin, Input.AdminPassword);
            if (!passwordValid)
            {
                ModelState.AddModelError(string.Empty, "Incorrect admin password.");
                return Page();
            }

            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                return Page();
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            _logger.LogInformation("User '{UserName}' ({UserId}) was deleted by admin '{AdminName}' ({AdminId}).",
                user.UserName, user.Id, admin.UserName, admin.Id);

            StatusMessage = $"User {user.UserName} has been deleted successfully.";
            return RedirectToPage("./UserList");
        }
    }
}

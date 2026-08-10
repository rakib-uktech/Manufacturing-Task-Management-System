#nullable disable
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace TPL_TM.Pages
{
    public class ShiftSupervisorLoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ShiftSupervisorLoginModel> _logger;

        public ShiftSupervisorLoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<ShiftSupervisorLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Supervisor Email")]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            public string ShiftId { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/Shift_Entry");
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/Shift_Entry");

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                return Page();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials.");
                return Page();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Admin") && !roles.Contains("Supervisor"))
            {
                ModelState.AddModelError(string.Empty, "Insufficient privileges — only Supervisors or Admins can authorize shifts.");
                return Page();
            }

            // ✅ Authorized supervisor — grant temporary permission
            TempData["SupervisorAuthorized"] = true;
            TempData["AuthorizedBy"] = Input.Email ?? User.Identity?.Name;
            TempData.Keep();

            if (!string.IsNullOrEmpty(Input.ShiftId))
                TempData["ShiftId"] = Input.ShiftId;

            _logger.LogInformation("Supervisor {Email} authorized a shift.", Input.Email);

            return LocalRedirect(returnUrl);
        }
    }
}

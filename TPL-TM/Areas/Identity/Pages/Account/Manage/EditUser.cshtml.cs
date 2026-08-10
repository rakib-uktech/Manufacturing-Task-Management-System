using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TPL_TM.Data;

namespace TPLPLM.Areas.Identity.Pages.Account.Manage
{
    [Authorize(Roles = "Admin")]
    public class EditUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public EditUserModel(UserManager<IdentityUser> userManager,
                             RoleManager<IdentityRole> roleManager,
                             ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [TempData]
        public string StatusMessage { get; set; }  // <-- Add this

        [BindProperty]
        public InputModel Input { get; set; }

        public List<SelectListItem> AvailableRoles { get; set; } = new();
        public List<SelectListItem> ShiftList { get; set; } = new();


        public class InputModel
        {
            [Required]
            public string Id { get; set; }

            [Display(Name = "User Name")]
            [Required]
            public string UserName { get; set; }

            [Display(Name = "Email")]
            [EmailAddress]
            [Required]
            public string Email { get; set; }

            [Display(Name = "Role")]
            [Required]
            public string Role { get; set; }

            [Display(Name = "Shift")]
            public int? ShiftId { get; set; }
        }


        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("User ID is required.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("User not found.");

            var userRoles = await _userManager.GetRolesAsync(user);
            var userShift = await _context.UserShiftAssignment
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            Input = new InputModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = userRoles.FirstOrDefault() ?? "User",
                ShiftId = userShift?.ShiftInformationId
            };

            // Load roles for dropdown
            AvailableRoles = _roleManager.Roles
                .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                .ToList();

            // Load shifts for dropdown
            ShiftList = await _context.ShiftInformation
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Name} ({s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm})"
                }).ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByIdAsync(Input.Id);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                return Page();
            }

            // Update username/email
            user.UserName = Input.UserName;
            user.Email = Input.Email;
            await _userManager.UpdateAsync(user);

            // Update role
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(Input.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, Input.Role);
            }

            // Update shift
            // Update shift assignment
            var existingShift = await _context.UserShiftAssignment
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            if (existingShift != null)
            {
                // Remove old assignment first
                _context.UserShiftAssignment.Remove(existingShift);
                await _context.SaveChangesAsync();
            }

            if (Input.ShiftId.HasValue)
            {
                // Add new assignment
                _context.UserShiftAssignment.Add(new UserShiftAssignment
                {
                    UserId = user.Id,
                    ShiftInformationId = Input.ShiftId.Value
                });
            }

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "User profile updated successfully.";
            return RedirectToPage("./UserList");
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TPL_TM.Data;

namespace TPLPLM.Areas.Identity.Pages.Account.Manage
{
    [Authorize(Roles = "Admin")]

    public class UserListModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserListModel(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<UserDisplayModel> Users { get; set; } = new();

        public class UserDisplayModel
        {
            public string Id { get; set; }
            public string UserName { get; set; }
            public string Email { get; set; }
            public string Role { get; set; }
            public string ShiftName { get; set; }
            public string ShiftTime { get; set; }
        }

        public async Task OnGetAsync()
        {
            var allUsers = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var userShiftAssignments = await _context.UserShiftAssignment
                .Include(s => s.ShiftInformation)
                .ToListAsync();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var shiftAssignment = userShiftAssignments.FirstOrDefault(x => x.UserId == user.Id);

                Users.Add(new UserDisplayModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "N/A",
                    ShiftName = shiftAssignment?.ShiftInformation?.Name ?? "N/A",
                    ShiftTime = shiftAssignment != null
                        ? $"{shiftAssignment.ShiftInformation.StartTime:hh\\:mm} - {shiftAssignment.ShiftInformation.EndTime:hh\\:mm}"
                        : "-"
                });
            }
        }
    }
}

using Microsoft.AspNetCore.Identity;

namespace TPL_TM.Data
{
    public class UserShiftAssignment
    {
        public string UserId { get; set; } = null!;
        public int ShiftInformationId { get; set; }

        public IdentityUser User { get; set; } = null!;
        public ShiftInformation ShiftInformation { get; set; } = null!;
    }
}

using Microsoft.AspNetCore.Identity;

namespace TPL_TM.Data
{
    public class ShiftInformation
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public ICollection<UserShiftAssignment> UserShiftAssignments { get; set; } = new List<UserShiftAssignment>();
    }

}

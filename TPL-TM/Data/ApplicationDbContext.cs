using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TPL_TM.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ShiftInformation> ShiftInformation { get; set; }
        public DbSet<UserShiftAssignment> UserShiftAssignment { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserShiftAssignment>(b =>
            {
                b.ToTable("UserShiftAssignment");
                b.HasKey(x => new { x.UserId, x.ShiftInformationId });

                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.ShiftInformation)
                    .WithMany(x => x.UserShiftAssignments)
                    .HasForeignKey(x => x.ShiftInformationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ShiftInformation>(b =>
            {
                b.ToTable("ShiftInformation");
                b.HasKey(x => x.Id);
            });
        }
    }
}

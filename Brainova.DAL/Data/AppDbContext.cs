using Brainova.DAL.Modles;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Brainova.DAL.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.SupervisorUser)
                .WithMany(s => s.Students)
                .HasForeignKey(u => u.SupervisorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}

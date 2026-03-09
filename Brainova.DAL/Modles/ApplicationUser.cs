using Microsoft.AspNetCore.Identity;


namespace Brainova.DAL.Modles
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = null!;
        public string? SupervisorUserId { get; set; }
        public ApplicationUser? SupervisorUser { get; set; }

        // ✅ Reverse navigation: Supervisor -> Students
        public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();

        public bool IsBlocked { get; set; } = false;
        public string? CodeResetPassword { get; set; }
        public DateTime? CodeResetPasswordExpire { get; set; }
    }
}

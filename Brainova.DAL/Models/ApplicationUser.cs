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

        // Wrong reset codes entered since the last successful reset or lock. Deliberately NOT
        // cleared when a new code is requested, so asking for fresh codes buys no extra guesses.
        public int ResetPasswordFailedAttempts { get; set; }

        // While this is in the future, no reset code is sent or accepted for this user.
        public DateTime? ResetPasswordLockedUntil { get; set; }
    }
}

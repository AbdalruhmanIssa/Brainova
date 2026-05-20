namespace Brainova.BLL.DTOs.Auth
{
    public class CurrentUserResponse
    {
        public string Id { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();

        // Only populated for users that have a supervisor (typically Students). Null otherwise.
        public string? SupervisorUserId { get; set; }
        public string? SupervisorFullName { get; set; }
    }
}

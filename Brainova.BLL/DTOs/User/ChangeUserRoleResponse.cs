namespace Brainova.BLL.DTOs.Auth
{
    public class ChangeUserRoleResponse
    {
        public string UserId { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? OldRole { get; set; }
        public string NewRole { get; set; } = null!;
        public bool IsChanged { get; set; }
    }
}
namespace Brainova.BLL.DTOs.User
{
    public class UpdateUserResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string RoleName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string? SupervisorId { get; set; }
        public bool IsChanged { get; set; }

    }
}
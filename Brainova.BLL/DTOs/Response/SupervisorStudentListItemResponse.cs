namespace Brainova.BLL.DTOs.Response
{
    public class SupervisorStudentListItemResponse
    {
        public string StudentId { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public int ReportsCount { get; set; }
    }
}
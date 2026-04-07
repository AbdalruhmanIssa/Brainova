namespace Brainova.BLL.DTOs.Response.Feedback
{
    public class AdminFeedbackResponse
    {
        public Guid FeedbackId { get; set; }
        public Guid ReportId { get; set; }
        public Guid CaseId { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }

        public string SupervisorId { get; set; } = default!;
        public string? SupervisorName { get; set; }

        public string Comment { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public bool IsSeen { get; set; }
    }
}

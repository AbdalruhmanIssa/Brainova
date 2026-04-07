using Brainova.DAL.Enums;

namespace Brainova.BLL.DTOs.Response
{
    public class AdminCaseDetailsResponse
    {
        public Guid CaseId { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }

        public string? SupervisorId { get; set; }
        public string? SupervisorName { get; set; }

        public CaseStatus Status { get; set; }

        public bool IsReportSubmitted { get; set; }
        public bool IsPredicted { get; set; }
        public bool IsReviewed { get; set; }

        public Guid? ReportId { get; set; }
        public DateTime? ReportSubmittedAt { get; set; }

        public Guid? FeedbackId { get; set; }
        public DateTime? FeedbackSubmittedAt { get; set; }

        public string? PredictionResult { get; set; }
        public DateTime? PredictionCreatedAt { get; set; }

        public DateTime CaseCreatedAt { get; set; }

        public string? ImageUrl { get; set; } = default!;
        public string? GradcamUrl { get; set; }
    }
}
using Brainova.DAL.Enums;

namespace Brainova.BLL.DTOs.Response
{
    public class StudentCaseDetailsResponse
    {
        public Guid CaseId { get; set; }

        public CaseStatus Status { get; set; }

        public bool IsReportSubmitted { get; set; }
        public bool IsPredicted { get; set; }
        public bool IsReviewed { get; set; }

        public Guid? ReportId { get; set; }
        public string? PredictionResult { get; set; }

        public DateTime CaseCreatedAt { get; set; }
        public DateTime? ReportSubmittedAt { get; set; }
        public DateTime? PredictionCreatedAt { get; set; }

        public string? ImageUrl { get; set; }
        public string? GradcamUrl { get; set; }
    }
}
using Brainova.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class SupervisorStudentCaseDetailsResponse
    {
        public Guid CaseId { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }

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

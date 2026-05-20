using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response.Report
{
    public class SupervisorReportDetailsResponse
    {
        public Guid ReportId { get; set; }
        public string? ReportCode { get; set; } = default!;
        public Guid CaseId { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; } = default!;

        public DateTime SubmittedAt { get; set; }

        // MRI
        public string MriImageUrl { get; set; } = default!;

        // AI (no gradcam)
        public string? PredictionResult { get; set; }
        public List<ProbabilityItemResponse> Probabilities { get; set; }


        public List<SupervisorReportAnswerResponse> Answers { get; set; } = new();
    }
}

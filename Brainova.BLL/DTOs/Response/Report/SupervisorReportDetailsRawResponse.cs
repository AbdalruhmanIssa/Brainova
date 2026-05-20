using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response.Report
{
    public class SupervisorReportDetailsRawResponse
    {
        
        public Guid ReportId { get; set; }
        public string? ReportCode { get; set; } = default!;
        public Guid CaseId { get; set; }
        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; } = default!;
        public DateTime SubmittedAt { get; set; }

        public string StoredFileName { get; set; } = default!;

        public string? PredictionResult { get; set; }
        public List<ProbabilityItemResponse> Probabilities { get; set; }


        public List<SupervisorReportAnswerResponse> Answers { get; set; } = new();
    }
}

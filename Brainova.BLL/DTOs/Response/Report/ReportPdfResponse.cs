namespace Brainova.BLL.DTOs.Response.Report
{
    public class ReportPdfResponse
    {
        public Guid ReportId { get; set; }
        public string? ReportCode { get; set; } = default!;
        public Guid CaseId { get; set; }

        public string StudentId { get; set; } = default!;
        public string StudentName { get; set; } = default!;
        public string SupervisorName { get; set; } = default!;

        public DateTime SubmittedAt { get; set; }
        public DateTime? CaseCreatedAt { get; set; }
        public DateTime? PredictionCreatedAt { get; set; }

        public string StoredFileName { get; set; } = default!;
        public string? PredictionResult { get; set; }

        public List<ProbabilityItemResponse> Probabilities { get; set; } = new();
        public List<ReportPdfAnswerResponse> Answers { get; set; } = new();
    }
}
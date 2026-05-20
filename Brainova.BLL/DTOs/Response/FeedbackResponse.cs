using System;

namespace Brainova.BLL.DTOs.Response
{
    public class FeedbackResponse
    {
        public Guid Id { get; set; }
        public Guid ReportId { get; set; }
        public string? ReportCode { get; set; } = default!;
        public DateTime ReportCreatedAt { get; set; }
        public string? PredictionResult { get; set; }

        public string SupervisorId { get; set; } = default!;
        public string? SupervisorName { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }

        public string Comment { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public bool IsSeen { get; set; }
    }
}
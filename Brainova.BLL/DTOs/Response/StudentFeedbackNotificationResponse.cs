using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class StudentFeedbackNotificationResponse
    {
        public Guid FeedbackId { get; set; }
        public Guid ReportId { get; set; }
        public Guid CaseId { get; set; }

        public string? SupervisorName { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsSeen { get; set; }

        public string? Preview { get; set; } // short text (optional)
    }
}

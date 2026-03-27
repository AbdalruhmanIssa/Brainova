using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class SupervisorNewReportResponse    
    {
        public Guid ReportId { get; set; }
        public Guid CaseId { get; set; }
        public DateTime SubmittedAt { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }
    }
}
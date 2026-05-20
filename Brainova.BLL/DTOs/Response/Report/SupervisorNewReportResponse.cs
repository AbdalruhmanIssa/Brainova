using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response.Report
{
    public class SupervisorNewReportResponse
    {
        public Guid ReportId { get; set; }
        public string? ReportCode { get; set; } = default!;
        public Guid CaseId { get; set; }
        public DateTime SubmittedAt { get; set; }

        public string StudentId { get; set; } = default!;
        public string? StudentName { get; set; }
    }
}

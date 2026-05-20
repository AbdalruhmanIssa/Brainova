using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class SupervisorDashboardSummaryResponse
    {
        public int TotalStudents { get; set; }
        public int TotalReports { get; set; }
        public int NewReports { get; set; }
        public int FeedbackGiven { get; set; }
    }
}

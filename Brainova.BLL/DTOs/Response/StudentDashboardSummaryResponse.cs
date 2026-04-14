namespace Brainova.BLL.DTOs.Response
{
    public class StudentDashboardSummaryResponse
    {
        public int TotalCases { get; set; }
        public int ReportsSubmitted { get; set; }
     //   public int PredictionsReady { get; set; }
        public int FeedbackReceived { get; set; }
    }
}
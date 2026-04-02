using Brainova.DAL.Enums;

namespace Brainova.BLL.DTOs.Response.Report
{
    public class ReportPdfAnswerResponse
    {
        public Guid QuestionId { get; set; }

        public string Code { get; set; } = default!;
        public string Question { get; set; } = default!;

        public ReportQuestionType Type { get; set; }
        public string AnswerValue { get; set; } = default!;
    }
}
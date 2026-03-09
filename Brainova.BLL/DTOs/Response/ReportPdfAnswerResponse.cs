using Brainova.DAL.Enums;

namespace Brainova.BLL.DTOs.Response
{
    public class ReportPdfAnswerResponse
    {
        public Guid QuestionId { get; set; }

        public string Code { get; set; } = default!;
        public string Question { get; set; } = default!;

        public ReportQuestionType Type { get; set; }

        public string? AnswerText { get; set; }
        public decimal? AnswerNumber { get; set; }
        public bool? AnswerBool { get; set; }
        public string? AnswerJson { get; set; }
    }
}
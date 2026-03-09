using Brainova.DAL.Enums;
using System;

namespace Brainova.DAL.Modles
{
    public class ReportAnswer : BaseEntity
    {
        public Guid ReportId { get; set; }
        public Report Report { get; set; } = default!;

        public Guid QuestionId { get; set; }
        public ReportQuestion Question { get; set; } = default!;

        public string? AnswerText { get; set; }
        public decimal? AnswerNumber { get; set; }
        public bool? AnswerBool { get; set; }
        public string? AnswerJson { get; set; }

        // snapshot (keeps report stable even if admin edits questions later)
        public string QuestionTextSnapshot { get; set; } = default!;
        public ReportQuestionType QuestionTypeSnapshot { get; set; }
    }
}
using Brainova.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class SupervisorReportAnswerResponse
    {
        public Guid QuestionId { get; set; }
        public string Question { get; set; } = default!;
        public string Code { get; set; } = default!;
        public ReportQuestionType Type { get; set; }

        public string AnswerValue { get; set; } = default!;
    }
}

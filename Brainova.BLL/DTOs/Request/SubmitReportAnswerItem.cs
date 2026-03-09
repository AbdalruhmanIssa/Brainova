using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Brainova.BLL.DTOs.Request
{
    public class SubmitReportAnswerItem
    {
        [Required]
        public Guid QuestionId { get; set; }

        public string? AnswerText { get; set; }
        public decimal? AnswerNumber { get; set; }
        public bool? AnswerBool { get; set; }
        public string? AnswerJson { get; set; }
    }
}

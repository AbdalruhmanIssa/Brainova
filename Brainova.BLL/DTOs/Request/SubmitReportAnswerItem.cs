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
        public string AnswerValue { get; set; } = default!;
    }
}
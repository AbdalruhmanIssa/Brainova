using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Request
{
    public class SubmitReportRequest
    {
        [Required]
        public Guid CaseId { get; set; }

        [Required]
        public List<SubmitReportAnswerItem> Answers { get; set; } = new();
    }


}
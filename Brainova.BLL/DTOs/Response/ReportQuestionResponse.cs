using Brainova.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Text;


namespace Brainova.BLL.DTOs.Response
{

    public class ReportQuestionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = default!;
        public ReportQuestionType Type { get; set; }
        public int Order { get; set; }
        public string? OptionsJson { get; set; }
    }
}

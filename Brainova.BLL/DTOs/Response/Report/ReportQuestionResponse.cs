using Brainova.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response.Report
{
    public class ReportQuestionResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string Text { get; set; } = default!;
        public ReportQuestionType Type { get; set; }
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public List<string>? Options { get; set; }
    }
}
using Brainova.DAL.Enums;

namespace Brainova.BLL.DTOs.Response.Report
{
    public class AdminReportQuestionResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Text { get; set; } = null!;
        public ReportQuestionType Type { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
        public bool IsRequired { get; set; }
        public List<string>? Options { get; set; }
    }
}
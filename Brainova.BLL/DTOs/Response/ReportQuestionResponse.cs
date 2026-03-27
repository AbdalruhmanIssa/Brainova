using Brainova.DAL.Enums;

namespace Brainova.BLL.DTOs.Response
{
    public class ReportQuestionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = default!;
        public ReportQuestionType Type { get; set; }
        public int Order { get; set; }
        public List<string>? Options { get; set; }
    }
}
using Brainova.DAL.Enums;

namespace Brainova.DAL.Modles
{
    public class ReportQuestion : BaseEntity
    {
        public string Code { get; set; } = default!;
        public string Text { get; set; } = default!;
        public ReportQuestionType Type { get; set; }

        public int Order { get; set; }
        public bool IsActive { get; set; } = true;

        // For choices: JSON array string e.g. ["Glioma","Meningioma"]
        public string? OptionsJson { get; set; }
    }
}
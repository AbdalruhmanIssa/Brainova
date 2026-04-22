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
        public bool IsRequired { get; set; }=true;

        // If true, this question's answer is ignored/skipped when
        // the preliminary assessment answer is "no tumor".
        public bool SkipWhenNoTumor { get; set; } = false;

        // For choices: JSON array string e.g. ["Glioma","Meningioma"]
        public string? OptionsJson { get; set; }
        public string SupervisorId { get; set; } = default!;
        public ApplicationUser Supervisor { get; set; } = default!;
    }
}
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

        // If true, this question is system-managed and cannot be
        // updated, deleted, or deactivated by supervisors.
        public bool IsSystem { get; set; } = false;

        // For choices: JSON array string e.g. ["Glioma","Meningioma"]
        public string? OptionsJson { get; set; }

        // Nullable: a question can be "orphaned" when its supervisor is deleted.
        // Orphaned questions remain so historical reports/answers stay intact.
        public string? SupervisorId { get; set; }
        public ApplicationUser? Supervisor { get; set; }
    }
}
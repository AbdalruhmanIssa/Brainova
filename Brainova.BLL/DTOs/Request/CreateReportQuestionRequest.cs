using Brainova.DAL.Enums;
using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Request
{
    public class CreateReportQuestionRequest
    {
        [Required, MaxLength(100)]
        public string Code { get; set; } = null!;

        [Required, MaxLength(2000)]
        public string Text { get; set; } = null!;

        [Required]
        public ReportQuestionType Type { get; set; }

        public int Order { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsRequired { get; set; }

        public bool SkipWhenNoTumor { get; set; } = false;

        public List<string>? Options { get; set; }
    }
}
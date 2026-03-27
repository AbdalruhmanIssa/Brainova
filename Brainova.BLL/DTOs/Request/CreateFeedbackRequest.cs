using System;
using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Request
{
    public class CreateFeedbackRequest
    {
        [Required]
        public Guid ReportId { get; set; }

        [Required, MinLength(3), MaxLength(3000)]
        public string Comment { get; set; } = default!;
    }
}
using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Request
{
    public class UpdateFeedbackRequest
    {
        [Required, MinLength(3), MaxLength(3000)]
        public string Comment { get; set; } = default!;
    }
}
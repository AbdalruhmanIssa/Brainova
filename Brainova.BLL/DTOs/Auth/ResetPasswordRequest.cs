using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Auth
{
    public class ResetPasswordRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required, MinLength(4), MaxLength(10)]
        public string Code { get; set; } = null!;

        [Required, MinLength(8), MaxLength(100)]
        public string NewPassword { get; set; } = null!;
    }
}

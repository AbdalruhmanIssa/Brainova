using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Auth
{
    public class ChangeUserPasswordRequest
    {
        [Required, MinLength(8), MaxLength(100)]
        public string NewPassword { get; set; } = null!;
    }
}
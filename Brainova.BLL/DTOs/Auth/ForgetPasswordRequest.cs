using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Auth
{
    public class ForgetPasswordRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
    }
}

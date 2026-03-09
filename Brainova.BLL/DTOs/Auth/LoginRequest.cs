using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]
        public string EmailOrUserName { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }
}

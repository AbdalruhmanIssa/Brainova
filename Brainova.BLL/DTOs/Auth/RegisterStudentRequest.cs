using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Auth
{
    public class RegisterStudentRequest
    {
        [Required, MinLength(3), MaxLength(80)]
        public string FullName { get; set; } = null!;

        [Required, EmailAddress, MaxLength(120)]
        public string Email { get; set; } = null!;

        [Required, MinLength(3), MaxLength(30)]
        public string UserName { get; set; } = null!;

        [Required, Phone, MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = null!;

        [Required] // must pick supervisor
        public string SupervisorUserId { get; set; } = null!;
    }
}

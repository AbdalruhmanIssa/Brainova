using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Brainova.BLL.DTOs.Auth
{
    public class CreateUserRequest
    {
        [Required, MinLength(3), MaxLength(80)]
        public string FullName { get; set; } = null!;
        [Required, EmailAddress, MaxLength(120)]
        public string Email { get; set; } = null!;
        [Required, MinLength(3), MaxLength(30)]
        public string UserName { get; set; } = null!;
        [Required, Phone, MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;
    }
}

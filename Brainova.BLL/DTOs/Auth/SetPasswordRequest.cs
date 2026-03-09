using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Brainova.BLL.DTOs.Auth
{
    public class SetPasswordRequest
    {
        public string UserId { get; set; } = null!;
        public string Token { get; set; } = null!;   // reset token
        [Required, MinLength(8), MaxLength(100)]
        public string NewPassword { get; set; } = null!;
    }
}

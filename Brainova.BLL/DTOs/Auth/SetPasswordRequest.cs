using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Auth
{
    public class SetPasswordRequest
    {
        public string UserId { get; set; } = null!;
        public string Token { get; set; } = null!;   // reset token
        public string NewPassword { get; set; } = null!;
    }
}

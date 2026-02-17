using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Auth
{
    public class CreateUserRequest
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.User
{
    public class UserDTO
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public bool EmailConfirmed { get; set; }
        public string RoleName { get; set; } = "";
        public bool IsBlocked { get; set; }
        public string? SupervisorId { get; set; }
        public string? SupervisorName { get; set; }
    }

}

using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.User
{
    public class UpdateStudentForEditResponse : UpdateUserResponse
    {
        public string SupervisorUserId { get; set; } = string.Empty;
    }
}

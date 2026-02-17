using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Brainova.BLL.DTOs.Auth
{
    public class AssignSupervisorRequest
    {
        [Required] public string StudentUserId { get; set; } = null!;
        [Required] public string SupervisorUserId { get; set; } = null!;
    }
}

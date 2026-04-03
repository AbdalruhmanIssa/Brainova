using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.Auth
{
    public class ChangeUserRoleRequest
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public string RoleName { get; set; } = null!;
     
    }
}
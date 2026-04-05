using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.DTOs.User
{
    public class DeleteUsersRequest
    {
        [Required]
        public List<string> UserIds { get; set; } = new();
    }
}
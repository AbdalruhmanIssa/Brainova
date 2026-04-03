using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.User;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.SuperAdmin
{

    [Area("Identity")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize(Roles = "SuperAdmin")] 
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }
        [HttpPost("create-admin")]

        public async Task<IActionResult> CreateAdmin(CreateUserRequest request)
        {
            var message = await _userService.CreateAdminAsync(request, Request);
            return Ok(new { message });
        }

        [HttpPatch("change-password/{userId}")]
       
        public async Task<IActionResult> ResetPassword(string userId, ChangeUserPasswordRequest request)
        {
            var result = await _userService.ResetUserPasswordAsync(userId, request);

            return Ok(new { message = result });
        }
        [HttpPut("change-role")]
        public async Task<IActionResult> ChangeUserRole([FromBody] ChangeUserRoleRequest request)
        {
            var result = await _userService.ChangeUserRoleAsync(request);

            return Ok(new
            {
                message = "User role updated successfully",
                data = result
            });
        }
    }
}

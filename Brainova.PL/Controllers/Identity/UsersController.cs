using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Areas.Identity.Controllers
{
    [Area("Identity")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")] // default for the whole controller
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ✅ Admin only (SuperAdmin excluded if you want)
        [HttpGet("all")]
       
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        // ✅ Admin + SuperAdmin
        [HttpPatch("block/{userId}")]
        public async Task<IActionResult> Block([FromRoute] string userId)
        {
            var ok = await _userService.BlockUserAsync(userId);
            return ok ? Ok(true) : NotFound(false);
        }


        // ✅ Admin + SuperAdmin
        [HttpPatch("unblock/{userId}")]
        public async Task<IActionResult> Unblock([FromRoute] string userId)
        {
            var ok = await _userService.UnBlockUserAsync(userId);
            return ok ? Ok(true) : NotFound(false);
        }
        [HttpGet("isblocked/{userId}")]
        public async Task<IActionResult> IsBlocked([FromRoute] string userId)
        {
            var blocked = await _userService.IsBlockedAsync(userId);
            return Ok(blocked);
        }


        // ✅ Admin + SuperAdmin create supervisor
        [HttpPost("create-supervisor")]
        public async Task<IActionResult> CreateSupervisor(CreateUserRequest request)
        {
            var message = await _userService.CreateSupervisorAsync(request, Request);
            return Ok(new { message });
        }

        // 👑 SuperAdmin only create admin
        [HttpPost("create-admin")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> CreateAdmin(CreateUserRequest request)
        {
            var message = await _userService.CreateAdminAsync(request, Request);
            return Ok(new { message });
        }
        [HttpGet("supervisors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSupervisors()
        {
            var list = await _userService.GetSupervisorsAsync();
            return Ok(list);
        }
        [HttpPost("assign-supervisor")]
       
        public async Task<IActionResult> AssignSupervisor(AssignSupervisorRequest request)
        {
            var msg = await _userService.AssignSupervisorAsync(request);
            return Ok(new { message = msg });
        }

        [HttpDelete("{userId}")]
       
        public async Task<IActionResult> DeleteUser([FromRoute] string userId)
        {
            var msg = await _userService.DeleteUserAsync(userId);
            return Ok(new { message = msg });
        }
    }

}

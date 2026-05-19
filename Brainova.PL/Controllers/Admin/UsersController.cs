using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.User;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Admin
{
    [Area("Identity")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize] // default for the whole controller
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ✅ Admin only (SuperAdmin excluded if you want)
        [HttpGet("all")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users

        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        // ✅ Admin + SuperAdmin
        [HttpPatch("block/{userId}")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users

        public async Task<IActionResult> Block(string userId)
        {
            await _userService.BlockUserAsync(userId);
            return Ok(new { message = "User blocked successfully" });
        }



        // ✅ Admin + SuperAdmin
        [HttpPatch("unblock/{userId}")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users

        public async Task<IActionResult> Unblock([FromRoute] string userId)
        {
            var ok = await _userService.UnBlockUserAsync(userId);
            return ok ? Ok(true) : NotFound(false);
        }
        [HttpGet("isblocked/{userId}")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users

        public async Task<IActionResult> IsBlocked([FromRoute] string userId)
        {
            var blocked = await _userService.IsBlockedAsync(userId);
            return Ok(blocked);
        }


        // ✅ Admin + SuperAdmin create supervisor
        [HttpPost("create-supervisor")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users

        public async Task<IActionResult> CreateSupervisor(CreateUserRequest request)
        {
            var message = await _userService.CreateSupervisorAsync(request, Request);
            return Ok(new { message });
        }

        // 👑 SuperAdmin only create admin
        

        [HttpPost("create-student")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users

        public async Task<IActionResult> CreateStudent(CreateUserRequest request)
        {
            var message = await _userService.CreateStudentAsync(request, Request);
            return Ok(new { message });
        }
        [HttpGet("supervisors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSupervisors()
        {
            var list = await _userService.GetSupervisorsAsync();
            return Ok(list);
        }


   
        [HttpPut("update/{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, UpdateUserRequest request)
        {
            var result = await _userService.UpdateUserAsync(userId, request);
            return Ok(result);
        }


        [HttpGet("{userId}")]
        
        public async Task<IActionResult> GetById([FromRoute] string userId)
        {
            var user = await _userService.GetByIdAsync(userId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }
        [HttpDelete("bulk-delete")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin and SuperAdmin can update users
        public async Task<IActionResult> DeleteUsers([FromBody] DeleteUsersRequest request)
        {
            var result = await _userService.DeleteUsersAsync(request);
            return Ok(result);
        }
    }

}

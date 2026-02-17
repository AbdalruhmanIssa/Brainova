
using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Areas.Identity.Controllers
{
    [Area("Identity")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AuthsController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserService _userService;
        public AuthsController(IAuthenticationService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        // 🔐 Login (same for all roles)
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }

        // 👨‍🎓 Student Register
        [HttpPost("register-student")]
        public async Task<IActionResult> RegisterStudent(RegisterStudentRequest request)
        {
            var message = await _authService.RegisterStudentAsync(request, Request);
            return Ok(new { message });
        }

        // 📩 Confirm Email
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string token, string userId)
        {
            var message = await _authService.ConfirmEmailAsync(token, userId);
            return Ok(new { message });
        }

        // 🔑 Forgot Password
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgetPasswordRequest request)
        {
            var success = await _authService.ForgotPasswordAsync(request);
            return Ok(new { success });
        }

        // 🔄 Reset Password (code flow)
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var success = await _authService.ResetPasswordAsync(request);
            return Ok(new { success });
        }

        // 🔐 Set Password (Option A – created by Admin/SuperAdmin)
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword(SetPasswordRequest request)
        {
            var message = await _authService.SetPasswordAsync(request);
            return Ok(new { message });
        }
        [HttpGet("my-students")]
        [Authorize(Roles = "Supervisor")]
        public async Task<IActionResult> MyStudents()
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId)) return Unauthorized();

            var students = await _userService.GetMyStudentsAsync(supervisorId);
            return Ok(students);
        }
    }
}

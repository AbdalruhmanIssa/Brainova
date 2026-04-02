using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Area("Supervisor")]
    [Authorize(Roles = "Supervisor")]
    public class StudentsController : ControllerBase
    {
        private readonly IUserService _userService;

        public StudentsController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyStudents()
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var students = await _userService.GetSupervisorStudentsAsync(supervisorId);
            return Ok(students);
        }
    }
}
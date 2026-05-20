using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Area("Supervisor")]
    [Authorize(Roles = "Supervisor")]
    public class MriCasesController : ControllerBase
    {
        private readonly IMriCaseService _service;

        public MriCasesController(IMriCaseService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyStudentsCases([FromQuery] string? studentId,CancellationToken ct)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var cases = await _service.GetSupervisorCasesAsync(supervisorId, studentId, ct);

            foreach (var item in cases)
            {
                item.ImageUrl = $"{Request.Scheme}://{Request.Host}{item.ImageUrl}";

                if (!string.IsNullOrWhiteSpace(item.GradcamUrl))
                    item.GradcamUrl = $"{Request.Scheme}://{Request.Host}{item.GradcamUrl}";
            }

            var response = new
            {
                TotalCount = cases.Count,
                Items = cases
            };

            return Ok(response);
        }
    }
}
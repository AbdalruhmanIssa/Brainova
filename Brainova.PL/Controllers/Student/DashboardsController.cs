using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Student
{
    [ApiController]
    [Area("Student")]
    [Route("api/[area]")]
    [Authorize(Roles = "Student")]
    public class DashboardsController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardsController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }
        [HttpGet("DashboardSummary")]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var result = await _dashboardService.GetStudentDashboardSummaryAsync(studentId, ct);
            return Ok(result);
        }
    }
}

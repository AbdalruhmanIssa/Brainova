using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Area("Supervisor")]
    [Route("api/[area]")]
    [Authorize(Roles = "Supervisor")]
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
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var result = await _dashboardService.GetSupervisorDashboardSummaryAsync(supervisorId, ct);
            return Ok(result);
        }

    }
}

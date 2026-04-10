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
        private readonly ISupervisorService _supervisorService;

        public DashboardsController(ISupervisorService supervisorService)
        {
            _supervisorService = supervisorService;
        }

        [HttpGet("DashboardSummary")]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var result = await _supervisorService.GetDashboardSummaryAsync(supervisorId, ct);
            return Ok(result);
        }

    }
}

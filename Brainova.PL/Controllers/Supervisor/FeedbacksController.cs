using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Area("Supervisor")]
    [Route("api/[area]/Reports/{reportId:guid}/[controller]")]
    [Authorize(Roles = "Supervisor")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Add(Guid reportId, [FromBody] CreateFeedbackRequest request)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var message = await _service.AddAsync(supervisorId, reportId, request);
            return Ok(new { message });
        }

        [HttpGet]
        public async Task<IActionResult> GetByReportId(Guid reportId)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var data = await _service.GetForSupervisorAsync(supervisorId, reportId);
            return Ok(data);
        }
    }
}
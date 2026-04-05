using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Area("Supervisor")]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Supervisor")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        // POST: api/Supervisor/Feedbacks/report/{reportId}
        [HttpPost("report/{reportId:guid}")]
        public async Task<IActionResult> Add(Guid reportId, [FromBody] CreateFeedbackRequest request)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var result = await _service.AddAsync(supervisorId, reportId, request);
            return Ok(result);
        }

        // GET: api/Supervisor/Feedbacks/report/{reportId}
        [HttpGet("report/{reportId:guid}")]
        public async Task<IActionResult> GetByReportId(Guid reportId)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var result = await _service.GetForSupervisorAsync(supervisorId, reportId);
            return Ok(result);
        }

        // GET: api/Supervisor/Feedbacks
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var (totalCount, items) = await _service.GetAllForSupervisorAsync(supervisorId, ct);

            return Ok(new
            {
                totalCount,
                items
            });
        }

        // PUT: api/Supervisor/Feedbacks/{feedbackId}
        [HttpPut("{feedbackId:guid}")]
        public async Task<IActionResult> Update(Guid feedbackId, [FromBody] UpdateFeedbackRequest request, CancellationToken ct)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var result = await _service.UpdateAsync(supervisorId, feedbackId, request, ct);
            return Ok(result);
        }

        // DELETE: api/Supervisor/Feedbacks/{feedbackId}
        [HttpDelete("{feedbackId:guid}")]
        public async Task<IActionResult> Delete(Guid feedbackId, CancellationToken ct)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var message = await _service.DeleteAsync(supervisorId, feedbackId, ct);
            return Ok(new { message });
        }
    }
}
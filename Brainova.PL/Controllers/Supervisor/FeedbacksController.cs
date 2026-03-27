using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Area("Supervisor")]
    [Authorize(Roles = "Supervisor")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateFeedbackRequest request)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var message = await _service.AddAsync(supervisorId, request);
            return Ok(new { message });
        }

        [HttpGet("{reportId:guid}")]
        public async Task<IActionResult> GetByReportId(Guid reportId)
        {
            var data = await _service.GetByReportIdAsync(reportId);
            return Ok(data);
        }
        [HttpPut("{feedbackId:guid}")]
        public async Task<IActionResult> Update(Guid feedbackId, [FromBody] UpdateFeedbackRequest request)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var message = await _service.UpdateAsync(supervisorId, feedbackId, request);
            return Ok(new { message });
        }

        [HttpDelete("{feedbackId:guid}")]
        public async Task<IActionResult> Delete(Guid feedbackId)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var message = await _service.DeleteAsync(supervisorId, feedbackId);
            return Ok(new { message });
        }
    }
}
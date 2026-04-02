using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Admin
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _service.GetAllAsync();
            return Ok(data);
        }

        [HttpPut("{feedbackId:guid}")]
        public async Task<IActionResult> Update(Guid feedbackId, [FromBody] UpdateFeedbackRequest request)
        {
            var message = await _service.UpdateAsync(null, feedbackId, request);
            return Ok(new { message });
        }

        [HttpDelete("{feedbackId:guid}")]
        public async Task<IActionResult> Delete(Guid feedbackId)
        {
            var message = await _service.DeleteAsync(null, feedbackId);
            return Ok(new { message });
        }
        [HttpGet("by-supervisor/{supervisorId}")]
        public async Task<IActionResult> GetBySupervisor(string supervisorId)
        {
            var data = await _service.GetBySupervisorAsync(supervisorId);
            return Ok(data);
        }
        [HttpGet("by-supervisor/{supervisorId}")]
        public async Task<IActionResult> GetBySupervisorAsync(string supervisorId)
        {
            var data = await _service.GetBySupervisorAsync(supervisorId);
            return Ok(data);
        }
    }
}
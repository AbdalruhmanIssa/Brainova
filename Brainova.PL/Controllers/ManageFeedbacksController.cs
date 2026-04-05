using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Area("Supervisor")]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Supervisor")]
    public class ManageFeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public ManageFeedbacksController(IFeedbackService service)
        {
            _service = service;
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
            try
            {
                var supervisorId = User.FindFirst("Id")?.Value;
                if (string.IsNullOrWhiteSpace(supervisorId))
                    return Unauthorized();

                var message = await _service.DeleteAsync(supervisorId, feedbackId);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }
    }
    }
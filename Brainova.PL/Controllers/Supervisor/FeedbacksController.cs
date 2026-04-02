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
            try
            {
                var supervisorId = User.FindFirst("Id")?.Value;
                if (string.IsNullOrWhiteSpace(supervisorId))
                    return Unauthorized();

                var message = await _service.AddAsync(supervisorId, reportId, request);
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

        [HttpGet]
        public async Task<IActionResult> GetByReportId(Guid reportId)
        {
            var data = await _service.GetByReportIdAsync(reportId);
            return Ok(data);
        }
    
    }
}
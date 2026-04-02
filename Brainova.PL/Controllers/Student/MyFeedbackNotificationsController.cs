using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Student
{
    [ApiController]
    [Area("Student")]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Student")]
    public class MyFeedbackNotificationsController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public MyFeedbackNotificationsController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpGet("unseen")]
        public async Task<IActionResult> GetUnseen()
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var data = await _service.GetUnseenForStudentAsync(studentId);
            return Ok(data);
        }

        [HttpPut("{feedbackId:guid}/mark-seen")]
        public async Task<IActionResult> MarkAsSeen(Guid feedbackId)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var message = await _service.MarkAsSeenAsync(studentId, feedbackId);
            return Ok(new { message });
        }
    }
}
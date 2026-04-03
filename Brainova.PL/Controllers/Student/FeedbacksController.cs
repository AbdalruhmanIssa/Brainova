using Brainova.BLL.DTOs.Response.Feedback;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Student
{
    [ApiController]
    [Area("Student")]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Student")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        // GET: /api/Student/Feedbacks/report/{reportId}
        [HttpGet("report/{reportId:guid}")]
        public async Task<IActionResult> GetByReportId(Guid reportId)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var data = await _service.GetForStudentAsync(studentId, reportId);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] StudentFeedbacksQuery query, CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var data = await _service.GetAllForStudentAsync(studentId, query, ct);
            return Ok(data);
        }

        [HttpGet("unseen")]
        public async Task<IActionResult> GetUnseen([FromQuery] StudentFeedbacksQuery query, CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var data = await _service.GetUnseenForStudentAsync(studentId, query, ct);
            return Ok(data);
        }

        // POST: /api/Student/Feedbacks/{feedbackId}/mark-seen
        [HttpPost("{feedbackId:guid}/mark-seen")]
        public async Task<IActionResult> MarkSeen(Guid feedbackId, CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            await _service.MarkSeenForStudentAsync(studentId, feedbackId, ct);
            return Ok(new { message = "Feedback marked as seen" });
        }

        // POST: /api/Student/Feedbacks/mark-all-seen
        [HttpPost("mark-all-seen")]
        public async Task<IActionResult> MarkAllSeen(CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            await _service.MarkAllSeenForStudentAsync(studentId, ct);
            return Ok(new { message = "All feedback marked as seen" });
        }
    }
}
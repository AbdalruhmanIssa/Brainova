using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Student
{
    [ApiController]
    [Area("Student")]
    [Route("api/[area]/Reports/{reportId:guid}/[controller]")]
    [Authorize(Roles = "Student")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetByReportId(Guid reportId)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var data = await _service.GetForStudentAsync(studentId, reportId);
            return Ok(data);
        }
    }
}
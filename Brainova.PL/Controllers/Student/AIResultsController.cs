using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Student
{
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class AIResultsController : ControllerBase
    {
        private readonly IAiResultService _aiResultService;

        public AIResultsController(IAiResultService aiResultService)
        {
            _aiResultService = aiResultService;
        }
        [HttpPost("{caseId:guid}/predict")]
        public async Task<IActionResult> Predict(Guid caseId, CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(studentId))
                return Unauthorized();

            var result = await _aiResultService.PredictAsync(studentId, caseId, ct);

            // build full url
            result.GradcamUrl =
                $"{Request.Scheme}://{Request.Host}{result.GradcamUrl}";

            return Ok(result);
        }
    }
}

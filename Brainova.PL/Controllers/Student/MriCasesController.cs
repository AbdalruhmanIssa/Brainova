using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Brainova.PL.Controllers.Student
{
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class MriCasesController : ControllerBase
    {
       private readonly IMriCaseService _service;
        public MriCasesController(IMriCaseService service)
        {
            _service = service;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] MriUploadRequest request, CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value; // ✅ read the same claim you generate

            if (string.IsNullOrEmpty(studentId))
                return Unauthorized();

            var response = await _service.CreateAsync(studentId, request, ct);

            // The image itself is served by CaseImagesController, which checks
            // that the caller owns the case (or supervises the student who does).
            response.ImageUrl = $"{Request.Scheme}://{Request.Host}/api/CaseImages/mri/{response.StoredFileName}";

            return Ok(response);
        }

        [HttpGet("my-cases")]
        public async Task<IActionResult> GetMyCases(CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var cases = await _service.GetMyCasesAsync(studentId, ct);

            foreach (var item in cases)
            {
                item.ImageUrl = $"{Request.Scheme}://{Request.Host}{item.ImageUrl}";

                if (!string.IsNullOrWhiteSpace(item.GradcamUrl))
                    item.GradcamUrl = $"{Request.Scheme}://{Request.Host}{item.GradcamUrl}";
            }

            return Ok(new
            {
                TotalCount = cases.Count,
                Items = cases
            });
        }
    }
}

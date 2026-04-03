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

            response.ImageUrl = Url.Action(nameof(GetImage), new { fileName = response.StoredFileName })!;
            response.ImageUrl = $"{Request.Scheme}://{Request.Host}{response.ImageUrl}";

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet("image/{fileName}")]
        public IActionResult GetImage(string fileName)
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "App_Data",
                "mri",
                fileName
            );

            if (!System.IO.File.Exists(path))
                return NotFound();

            return PhysicalFile(path, "image/jpeg");
        }
        [HttpGet("my-cases")]
        public async Task<IActionResult> GetMyCases([FromQuery] StudentCasesQuery query, CancellationToken ct)
        {
            var studentId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized();

            var response = await _service.GetMyCasesAsync(studentId, query, ct);

            foreach (var item in response.Items)
            {
                item.ImageUrl = $"{Request.Scheme}://{Request.Host}{item.ImageUrl}";

                if (!string.IsNullOrWhiteSpace(item.GradcamUrl))
                    item.GradcamUrl = $"{Request.Scheme}://{Request.Host}{item.GradcamUrl}";
            }

            return Ok(response);
        }
    }
}

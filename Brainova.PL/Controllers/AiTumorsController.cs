using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ML.OnnxRuntime;

namespace Brainova.PL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiTumorsController : ControllerBase
    {
        private readonly IAiTumorService _aiService;

        public AiTumorsController(IAiTumorService aiService)
        {
            _aiService = aiService;
        }

       

        [HttpPost("gradcam")]
        public async Task<IActionResult> Gradcam([FromForm] IFormFile file, CancellationToken ct)
        {
            await using var stream = file.OpenReadStream();
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

            var dto = await _aiService.GetGradcamAsync(stream, file.FileName, contentType, ct);

            // build URL here (HttpContext is guaranteed)
            dto.GradcamUrl = Url.Action(
                nameof(GradcamImage),
                values: new { fileName = dto.FileName }
            )!;

            // make it absolute (https://host/..)
            dto.GradcamUrl = Url.Action(nameof(GradcamImage), new { fileName = dto.FileName })!;
            dto.GradcamUrl = $"{Request.Scheme}://{Request.Host}{dto.GradcamUrl}";

            return Ok(dto);
        }
        [HttpGet("gradcam-image/{fileName}")]
        public IActionResult GradcamImage(string fileName, [FromServices] IWebHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "App_Data", "gradcam", fileName);

            if (!System.IO.File.Exists(path))
                return NotFound();

            return PhysicalFile(path, "image/jpeg");
        }
        [HttpGet("python-health")]
        public async Task<IActionResult> PythonHealth([FromServices] IHttpClientFactory f, CancellationToken ct)
        {
            var client = f.CreateClient("GradCamClient");
            var res = await client.GetAsync("/health", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return Ok(new { status = (int)res.StatusCode, body });
        }
        [HttpGet("ping")]
        public IActionResult Ping() => Ok("PING_OK");
    }
}
//[HttpPost("predict")]
//public async Task<IActionResult> Predict(IFormFile file)
//{
//    if (file is null || file.Length == 0)
//        return BadRequest("No file uploaded.");

//    await using var stream = file.OpenReadStream();
//    var result = await _aiService.PredictAsync(stream);

//    return Ok(new
//    {
//        label = result.label,
//        probabilities = result.probabilities
//    });
//}
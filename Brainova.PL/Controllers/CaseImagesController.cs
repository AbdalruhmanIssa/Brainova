using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers
{
    /// <summary>
    /// Serves the images that belong to real MRI cases: the uploaded scan and its
    /// Grad-CAM heatmap. Only two people may download them: the student who owns
    /// the case, and that student's assigned supervisor.
    ///
    /// Anyone else gets 404 rather than 403, so the endpoint does not reveal which
    /// file names exist.
    ///
    /// The frontend shows these with a plain &lt;img src&gt;. That still works with
    /// [Authorize] because the browser sends the access_token cookie
    /// (SameSite=None; Partitioned) with image requests too, and Program.cs reads
    /// the JWT from that cookie when no Authorization header is present.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Student,Supervisor")]
    public class CaseImagesController : ControllerBase
    {
        private const string MriFolder = "mri";
        private const string GradcamFolder = "gradcam";

        private readonly IMriCaseService _mriCases;
        private readonly IFileService _files;

        public CaseImagesController(IMriCaseService mriCases, IFileService files)
        {
            _mriCases = mriCases;
            _files = files;
        }

        // GET api/CaseImages/mri/{fileName}
        [HttpGet("mri/{fileName}")]
        public async Task<IActionResult> GetMriImage(string fileName, CancellationToken ct)
        {
            var userId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (!IsBareFileName(fileName))
                return NotFound();

            if (!await _mriCases.CanViewMriImageAsync(userId, fileName, ct))
                return NotFound();

            return ServeImage(MriFolder, fileName);
        }

        // GET api/CaseImages/gradcam/{fileName}
        [HttpGet("gradcam/{fileName}")]
        public async Task<IActionResult> GetGradcamImage(string fileName, CancellationToken ct)
        {
            var userId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (!IsBareFileName(fileName))
                return NotFound();

            if (!await _mriCases.CanViewGradcamImageAsync(userId, fileName, ct))
                return NotFound();

            return ServeImage(GradcamFolder, fileName);
        }

        private IActionResult ServeImage(string folder, string fileName)
        {
            var path = _files.BuildPath(folder, fileName);
            if (!System.IO.File.Exists(path))
                return NotFound();

            // Personal medical data: browsers may cache it, shared proxies/CDNs may not.
            Response.Headers.CacheControl = "private, max-age=3600";
            return PhysicalFile(path, "image/jpeg");
        }

        // Only a bare file name, never a path ("..\x.jpg", "a/b.jpg").
        private static bool IsBareFileName(string fileName)
            => !string.IsNullOrWhiteSpace(fileName) && Path.GetFileName(fileName) == fileName;
    }
}

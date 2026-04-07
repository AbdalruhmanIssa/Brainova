using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Admin
{
    [ApiController]
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class CasesController : ControllerBase
    {
        private readonly IMriCaseService _service;

        public CasesController(IMriCaseService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetCases(CancellationToken ct)
        {
            var data = await _service.GetAdminCasesAsync(ct);

            foreach (var item in data)
            {
                item.ImageUrl = $"{Request.Scheme}://{Request.Host}{item.ImageUrl}";

                if (!string.IsNullOrWhiteSpace(item.GradcamUrl))
                    item.GradcamUrl = $"{Request.Scheme}://{Request.Host}{item.GradcamUrl}";
            }

            return Ok(data);
        }
    }
}

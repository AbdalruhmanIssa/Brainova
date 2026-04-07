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
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;
        public FeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetFeedbacks(CancellationToken ct)
        {
            var (totalCount, items) = await _service.GetAdminFeedbacksAsync(ct);

            return Ok(new
            {
                totalCount,
                items
            });
        }
    }
}

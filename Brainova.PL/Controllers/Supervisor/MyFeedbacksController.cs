using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Area("Supervisor")]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Supervisor")]
    public class MyFeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;

        public MyFeedbacksController(IFeedbackService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var supervisorId = User.FindFirst("Id")?.Value;

            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var data = await _service.GetBySupervisorAsync(supervisorId);

            return Ok(data);
        }
    }
}
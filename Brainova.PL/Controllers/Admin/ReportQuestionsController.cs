
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Admin
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Admin")]
    [Area("Admin")]

    public class ReportQuestionsController : ControllerBase
    {
        private readonly IReportQuestionService _svc;

        public ReportQuestionsController(IReportQuestionService svc)
        {
            _svc = svc;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReportQuestionRequest req)
        {
            await _svc.AddAsync(req);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _svc.GetAllAsync();
            return Ok(data);
        }

        [HttpPatch("{id:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            await _svc.ToggleActiveAsync(id);

            return Ok(new { message = "Question status toggled" });
        }
    }
}
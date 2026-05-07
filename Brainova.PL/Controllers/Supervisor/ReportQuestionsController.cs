using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.DTOs.Response.Report;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Supervisor")]
    [Area("Supervisor")]
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
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            await _svc.AddAsync(supervisorId, req);
            return Ok(new { message = "Question created successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var questions = await _svc.GetAllForSupervisorAsync(supervisorId);

            var dto = questions.Select(q => new AdminReportQuestionResponse
            {
                Id = q.Id,
                Code = q.Code,
                Text = q.Text,
                Type = q.Type,
                Order = q.Order,
                IsActive = q.IsActive,
                IsRequired = q.IsRequired,
                SkipWhenNoTumor = q.SkipWhenNoTumor,
                IsSystem = q.IsSystem,
                Options = string.IsNullOrWhiteSpace(q.OptionsJson)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson)
            }).ToList();

            return Ok(dto);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReportQuestionRequest req)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            await _svc.UpdateAsync(supervisorId, id, req);
            return Ok(new { message = "Question updated successfully" });
        }

        [HttpPatch("{id:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            await _svc.ToggleActiveAsync(supervisorId, id);
            return Ok(new { message = "Question status toggled" });
        }
    }
}
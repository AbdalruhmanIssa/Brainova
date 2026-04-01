
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
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
            var questions = await _svc.GetAllAsync();

            var dto = questions.Select(q => new AdminReportQuestionResponse
            {
                Id = q.Id,
                Code = q.Code,
                Text = q.Text,
                Type = q.Type,
                Order = q.Order,
                IsActive = q.IsActive,
                IsRequired = q.IsRequired,
                Options = string.IsNullOrWhiteSpace(q.OptionsJson)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson)
            }).ToList();

            return Ok(dto);
        }
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReportQuestionRequest req)
        {
            await _svc.UpdateAsync(id, req);
            return Ok(new { message = "Question updated successfully" });
        }

        [HttpPatch("{id:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            await _svc.ToggleActiveAsync(id);

            return Ok(new { message = "Question status toggled" });
        }
    }
}
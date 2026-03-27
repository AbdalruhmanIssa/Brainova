using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Brainova.PL.Controllers.Student
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Student")]
    [Area("Student")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportQuestionService _qSvc;
        private readonly IReportService _rSvc;
        private readonly IReportPdfService _reportPdfService;

        public ReportsController(IReportQuestionService qSvc, IReportService rSvc, IReportPdfService reportPdfService)
        {
            _qSvc = qSvc;
            _rSvc = rSvc;
            _reportPdfService = reportPdfService;
        }

        // GET: api/Student/Reports/questions
        [HttpGet("questions")]
        public async Task<IActionResult> GetQuestions()
        {
            var questions = await _qSvc.GetActiveAsync();
            var dto = questions.Adapt<List<ReportQuestionResponse>>();
            return Ok(dto);
        }

        // POST: api/Student/Reports/submit
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] SubmitReportRequest req)
        {
            var studentId = User.FindFirst("Id")?.Value!;
            var reportId = await _rSvc.SubmitAsync(studentId, req);
            return Ok(new { reportId });
        }
        [HttpGet("{reportId:guid}/pdf")]
        public async Task<IActionResult> DownloadReportPdf(Guid reportId, CancellationToken ct)
        {
            var supervisorId = User.FindFirstValue("Id");
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var pdfBytes = await _reportPdfService.GenerateSupervisorReportPdfAsync(supervisorId, reportId, ct);

            return File(
                pdfBytes,
                "application/pdf",
                $"Brainova_Report_{reportId}.pdf"
            );
        }
    }

}
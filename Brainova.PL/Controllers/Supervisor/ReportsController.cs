using Brainova.BLL.DTOs.Response;
using Brainova.BLL.DTOs.Response.Report;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Brainova.PL.Controllers.Supervisor
{
    [ApiController]
    [Route("api/[area]/[controller]")]
    [Authorize(Roles = "Supervisor")]
    [Area("Supervisor")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _svc;
        private readonly IReportPdfService _reportPdfService;
        public ReportsController(IReportService svc,IReportPdfService reportPdfService)
        {
            _svc = svc;
            _reportPdfService = reportPdfService;
        }

        // GET: api/Supervisor/Reports/new
        [HttpGet("new")]
        public async Task<IActionResult> GetNew()
        {
            var supervisorId = User.FindFirst("Id")?.Value;
            if (string.IsNullOrWhiteSpace(supervisorId))
                return Unauthorized();

            var reports = await _svc.GetNewForSupervisorAsync(supervisorId);

            return Ok(new
            {
                TotalCount = reports.Count,
                Items = reports
            });
        }

        // GET api/Supervisor/Reports/{reportId}/details
        [HttpGet("{reportId:guid}/details")]
        public async Task<IActionResult> GetDetails(Guid reportId)
        {
            var supervisorId = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(supervisorId))
                return Unauthorized();

            var raw = await _svc.GetSupervisorDetailsAsync(supervisorId, reportId);

            var relative = Url.Action(
                action: "GetImage",
                controller: "MriCases",
                values: new
                {
                    area = "Student",
                    fileName = raw.StoredFileName
                });

            var imageUrl = $"{Request.Scheme}://{Request.Host}{relative}";

            var dto = new SupervisorReportDetailsResponse
            {
                ReportId = raw.ReportId,
                CaseId = raw.CaseId,
                StudentId = raw.StudentId,
                StudentName = raw.StudentName,
                SubmittedAt = raw.SubmittedAt,
                PredictionResult = raw.PredictionResult,
                MriImageUrl = imageUrl,
                Answers = raw.Answers
            };

            return Ok(dto);
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
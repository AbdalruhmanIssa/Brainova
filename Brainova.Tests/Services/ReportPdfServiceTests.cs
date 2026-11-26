using Brainova.BLL.DTOs.Response.Report;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// ReportPdfService asks IReportService for the report data, then renders
    /// a PDF with QuestPDF. IReportService is mocked; QuestPDF runs for real,
    /// so these tests prove a PDF is actually produced.
    ///
    /// A PDF is binary, so the tests do not read its text. They check the
    /// file signature instead: every PDF starts with the bytes "%PDF".
    /// </summary>
    public class ReportPdfServiceTests : IDisposable
    {
        // A valid 1x1 pixel PNG, used as the "MRI image" on disk.
        private const string OnePixelPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        private readonly Mock<IReportService> _reports = new();
        private readonly TempContentRoot _root = new();
        private readonly ReportPdfService _sut;

        public ReportPdfServiceTests()
        {
            _sut = new ReportPdfService(_reports.Object, _root.Env.Object);
        }

        public void Dispose() => _root.Dispose();

        [Fact]
        public async Task GenerateStudentReportPdfAsync_ReturnsAPdf_UsingTheStudentQuery()
        {
            var reportId = Guid.NewGuid();
            _reports.Setup(r => r.GetStudentPdfDetailsAsync("stu-1", reportId)).ReturnsAsync(MakeModel(reportId));

            var pdf = await _sut.GenerateStudentReportPdfAsync("stu-1", reportId);

            ShouldBeAPdf(pdf);
            _reports.Verify(r => r.GetStudentPdfDetailsAsync("stu-1", reportId), Times.Once);
            _reports.Verify(r => r.GetSupervisorPdfDetailsAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GenerateSupervisorReportPdfAsync_ReturnsAPdf_UsingTheSupervisorQuery()
        {
            var reportId = Guid.NewGuid();
            _reports.Setup(r => r.GetSupervisorPdfDetailsAsync("sup-1", reportId)).ReturnsAsync(MakeModel(reportId));

            var pdf = await _sut.GenerateSupervisorReportPdfAsync("sup-1", reportId);

            ShouldBeAPdf(pdf);
            _reports.Verify(r => r.GetSupervisorPdfDetailsAsync("sup-1", reportId), Times.Once);
            _reports.Verify(r => r.GetStudentPdfDetailsAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GenerateStudentReportPdfAsync_WhenMriImageExistsOnDisk_EmbedsItAndStillReturnsAPdf()
        {
            var reportId = Guid.NewGuid();
            _root.WriteAppDataFile("mri", "scan.png", Convert.FromBase64String(OnePixelPngBase64));
            _reports.Setup(r => r.GetStudentPdfDetailsAsync("stu-1", reportId)).ReturnsAsync(MakeModel(reportId));

            var pdf = await _sut.GenerateStudentReportPdfAsync("stu-1", reportId);

            ShouldBeAPdf(pdf);
        }

        // A report with no answers and no AI result must still render
        // (the PDF shows "No answers available." / "Not Available").
        [Fact]
        public async Task GenerateStudentReportPdfAsync_WhenReportHasNoAnswersOrPrediction_StillReturnsAPdf()
        {
            var reportId = Guid.NewGuid();
            var model = MakeModel(reportId);
            model.Answers.Clear();
            model.Probabilities.Clear();
            model.PredictionResult = null;
            model.ReportCode = null;
            _reports.Setup(r => r.GetStudentPdfDetailsAsync("stu-1", reportId)).ReturnsAsync(model);

            var pdf = await _sut.GenerateStudentReportPdfAsync("stu-1", reportId);

            ShouldBeAPdf(pdf);
        }

        // The access checks live in ReportService. Its exceptions must pass
        // straight through, so the controller still answers 404 / 400.
        [Fact]
        public async Task GenerateSupervisorReportPdfAsync_WhenReportServiceThrows_PropagatesTheException()
        {
            _reports.Setup(r => r.GetSupervisorPdfDetailsAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                    .ThrowsAsync(new NotFoundException("Report not found"));

            var act = () => _sut.GenerateSupervisorReportPdfAsync("sup-1", Guid.NewGuid());

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Report not found");
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static void ShouldBeAPdf(byte[] pdf)
        {
            pdf.Should().NotBeNull();
            pdf.Length.Should().BeGreaterThan(4);
            System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        }

        private static ReportPdfResponse MakeModel(Guid reportId)
            => new()
            {
                ReportId = reportId,
                ReportCode = "REP-2026-000001",
                CaseId = Guid.NewGuid(),
                StudentId = "stu-1",
                StudentName = "Student One",
                SupervisorName = "Dr. Supervisor",
                SubmittedAt = new DateTime(2026, 3, 1, 12, 0, 0),
                CaseCreatedAt = new DateTime(2026, 3, 1, 10, 0, 0),
                PredictionCreatedAt = new DateTime(2026, 3, 1, 11, 0, 0),
                StoredFileName = "scan.png",
                PredictionResult = "glioma",
                Probabilities = new List<ProbabilityItemResponse>
                {
                    new() { Label = "Glioma", Value = 0.7f },
                    new() { Label = "Meningioma", Value = 0.1f },
                    new() { Label = "No Tumor", Value = 0.15f },
                    new() { Label = "Pituitary", Value = 0.05f }
                },
                Answers = new List<ReportPdfAnswerResponse>
                {
                    new()
                    {
                        QuestionId = Guid.NewGuid(),
                        Code = "Preliminary Assesment",
                        Question = "Is there a tumor?",
                        Type = ReportQuestionType.SingleChoice,
                        AnswerValue = "Tumor"
                    },
                    new()
                    {
                        QuestionId = Guid.NewGuid(),
                        Code = "Details",
                        Question = "Describe the tumor location.",
                        Type = ReportQuestionType.Text,
                        AnswerValue = ""       // rendered as "No answer"
                    }
                }
            };
    }
}

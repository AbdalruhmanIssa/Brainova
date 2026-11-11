using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// MriCaseService is the core domain service: upload a case, and list
    /// cases for a student / for a supervisor.
    ///
    /// The list methods only return cases that are Predicted or Reviewed, and
    /// join each case to its report and feedback. The tests seed cases in
    /// every status and for other users, to prove the filters hold.
    /// </summary>
    public class MriCaseServiceTests
    {
        private static readonly DateTime Jan1 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IFileService> _files = new();
        private readonly MriCaseService _sut;

        public MriCaseServiceTests()
        {
            _sut = new MriCaseService(_uow.Object, _files.Object);
        }

        // ============================ CreateAsync ============================

        [Fact]
        public async Task CreateAsync_SavesTheFile_ThenAddsAnUploadedCaseForTheStudent()
        {
            var file = Mock.Of<IFormFile>();
            _files.Setup(f => f.SaveAsync(file, "mri", It.IsAny<CancellationToken>()))
                  .ReturnsAsync("stored-name.jpg");
            var caseRepo = _uow.SetupRepo(new List<MriCase>());

            var result = await _sut.CreateAsync("stu-1", new MriUploadRequest { File = file });

            caseRepo.Verify(r => r.AddAsync(
                It.Is<MriCase>(c => c.StudentId == "stu-1"
                                 && c.StoredFileName == "stored-name.jpg"
                                 && c.Status == CaseStatus.Uploaded),
                It.IsAny<CancellationToken>()), Times.Once);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

            result.Status.Should().Be(CaseStatus.Uploaded);
            result.StoredFileName.Should().Be("stored-name.jpg");
        }

        // If the file cannot be saved, no case row may be created — otherwise
        // the database would point at an image that does not exist.
        [Fact]
        public async Task CreateAsync_WhenFileSaveFails_DoesNotCreateACase()
        {
            _files.Setup(f => f.SaveAsync(It.IsAny<IFormFile>(), "mri", It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new Exception("File is required."));
            var caseRepo = _uow.SetupRepo(new List<MriCase>());

            var act = () => _sut.CreateAsync("stu-1", new MriUploadRequest { File = Mock.Of<IFormFile>() });

            await act.Should().ThrowAsync<Exception>().WithMessage("File is required.");
            caseRepo.Verify(r => r.AddAsync(It.IsAny<MriCase>(), It.IsAny<CancellationToken>()), Times.Never);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ====================== GetStoredFileNameAsync =======================

        [Fact]
        public async Task GetStoredFileNameAsync_WhenCaseExists_ReturnsItsFileName()
        {
            var mriCase = new MriCase { Id = Guid.NewGuid(), StoredFileName = "abc.jpg" };
            var caseRepo = _uow.SetupRepo(new List<MriCase> { mriCase });
            caseRepo.Setup(r => r.GetByIdAsync(mriCase.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mriCase);

            var result = await _sut.GetStoredFileNameAsync(mriCase.Id);

            result.Should().Be("abc.jpg");
        }

        [Fact]
        public async Task GetStoredFileNameAsync_WhenCaseDoesNotExist_ReturnsNull()
        {
            var caseRepo = _uow.SetupRepo(new List<MriCase>());
            caseRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((MriCase?)null);

            var result = await _sut.GetStoredFileNameAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        // ========================== GetMyCasesAsync ==========================

        [Fact]
        public async Task GetMyCasesAsync_ReturnsOnlyThisStudentsPredictedOrReviewedCases_NewestFirst()
        {
            var older = MakeCase("stu-1", CaseStatus.Predicted, Jan1);
            var newer = MakeCase("stu-1", CaseStatus.Reviewed, Jan1.AddDays(2));
            var uploadedOnly = MakeCase("stu-1", CaseStatus.Uploaded, Jan1.AddDays(3));
            var reportOnly = MakeCase("stu-1", CaseStatus.ReportSubmitted, Jan1.AddDays(4));
            var someoneElses = MakeCase("stu-2", CaseStatus.Predicted, Jan1.AddDays(5));

            _uow.SetupRepo(new List<MriCase> { older, newer, uploadedOnly, reportOnly, someoneElses });
            _uow.SetupRepo(new List<Report>());
            _uow.SetupRepo(new List<Feedback>());

            var result = await _sut.GetMyCasesAsync("stu-1");

            result.Select(c => c.CaseId).Should().Equal(newer.Id, older.Id);
        }

        [Fact]
        public async Task GetMyCasesAsync_MapsReportFeedbackAndPredictionOntoTheCase()
        {
            var mriCase = MakeCase("stu-1", CaseStatus.Reviewed, Jan1, withAi: true);
            var report = new Report
            {
                Id = Guid.NewGuid(),
                CaseId = mriCase.Id,
                StudentId = "stu-1",
                ReportCode = "REP-2026-000001",
                SubmittedAt = Jan1.AddHours(1)
            };
            var feedback = new Feedback
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                CreatedAt = Jan1.AddHours(2)
            };

            _uow.SetupRepo(new List<MriCase> { mriCase });
            _uow.SetupRepo(new List<Report> { report });
            _uow.SetupRepo(new List<Feedback> { feedback });

            var result = await _sut.GetMyCasesAsync("stu-1");

            var item = result.Should().ContainSingle().Subject;
            item.CaseId.Should().Be(mriCase.Id);
            item.IsReviewed.Should().BeTrue();
            item.IsPredicted.Should().BeTrue();
            item.IsReportSubmitted.Should().BeTrue();

            item.ReportId.Should().Be(report.Id);
            item.ReportCode.Should().Be("REP-2026-000001");
            item.ReportSubmittedAt.Should().NotBeNull();
            item.FeedbackId.Should().Be(feedback.Id);
            item.FeedbackSubmittedAt.Should().NotBeNull();

            item.PredictionResult.Should().Be("glioma");
            item.ImageUrl.Should().Be("/api/Student/MriCases/image/scan.jpg");
            item.GradcamUrl.Should().Be("/api/AiTumors/gradcam-image/cam.jpg");
        }

        [Fact]
        public async Task GetMyCasesAsync_WhenCaseHasNoReportOrPrediction_LeavesThoseFieldsNull()
        {
            var mriCase = MakeCase("stu-1", CaseStatus.Predicted, Jan1, withAi: false);

            _uow.SetupRepo(new List<MriCase> { mriCase });
            _uow.SetupRepo(new List<Report>());
            _uow.SetupRepo(new List<Feedback>());

            var result = await _sut.GetMyCasesAsync("stu-1");

            var item = result.Should().ContainSingle().Subject;
            item.IsReviewed.Should().BeFalse();
            item.ReportId.Should().BeNull();
            item.ReportSubmittedAt.Should().BeNull();
            item.FeedbackId.Should().BeNull();
            item.FeedbackSubmittedAt.Should().BeNull();
            item.PredictionResult.Should().BeNull();
            item.GradcamUrl.Should().BeNull();
        }

        // ====================== GetSupervisorCasesAsync ======================

        [Fact]
        public async Task GetSupervisorCasesAsync_ReturnsOnlyCasesOfThisSupervisorsStudents()
        {
            var myStudent = MakeStudent("stu-1", "sup-1", "Student One");
            var otherStudent = MakeStudent("stu-2", "sup-2", "Student Two");

            var mine = MakeCase("stu-1", CaseStatus.Predicted, Jan1, student: myStudent);
            var mineNotPredicted = MakeCase("stu-1", CaseStatus.Uploaded, Jan1.AddDays(1), student: myStudent);
            var notMine = MakeCase("stu-2", CaseStatus.Predicted, Jan1.AddDays(2), student: otherStudent);

            _uow.SetupRepo(new List<MriCase> { mine, mineNotPredicted, notMine });
            _uow.SetupRepo(new List<Report>());
            _uow.SetupRepo(new List<Feedback>());

            var result = await _sut.GetSupervisorCasesAsync("sup-1");

            var item = result.Should().ContainSingle().Subject;
            item.CaseId.Should().Be(mine.Id);
            item.StudentId.Should().Be("stu-1");
            item.StudentName.Should().Be("Student One");
            item.StudentEmail.Should().Be("stu-1@brainova.test");
        }

        [Fact]
        public async Task GetSupervisorCasesAsync_WhenStudentIdIsGiven_FiltersToThatStudent()
        {
            var studentA = MakeStudent("stu-a", "sup-1", "A");
            var studentB = MakeStudent("stu-b", "sup-1", "B");

            var caseA = MakeCase("stu-a", CaseStatus.Predicted, Jan1, student: studentA);
            var caseB = MakeCase("stu-b", CaseStatus.Reviewed, Jan1.AddDays(1), student: studentB);

            _uow.SetupRepo(new List<MriCase> { caseA, caseB });
            _uow.SetupRepo(new List<Report>());
            _uow.SetupRepo(new List<Feedback>());

            var all = await _sut.GetSupervisorCasesAsync("sup-1");
            var onlyB = await _sut.GetSupervisorCasesAsync("sup-1", "stu-b");

            all.Select(c => c.CaseId).Should().Equal(caseB.Id, caseA.Id);   // newest first
            onlyB.Select(c => c.CaseId).Should().Equal(caseB.Id);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static ApplicationUser MakeStudent(string id, string supervisorId, string fullName)
            => new()
            {
                Id = id,
                FullName = fullName,
                Email = id + "@brainova.test",
                SupervisorUserId = supervisorId
            };

        private static MriCase MakeCase(
            string studentId,
            CaseStatus status,
            DateTime createdAtUtc,
            bool withAi = false,
            ApplicationUser? student = null)
        {
            var mriCase = new MriCase
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                Student = student!,
                StoredFileName = "scan.jpg",
                Status = status,
                CreatedAt = createdAtUtc
            };

            if (withAi)
            {
                mriCase.AiResult = new AiResult
                {
                    CaseId = mriCase.Id,
                    PredictionResult = "glioma",
                    GradcamFileName = "cam.jpg",
                    CreatedAt = createdAtUtc.AddMinutes(30)
                };
            }

            return mriCase;
        }
    }
}

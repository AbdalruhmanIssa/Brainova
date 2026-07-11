using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using FluentAssertions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// Example test class — read top to bottom, the tests get progressively harder.
    ///
    /// LEVEL 1: guard clauses (no mock setup needed at all)
    /// LEVEL 2: [Theory] — one test, many inputs
    /// LEVEL 3: mocking Query() with MockQueryable so EF async operators work
    /// LEVEL 4: happy path — Setup + Verify together
    /// LEVEL 5: behavior test — notifications are best-effort
    /// </summary>
    public class FeedbackServiceTests
    {
        // "Mock<T>" builds a fake implementation of the interface.
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<INotificationService> _notifications = new();

        // "sut" = System Under Test. The ONLY real class in these tests.
        private readonly FeedbackService _sut;

        public FeedbackServiceTests()
        {
            // xUnit creates a NEW instance of this class for EVERY test method,
            // so this constructor runs before each test → no shared state.
            _sut = new FeedbackService(_uow.Object, _notifications.Object);
        }

        // =====================================================================
        // LEVEL 1 — guard clauses. The service throws before touching _uow,
        // so the mocks need zero setup. Easiest tests you'll ever write.
        // =====================================================================

        [Fact]
        public async Task AddAsync_WhenSupervisorIdIsEmpty_ThrowsUnauthorized()
        {
            // Act — wrap the call in a delegate, don't await it directly
            var act = () => _sut.AddAsync("", Guid.NewGuid(), ValidRequest());

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                     .WithMessage("Supervisor is not authenticated");
        }

        [Fact]
        public async Task AddAsync_WhenReportIdIsEmpty_ThrowsBadRequest()
        {
            var act = () => _sut.AddAsync("sup-1", Guid.Empty, ValidRequest());

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("ReportId is required");
        }

        [Fact]
        public async Task AddAsync_WhenRequestIsNull_ThrowsBadRequest()
        {
            var act = () => _sut.AddAsync("sup-1", Guid.NewGuid(), null!);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        // =====================================================================
        // LEVEL 2 — [Theory]: same test body, multiple inputs.
        // Each InlineData shows up as a separate test result.
        // =====================================================================

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddAsync_WhenCommentIsBlank_ThrowsBadRequest(string? comment)
        {
            var request = new CreateFeedbackRequest { Comment = comment! };

            var act = () => _sut.AddAsync("sup-1", Guid.NewGuid(), request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Comment is required");
        }

        // =====================================================================
        // LEVEL 3 — the service now reaches _uow.Repo<Report>().Query().
        // Query() returns IQueryable and the service calls FirstOrDefaultAsync
        // + Include on it. Plain lists can't do async → MockQueryable's
        // BuildMock() wraps a list so EF's async operators work.
        // =====================================================================

        [Fact]
        public async Task AddAsync_WhenReportDoesNotExist_ThrowsNotFound()
        {
            // Arrange: an EMPTY report table
            SetupRepo<Report>(new List<Report>());

            var act = () => _sut.AddAsync("sup-1", Guid.NewGuid(), ValidRequest());

            await act.Should().ThrowAsync<NotFoundException>()
                     .WithMessage("Report not found");
        }

        [Fact]
        public async Task AddAsync_WhenSupervisorDoesNotOwnStudent_ThrowsForbidden()
        {
            // Arrange: report exists, but the student belongs to ANOTHER supervisor
            var report = MakeReport(supervisorUserId: "someone-else");
            SetupRepo(new List<Report> { report });

            var act = () => _sut.AddAsync("sup-1", report.Id, ValidRequest());

            await act.Should().ThrowAsync<ForbiddenException>()
                     .WithMessage("You are not allowed to add feedback to this report");
        }

        [Fact]
        public async Task AddAsync_WhenFeedbackAlreadyExists_ThrowsBadRequest()
        {
            var report = MakeReport(supervisorUserId: "sup-1");
            SetupRepo(new List<Report> { report });
            // Feedback table already has a row for this report → AnyAsync == true
            SetupRepo(new List<Feedback> { new() { ReportId = report.Id } });

            var act = () => _sut.AddAsync("sup-1", report.Id, ValidRequest());

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Feedback already exists for this report");
        }

        // =====================================================================
        // LEVEL 4 — happy path. This is where Moq's Verify() shines:
        // we don't just check the return value, we PROVE the service did the
        // right side-effects (saved, added, flipped case status, notified).
        // =====================================================================

        [Fact]
        public async Task AddAsync_HappyPath_SavesFeedbackAndMarksCaseReviewed()
        {
            // Arrange
            var report = MakeReport(supervisorUserId: "sup-1");
            var reportRepo = SetupRepo(new List<Report> { report });
            var feedbackRepo = SetupRepo(new List<Feedback>());        // no existing feedback
            SetupRepo(new List<ApplicationUser>                        // for supervisor-name lookup
            {
                new() { Id = "sup-1", FullName = "Dr. Supervisor" }
            });
            var caseRepo = SetupRepo(new List<MriCase>());

            // Act
            var result = await _sut.AddAsync("sup-1", report.Id,
                new CreateFeedbackRequest { Comment = "  Nice work  " });

            // Assert — return value
            result.Should().Be("Feedback added successfully");

            // Assert — side effects, via Verify:
            // 1) a feedback row was added, with the comment TRIMMED
            feedbackRepo.Verify(r => r.AddAsync(
                It.Is<Feedback>(f => f.Comment == "Nice work"
                                  && f.ReportId == report.Id
                                  && f.SupervisorId == "sup-1"
                                  && f.IsSeen == false),
                It.IsAny<CancellationToken>()), Times.Once);

            // 2) the MRI case was flipped to Reviewed and updated
            report.Case.Status.Should().Be(CaseStatus.Reviewed);
            caseRepo.Verify(r => r.Update(report.Case), Times.Once);

            // 3) everything was persisted exactly once
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

            // 4) the student got a real-time push
            _notifications.Verify(n => n.NotifyStudentNewFeedbackAsync(
                report.StudentId,
                It.IsAny<Brainova.BLL.DTOs.Response.StudentFeedbackNotificationResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // LEVEL 5 — behavior lock-in. The service comments say: "a SignalR
        // failure must NOT fail the request — the feedback is saved either
        // way". This test makes that promise permanent: if someone removes
        // the try/catch later, this test fails.
        // =====================================================================

        [Fact]
        public async Task AddAsync_WhenNotificationThrows_StillSucceeds()
        {
            var report = MakeReport(supervisorUserId: "sup-1");
            SetupRepo(new List<Report> { report });
            SetupRepo(new List<Feedback>());
            SetupRepo(new List<ApplicationUser> { new() { Id = "sup-1", FullName = "X" } });
            SetupRepo(new List<MriCase>());

            // Make the notification blow up
            _notifications
                .Setup(n => n.NotifyStudentNewFeedbackAsync(
                    It.IsAny<string>(),
                    It.IsAny<Brainova.BLL.DTOs.Response.StudentFeedbackNotificationResponse>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR is down"));

            // Act — must NOT throw
            var result = await _sut.AddAsync("sup-1", report.Id, ValidRequest());

            // Assert — feedback was still saved
            result.Should().Be("Feedback added successfully");
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // Helpers — small builders keep the tests short and readable.
        // =====================================================================

        /// <summary>
        /// Wires _uow.Repo&lt;T&gt;() to a fake repository whose Query()
        /// serves the given in-memory list (async-capable via BuildMock).
        /// Returns the repo mock so tests can Verify() calls on it.
        /// </summary>
        private Mock<IGenericRepository<T>> SetupRepo<T>(List<T> data) where T : class
        {
            var repo = new Mock<IGenericRepository<T>>();
            repo.Setup(r => r.Query()).Returns(data.BuildMock());
            _uow.Setup(u => u.Repo<T>()).Returns(repo.Object);
            return repo;
        }

        private static CreateFeedbackRequest ValidRequest()
            => new() { Comment = "Looks good, well analyzed." };

        private static Report MakeReport(string supervisorUserId)
        {
            var studentId = "student-1";
            return new Report
            {
                Id = Guid.NewGuid(),
                ReportCode = "RPT-001",
                StudentId = studentId,
                Student = new ApplicationUser
                {
                    Id = studentId,
                    FullName = "Student One",
                    SupervisorUserId = supervisorUserId
                },
                CaseId = Guid.NewGuid(),
                Case = new MriCase { Status = CaseStatus.Uploaded }
            };
        }
    }
}

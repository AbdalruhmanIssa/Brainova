using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// DashboardService only counts rows. The tests seed small in-memory
    /// tables that contain rows which SHOULD be counted and rows which should
    /// NOT (another student, another supervisor), then check the numbers.
    /// </summary>
    public class DashboardServiceTests
    {
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly DashboardService _sut;

        public DashboardServiceTests()
        {
            _sut = new DashboardService(_uow.Object);
        }

        // ------------------------- student dashboard -------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetStudentDashboardSummaryAsync_WhenStudentIdIsBlank_ThrowsUnauthorized(string? studentId)
        {
            var act = () => _sut.GetStudentDashboardSummaryAsync(studentId!);

            await act.Should().ThrowAsync<UnauthorizedException>()
                     .WithMessage("Student is not authenticated");
        }

        [Fact]
        public async Task GetStudentDashboardSummaryAsync_CountsOnlyThisStudentsRows()
        {
            _uow.SetupRepo(new List<MriCase>
            {
                new() { StudentId = "stu-1", Status = CaseStatus.Uploaded },
                new() { StudentId = "stu-1", Status = CaseStatus.Predicted },
                new() { StudentId = "stu-1", Status = CaseStatus.Reviewed },
                new() { StudentId = "someone-else", Status = CaseStatus.Predicted }
            });
            _uow.SetupRepo(new List<Report>
            {
                new() { StudentId = "stu-1" },
                new() { StudentId = "stu-1" },
                new() { StudentId = "someone-else" }
            });
            _uow.SetupRepo(new List<Feedback>
            {
                new() { StudentId = "stu-1" },
                new() { StudentId = "someone-else" }
            });

            var result = await _sut.GetStudentDashboardSummaryAsync("stu-1");

            result.TotalCases.Should().Be(3);
            result.ReportsSubmitted.Should().Be(2);
            result.FeedbackReceived.Should().Be(1);
        }

        [Fact]
        public async Task GetStudentDashboardSummaryAsync_WhenStudentHasNothing_ReturnsZeros()
        {
            _uow.SetupRepo(new List<MriCase>());
            _uow.SetupRepo(new List<Report>());
            _uow.SetupRepo(new List<Feedback>());

            var result = await _sut.GetStudentDashboardSummaryAsync("stu-1");

            result.TotalCases.Should().Be(0);
            result.ReportsSubmitted.Should().Be(0);
            result.FeedbackReceived.Should().Be(0);
        }

        // ------------------------ supervisor dashboard -----------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetSupervisorDashboardSummaryAsync_WhenSupervisorIdIsBlank_ThrowsUnauthorized(string? supervisorId)
        {
            var act = () => _sut.GetSupervisorDashboardSummaryAsync(supervisorId!);

            await act.Should().ThrowAsync<UnauthorizedException>()
                     .WithMessage("Supervisor is not authenticated");
        }

        [Fact]
        public async Task GetSupervisorDashboardSummaryAsync_CountsStudentsReportsAndFeedback()
        {
            var mine1 = new ApplicationUser { Id = "stu-1", SupervisorUserId = "sup-1" };
            var mine2 = new ApplicationUser { Id = "stu-2", SupervisorUserId = "sup-1" };
            var other = new ApplicationUser { Id = "stu-3", SupervisorUserId = "sup-2" };

            _uow.SetupRepo(new List<ApplicationUser> { mine1, mine2, other });
            _uow.SetupRepo(new List<Report>
            {
                new() { StudentId = mine1.Id, Student = mine1 },
                new() { StudentId = mine1.Id, Student = mine1 },
                new() { StudentId = mine2.Id, Student = mine2 },
                new() { StudentId = other.Id, Student = other }
            });
            _uow.SetupRepo(new List<Feedback>
            {
                new() { SupervisorId = "sup-1" },
                new() { SupervisorId = "sup-2" }
            });

            var result = await _sut.GetSupervisorDashboardSummaryAsync("sup-1");

            result.TotalStudents.Should().Be(2);
            result.TotalReports.Should().Be(3);
            result.FeedbackGiven.Should().Be(1);
            // "new" = reports that have no feedback yet = 3 - 1
            result.NewReports.Should().Be(2);
        }

        // The service clamps NewReports at 0 so the UI never shows "-1 new reports".
        [Fact]
        public async Task GetSupervisorDashboardSummaryAsync_WhenFeedbackExceedsReports_NewReportsIsZero()
        {
            var student = new ApplicationUser { Id = "stu-1", SupervisorUserId = "sup-1" };

            _uow.SetupRepo(new List<ApplicationUser> { student });
            _uow.SetupRepo(new List<Report> { new() { StudentId = student.Id, Student = student } });
            _uow.SetupRepo(new List<Feedback>
            {
                new() { SupervisorId = "sup-1" },
                new() { SupervisorId = "sup-1" }
            });

            var result = await _sut.GetSupervisorDashboardSummaryAsync("sup-1");

            result.NewReports.Should().Be(0);
        }
    }
}

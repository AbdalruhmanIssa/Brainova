using System.Linq.Expressions;
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using MockQueryable;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// Round 2 — your turn. Two tests are written as worked examples,
    /// four are TODOs for you. New concepts here:
    ///
    /// 1. Mocking UserManager (ugly constructor — see MockUserManager helper)
    /// 2. Mocking ExistsAsync, which takes an Expression argument
    /// </summary>
    public class ReportQuestionServiceTests
    {
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<INotificationService> _notifications = new();
        private readonly ReportQuestionService _sut;

        public ReportQuestionServiceTests()
        {
            _userManager = MockUserManager();
            _sut = new ReportQuestionService(_uow.Object, _userManager.Object, _notifications.Object);
        }

        // =====================================================================
        // WORKED EXAMPLE 1 — same pattern as FeedbackServiceTests Level 3.
        // =====================================================================

        [Fact]
        public async Task GetActiveForStudent_WhenStudentDoesNotExist_ThrowsNotFound()
        {
            // Arrange: empty users table
            SetupRepo<ApplicationUser>(new List<ApplicationUser>());

            // Act
            var act = () => _sut.GetActiveForStudentAsync("ghost-student");

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                     .WithMessage("Student not found");
        }

        // =====================================================================
        // WORKED EXAMPLE 2 — mocking UserManager + ExistsAsync.
        //
        // AddAsync flow: EnsureSupervisorExists (UserManager) → ExistsAsync
        // for duplicate code → ExistsAsync for duplicate order → validation.
        //
        // NEW CONCEPT: ExistsAsync takes an Expression<Func<T,bool>>. You
        // can't easily match WHICH expression was passed, so you use
        // It.IsAny<Expression<Func<ReportQuestion, bool>>>() and control the
        // returned bool. Here: first call (code check) returns true → throws.
        // =====================================================================

        [Fact]
        public async Task AddAsync_WhenCodeAlreadyExists_ThrowsBadRequest()
        {
            // Arrange: a valid supervisor…
            SetupValidSupervisor("sup-1");

            // …and a question repo where ANY ExistsAsync check returns true —
            // the code-duplicate check runs first, so that's what throws.
            var repo = new Mock<IGenericRepository<ReportQuestion>>();
            repo.Setup(r => r.ExistsAsync(
                    It.IsAny<Expression<Func<ReportQuestion, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _uow.Setup(u => u.Repo<ReportQuestion>()).Returns(repo.Object);

            // Act
            var act = () => _sut.AddAsync("sup-1", ValidRequest());

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Question code already exists for this supervisor.");
        }

        // =====================================================================
        // TODO 1 (easy): GetActiveForStudentAsync — student EXISTS but has
        // SupervisorUserId = null → should throw BadRequestException
        // "Student has no assigned supervisor".
        // Hint: SetupRepo with a list containing one ApplicationUser whose
        // Id matches what you pass in, SupervisorUserId left null.
        // =====================================================================
        [Fact]
        public async Task GetActiveForStudentAsync_WhenStudentHasNoAssignedSupervisor_ThrowsBadRequest()
        {
           
            var student = new ApplicationUser { Id = "student-1", SupervisorUserId = null };
            SetupRepo(new List<ApplicationUser> { student });

            var act = () => _sut.GetActiveForStudentAsync(student.Id);   // ① right method, ② pass the STUDENT's id

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Student has no assigned supervisor");
        }


        // =====================================================================
        // TODO 2 (easy): AddAsync — supervisor does not exist → NotFoundException
        // "Supervisor not found".
        // Hint: _userManager.Setup(m => m.FindByIdAsync("sup-1"))
        //           .ReturnsAsync((ApplicationUser?)null);
        // No repo setup needed — it throws before reaching the repo.
        // =====================================================================
        public async Task AddAsync_WhenSupervisorDoesNotExist_ThrowsNotFound()
        {
            // Arrange: mock UserManager to return null for the supervisor
            _userManager.Setup(m => m.FindByIdAsync("sup-1"))
                        .ReturnsAsync((ApplicationUser?)null);
            // Act
            var act = () => _sut.AddAsync("sup-1", ValidRequest());
            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                     .WithMessage("Supervisor not found");
        }


        // =====================================================================
        // TODO 3 (medium): AddAsync — user exists but is NOT in the
        // "Supervisor" role → BadRequestException "Invalid supervisor".
        // Hint: FindByIdAsync returns a user, but GetRolesAsync returns
        // new List<string> { "Student" }.
        // =====================================================================



        // =====================================================================
        // TODO 4 (harder): GetActiveForStudentAsync happy path — seed the
        // ReportQuestion repo with 3 questions for the student's supervisor:
        // two active (Order 2 and Order 1) and one inactive. Assert the result
        // has exactly 2 items AND is ordered by Order (1 then 2).
        // Hints:
        //   - You need TWO SetupRepo calls: ApplicationUser + ReportQuestion.
        //   - result.Should().HaveCount(2);
        //   - result.Select(q => q.Order).Should().ContainInOrder(1, 2);
        // =====================================================================



        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// UserManager has no interface and a 9-parameter constructor.
        /// The standard trick: mock IUserStore (its only required dependency)
        /// and pass null for the other 8. Every ASP.NET project tests it this way.
        /// </summary>
        private static Mock<UserManager<ApplicationUser>> MockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        /// <summary>Makes _userManager say: this id exists and IS a Supervisor.</summary>
        private void SetupValidSupervisor(string supervisorId)
        {
            var supervisor = new ApplicationUser { Id = supervisorId, FullName = "Dr. Sup" };

            _userManager.Setup(m => m.FindByIdAsync(supervisorId))
                        .ReturnsAsync(supervisor);

            _userManager.Setup(m => m.GetRolesAsync(supervisor))
                        .ReturnsAsync(new List<string> { "Supervisor" });
        }

        private Mock<IGenericRepository<T>> SetupRepo<T>(List<T> data) where T : class
        {
            var repo = new Mock<IGenericRepository<T>>();
            repo.Setup(r => r.Query()).Returns(data.BuildMock());
            _uow.Setup(u => u.Repo<T>()).Returns(repo.Object);
            return repo;
        }

        private static CreateReportQuestionRequest ValidRequest() => new()
        {
            Code = "Q1",
            Text = "Describe the tumor location.",
            Type = ReportQuestionType.Text,
            Order = 1,
            IsActive = true
        };
    }
}

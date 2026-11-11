using System.Security.Claims;
using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.Realtime;
using Brainova.BLL.DTOs.User;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using MockQueryable;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// UserService is the largest service (user management for admins).
    /// It has seven dependencies; all are mocked.
    ///
    /// Two things are specific to this class:
    ///
    /// 1. "Who is calling?" — the service reads the acting admin's id from
    ///    the HTTP context. ActAs("admin-1") puts a fake logged-in user there.
    ///
    /// 2. Repository methods that return a TUPLE, e.g.
    ///    (bool Success, string Message, ApplicationUser? User). For those,
    ///    the setups use .Returns(Task.FromResult&lt;(...)&gt;(...)) and spell the
    ///    tuple type out, so the compiler does not have to guess it.
    /// </summary>
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _userRepository = new();
        private readonly Mock<UserManager<ApplicationUser>> _userManager = IdentityMocks.CreateUserManager();
        private readonly Mock<IEmailSender> _email = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IHttpContextAccessor> _http = new();
        private readonly Mock<IReportQuestionService> _questions = new();
        private readonly Mock<INotificationService> _notifications = new();
        private readonly UserService _sut;

        public UserServiceTests()
        {
            _sut = new UserService(
                _userRepository.Object,
                _userManager.Object,
                _email.Object,
                _uow.Object,
                _http.Object,
                _questions.Object,
                _notifications.Object);
        }

        // =====================================================================
        // GetAllAsync
        // =====================================================================

        [Fact]
        public async Task GetAllAsync_WhenThereAreNoUsers_ReturnsEmptyList()
        {
            _userRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ApplicationUser>());

            var result = await _sut.GetAllAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_MapsRoles_AndFillsSupervisorOnlyForStudents()
        {
            var supervisor = MakeUser("sup-1", "Dr. Supervisor");
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            // An admin row that (wrongly) still carries a SupervisorUserId:
            // the DTO must NOT expose it, because only students have supervisors.
            var admin = MakeUser("adm-1", "Admin One", supervisorId: "sup-1");

            _userRepository.Setup(r => r.GetAllAsync())
                           .ReturnsAsync(new List<ApplicationUser> { supervisor, student, admin });

            _uow.SetupRepo(new List<IdentityRole>
            {
                new("Supervisor") { Id = "role-sup" },
                new("Student") { Id = "role-stu" },
                new("Admin") { Id = "role-adm" }
            });
            _uow.SetupRepo(new List<IdentityUserRole<string>>
            {
                new() { UserId = "sup-1", RoleId = "role-sup" },
                new() { UserId = "stu-1", RoleId = "role-stu" },
                new() { UserId = "adm-1", RoleId = "role-adm" }
            });
            _uow.SetupRepo(new List<ApplicationUser> { supervisor, student, admin });

            var result = await _sut.GetAllAsync();

            result.Should().HaveCount(3);

            var studentDto = result.Single(u => u.Id == "stu-1");
            studentDto.RoleName.Should().Be("Student");
            studentDto.SupervisorId.Should().Be("sup-1");
            studentDto.SupervisorName.Should().Be("Dr. Supervisor");

            var supervisorDto = result.Single(u => u.Id == "sup-1");
            supervisorDto.RoleName.Should().Be("Supervisor");
            supervisorDto.SupervisorId.Should().BeNull();

            var adminDto = result.Single(u => u.Id == "adm-1");
            adminDto.RoleName.Should().Be("Admin");
            adminDto.SupervisorId.Should().BeNull();
            adminDto.SupervisorName.Should().BeNull();
        }

        // =====================================================================
        // GetByIdAsync
        // =====================================================================

        [Fact]
        public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNull()
        {
            _userRepository.Setup(r => r.GetByIdAsync("ghost")).ReturnsAsync((ApplicationUser?)null);

            var result = await _sut.GetByIdAsync("ghost");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ForAStudent_IncludesTheSupervisorName()
        {
            var supervisor = MakeUser("sup-1", "Dr. Supervisor");
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            _userRepository.Setup(r => r.GetByIdAsync("stu-1")).ReturnsAsync(student);
            SetRoles(student, "Student");
            _userManager.Setup(m => m.FindByIdAsync("sup-1")).ReturnsAsync(supervisor);

            var result = await _sut.GetByIdAsync("stu-1");

            result!.RoleName.Should().Be("Student");
            result.SupervisorId.Should().Be("sup-1");
            result.SupervisorName.Should().Be("Dr. Supervisor");
            result.Email.Should().Be("stu-1@brainova.test");
        }

        [Fact]
        public async Task GetByIdAsync_ForANonStudent_HasNoSupervisor()
        {
            var admin = MakeUser("adm-1", "Admin One");
            _userRepository.Setup(r => r.GetByIdAsync("adm-1")).ReturnsAsync(admin);
            SetRoles(admin, "Admin");

            var result = await _sut.GetByIdAsync("adm-1");

            result!.RoleName.Should().Be("Admin");
            result.SupervisorId.Should().BeNull();
            result.SupervisorName.Should().BeNull();
        }

        // =====================================================================
        // GetSupervisorsAsync / GetSupervisorStudentsAsync
        // =====================================================================

        [Fact]
        public async Task GetSupervisorsAsync_ReturnsOnlyUsersThatHaveTheSupervisorRole()
        {
            var real = MakeUser("sup-1", "Dr. Real");
            var notReally = MakeUser("usr-2", "Not A Supervisor");
            _userRepository.Setup(r => r.GetSupervisorsAsync())
                           .ReturnsAsync(new List<ApplicationUser> { real, notReally });
            SetRoles(real, "Supervisor");
            SetRoles(notReally, "Admin");

            var result = await _sut.GetSupervisorsAsync();

            var only = result.Should().ContainSingle().Subject;
            only.Id.Should().Be("sup-1");
            only.FullName.Should().Be("Dr. Real");
        }

        [Fact]
        public async Task GetSupervisorStudentsAsync_ReturnsOwnStudentsWithTheirReportCounts()
        {
            var studentA = MakeUser("stu-a", "A", supervisorId: "sup-1");
            var studentB = MakeUser("stu-b", "B", supervisorId: "sup-1");
            var someoneElses = MakeUser("stu-c", "C", supervisorId: "sup-2");
            SetAllUsers(studentA, studentB, someoneElses);

            _uow.SetupRepo(new List<Report>
            {
                new() { StudentId = "stu-a" },
                new() { StudentId = "stu-a" },
                new() { StudentId = "stu-c" }
            });

            var result = await _sut.GetSupervisorStudentsAsync("sup-1");

            result.Select(s => s.StudentId).Should().BeEquivalentTo(new[] { "stu-a", "stu-b" });
            result.Single(s => s.StudentId == "stu-a").ReportsCount.Should().Be(2);
            result.Single(s => s.StudentId == "stu-b").ReportsCount.Should().Be(0);
        }

        // =====================================================================
        // Block / Unblock
        // =====================================================================

        [Fact]
        public async Task BlockUserAsync_OnSuccess_NotifiesTheUserTheAdminsAndTheStudentsSupervisor()
        {
            ActAs("admin-1");
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            _userManager.Setup(m => m.FindByIdAsync("stu-1")).ReturnsAsync(student);
            SetRoles(student, "Student");
            _userRepository.Setup(r => r.BlockUserAsync("stu-1")).ReturnsAsync(true);

            var result = await _sut.BlockUserAsync("stu-1");

            result.Should().BeTrue();

            // 1) the blocked user (frontend logs them out)
            _notifications.Verify(n => n.NotifyUserBlockedAsync("stu-1", It.IsAny<CancellationToken>()), Times.Once);

            // 2) admins, with WHO did it
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.Blocked
                                               && p.UserId == "stu-1"
                                               && p.ByUserId == "admin-1"),
                It.IsAny<CancellationToken>()), Times.Once);

            // 3) the student's supervisor
            _notifications.Verify(n => n.NotifySupervisorStudentsChangedAsync(
                "sup-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BlockUserAsync_WhenRepositoryReportsFailure_ReturnsFalseAndNotifiesNobody()
        {
            _userRepository.Setup(r => r.BlockUserAsync("ghost")).ReturnsAsync(false);

            var result = await _sut.BlockUserAsync("ghost");

            result.Should().BeFalse();
            _notifications.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task BlockUserAsync_WhenNotificationThrows_StillReturnsTrue()
        {
            _userRepository.Setup(r => r.BlockUserAsync("usr-1")).ReturnsAsync(true);
            _notifications.Setup(n => n.NotifyUserBlockedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new Exception("SignalR is down"));

            var result = await _sut.BlockUserAsync("usr-1");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task UnBlockUserAsync_OnSuccess_NotifiesTheUserAndAdmins()
        {
            var admin = MakeUser("adm-2", "Admin Two");
            _userManager.Setup(m => m.FindByIdAsync("adm-2")).ReturnsAsync(admin);
            _userRepository.Setup(r => r.UnBlockUserAsync("adm-2")).ReturnsAsync(true);

            var result = await _sut.UnBlockUserAsync("adm-2");

            result.Should().BeTrue();
            _notifications.Verify(n => n.NotifyUserUnblockedAsync("adm-2", It.IsAny<CancellationToken>()), Times.Once);
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.Unblocked && p.UserId == "adm-2"),
                It.IsAny<CancellationToken>()), Times.Once);
            // Not a student → no supervisor to tell.
            _notifications.Verify(n => n.NotifySupervisorStudentsChangedAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IsBlockedAsync_ReturnsWhatTheRepositorySays(bool blocked)
        {
            _userRepository.Setup(r => r.IsBlockedAsync("usr-1")).ReturnsAsync(blocked);

            var result = await _sut.IsBlockedAsync("usr-1");

            result.Should().Be(blocked);
        }

        // =====================================================================
        // Create (Supervisor / Admin / Student)
        // =====================================================================

        [Fact]
        public async Task CreateAdminAsync_WhenEmailAlreadyExists_ThrowsBadRequest()
        {
            var request = NewUserRequest();
            _userManager.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(new ApplicationUser());

            var act = () => _sut.CreateAdminAsync(request, null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Email already exists");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task CreateAdminAsync_WhenUserNameAlreadyExists_ThrowsBadRequest()
        {
            var request = NewUserRequest();
            _userManager.Setup(m => m.FindByNameAsync(request.UserName)).ReturnsAsync(new ApplicationUser());

            var act = () => _sut.CreateAdminAsync(request, null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Username already exists");
            VerifyNoUserWasCreated();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateStudentAsync_WhenSupervisorIdIsBlank_ThrowsBadRequest(string? supervisorId)
        {
            var act = () => _sut.CreateStudentAsync(NewUserRequest(supervisorId), null!);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("SupervisorUserId is required for student");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task CreateStudentAsync_WhenSupervisorDoesNotExist_ThrowsNotFound()
        {
            _userManager.Setup(m => m.FindByIdAsync("sup-1")).ReturnsAsync((ApplicationUser?)null);

            var act = () => _sut.CreateStudentAsync(NewUserRequest("sup-1"), null!);

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Supervisor not found");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task CreateStudentAsync_WhenChosenUserIsNotASupervisor_ThrowsBadRequest()
        {
            var notASupervisor = MakeUser("usr-9", "Some Admin");
            _userManager.Setup(m => m.FindByIdAsync("usr-9")).ReturnsAsync(notASupervisor);
            SetRoles(notASupervisor, "Admin");

            var act = () => _sut.CreateStudentAsync(NewUserRequest("usr-9"), null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid supervisor");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task CreateAdminAsync_WhenRepositoryFails_ThrowsBadRequestWithItsMessage_AndSendsNoEmail()
        {
            _userRepository.Setup(r => r.CreateUserWithRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
                .Returns(Task.FromResult<(bool Success, string Message, ApplicationUser? User)>(
                    (false, "Role does not exist", null)));

            var act = () => _sut.CreateAdminAsync(NewUserRequest(), null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Role does not exist");
            VerifyNoEmailSent();
        }

        [Fact]
        public async Task CreateSupervisorAsync_HappyPath_SeedsSystemQuestions_AndSendsSetPasswordEmail()
        {
            ActAs("admin-1");
            var request = NewUserRequest();
            RepositoryCreatesUsersWithRole("Supervisor");
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
                        .ReturnsAsync("reset-token");

            var message = await _sut.CreateSupervisorAsync(request, null!);

            message.Should().Be("Supervisor created successfully. Set-password email sent.");

            // A new supervisor gets the built-in (system) report questions.
            _questions.Verify(q => q.SeedSystemQuestionsForSupervisorAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

            // The account has no password yet: the email carries a set-password link.
            _email.Verify(e => e.SendEmailAsync(
                request.Email,
                It.Is<string>(subject => subject.Contains("Supervisor")),
                It.Is<string>(body => body.Contains("set-password") && body.Contains("reset-token"))),
                Times.Once);

            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.Created
                                               && p.Role == "Supervisor"
                                               && p.ByUserId == "admin-1"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAdminAsync_HappyPath_DoesNotSeedQuestions_AndStoresNoSupervisor()
        {
            // Even if the request carries a supervisor id, an Admin must not get one.
            var request = NewUserRequest(supervisorId: "sup-1");
            ApplicationUser? created = null;
            _userRepository.Setup(r => r.CreateUserWithRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
                .Callback((ApplicationUser u, string _) => created = u)
                .Returns((ApplicationUser u, string _) =>
                    Task.FromResult<(bool Success, string Message, ApplicationUser? User)>((true, "created", u)));
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
                        .ReturnsAsync("reset-token");

            var message = await _sut.CreateAdminAsync(request, null!);

            message.Should().Be("Admin created successfully. Set-password email sent.");
            created!.SupervisorUserId.Should().BeNull();
            created.EmailConfirmed.Should().BeFalse();
            _questions.Verify(q => q.SeedSystemQuestionsForSupervisorAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateStudentAsync_HappyPath_AssignsSupervisor_AndNotifiesThatSupervisor()
        {
            var supervisor = MakeUser("sup-1", "Dr. Supervisor");
            _userManager.Setup(m => m.FindByIdAsync("sup-1")).ReturnsAsync(supervisor);
            SetRoles(supervisor, "Supervisor");

            ApplicationUser? created = null;
            _userRepository.Setup(r => r.CreateUserWithRoleAsync(It.IsAny<ApplicationUser>(), "Student"))
                .Callback((ApplicationUser u, string _) => created = u)
                .Returns((ApplicationUser u, string _) =>
                    Task.FromResult<(bool Success, string Message, ApplicationUser? User)>((true, "created", u)));
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
                        .ReturnsAsync("reset-token");

            var message = await _sut.CreateStudentAsync(NewUserRequest("sup-1"), null!);

            message.Should().Be("Student created successfully. Set-password email sent.");
            created!.SupervisorUserId.Should().Be("sup-1");
            _notifications.Verify(n => n.NotifySupervisorStudentsChangedAsync(
                "sup-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // ChangeUserRoleAsync
        // =====================================================================

        [Fact]
        public async Task ChangeUserRoleAsync_WhenRoleNameIsUnknown_ThrowsBadRequest()
        {
            var act = () => _sut.ChangeUserRoleAsync(new ChangeUserRoleRequest { UserId = "usr-1", RoleName = "God" });

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid role name");
        }

        [Fact]
        public async Task ChangeUserRoleAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            var act = () => _sut.ChangeUserRoleAsync(new ChangeUserRoleRequest { UserId = "ghost", RoleName = "Admin" });

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        [Fact]
        public async Task ChangeUserRoleAsync_WhenRoleIsUnchanged_ReturnsIsChangedFalse_AndDoesNothingElse()
        {
            var user = MakeUser("usr-1", "User One");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(user);
            SetRoles(user, "Admin");

            var result = await _sut.ChangeUserRoleAsync(new ChangeUserRoleRequest { UserId = "usr-1", RoleName = "Admin" });

            result.IsChanged.Should().BeFalse();
            result.OldRole.Should().Be("Admin");
            result.NewRole.Should().Be("Admin");
            _userRepository.Verify(r => r.ChangeUserRoleAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            VerifyNoEmailSent();
            _notifications.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ChangeUserRoleAsync_WhenRepositoryFails_ThrowsBadRequestWithItsMessage()
        {
            var user = MakeUser("usr-1", "User One");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(user);
            SetRoles(user, "Student");
            _userRepository.Setup(r => r.ChangeUserRoleAsync("usr-1", "Admin"))
                .Returns(Task.FromResult<(bool Success, string Message, string? OldRole)>(
                    (false, "Could not change role", null)));

            var act = () => _sut.ChangeUserRoleAsync(new ChangeUserRoleRequest { UserId = "usr-1", RoleName = "Admin" });

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Could not change role");
            VerifyNoEmailSent();
        }

        [Fact]
        public async Task ChangeUserRoleAsync_HappyPath_EmailsTheUser_AndNotifiesUserAdminsAndOldSupervisor()
        {
            ActAs("admin-1");
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            _userManager.Setup(m => m.FindByIdAsync("stu-1")).ReturnsAsync(student);
            SetRoles(student, "Student");
            _userRepository.Setup(r => r.ChangeUserRoleAsync("stu-1", "Supervisor"))
                .Returns(Task.FromResult<(bool Success, string Message, string? OldRole)>(
                    (true, "ok", "Student")));

            var result = await _sut.ChangeUserRoleAsync(
                new ChangeUserRoleRequest { UserId = "stu-1", RoleName = "Supervisor" });

            result.IsChanged.Should().BeTrue();
            result.OldRole.Should().Be("Student");
            result.NewRole.Should().Be("Supervisor");

            _email.Verify(e => e.SendEmailAsync(student.Email!, It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            _notifications.Verify(n => n.NotifyUserRoleChangedAsync(
                "stu-1", "Student", "Supervisor", It.IsAny<CancellationToken>()), Times.Once);
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.RoleChanged
                                               && p.OldRole == "Student"
                                               && p.Role == "Supervisor"
                                               && p.ByUserId == "admin-1"),
                It.IsAny<CancellationToken>()), Times.Once);
            // They were a student of sup-1 and no longer are.
            _notifications.Verify(n => n.NotifySupervisorStudentsChangedAsync(
                "sup-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // UpdateUserAsync
        // =====================================================================

        [Fact]
        public async Task UpdateUserAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            ActAs("admin-1");

            var act = () => _sut.UpdateUserAsync("ghost", UpdateRequestFor(MakeUser("ghost", "Ghost")));

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        // Authorization inside the service: a non-admin may only edit themselves.
        [Fact]
        public async Task UpdateUserAsync_WhenANonAdminUpdatesSomeoneElse_ThrowsForbidden()
        {
            var target = MakeUser("usr-1", "Target");
            var caller = MakeUser("stu-9", "Curious Student");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(target);
            _userManager.Setup(m => m.FindByIdAsync("stu-9")).ReturnsAsync(caller);
            SetRoles(caller, "Student");
            ActAs("stu-9");

            var act = () => _sut.UpdateUserAsync("usr-1", UpdateRequestFor(target));

            await act.Should().ThrowAsync<ForbiddenException>()
                     .WithMessage("You are not allowed to update other users");
            _userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenNewEmailBelongsToAnotherUser_ThrowsBadRequest()
        {
            var user = SelfUpdatingUser("usr-1", "Admin");
            var request = UpdateRequestFor(user);
            request.Email = "taken@brainova.test";
            _userManager.Setup(m => m.FindByEmailAsync("taken@brainova.test"))
                        .ReturnsAsync(MakeUser("usr-2", "Someone Else"));

            var act = () => _sut.UpdateUserAsync("usr-1", request);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Email already exists");
        }

        [Fact]
        public async Task UpdateUserAsync_WhenNewUserNameBelongsToAnotherUser_ThrowsBadRequest()
        {
            var user = SelfUpdatingUser("usr-1", "Admin");
            var request = UpdateRequestFor(user);
            request.UserName = "taken";
            _userManager.Setup(m => m.FindByNameAsync("taken")).ReturnsAsync(MakeUser("usr-2", "Someone Else"));

            var act = () => _sut.UpdateUserAsync("usr-1", request);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Username already exists");
        }

        [Fact]
        public async Task UpdateUserAsync_WhenANonStudentIsGivenASupervisor_ThrowsBadRequest()
        {
            var user = SelfUpdatingUser("usr-1", "Admin");
            var request = UpdateRequestFor(user);
            request.SupervisorUserId = "sup-1";

            var act = () => _sut.UpdateUserAsync("usr-1", request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Supervisor can only be assigned to students");
        }

        [Fact]
        public async Task UpdateUserAsync_WhenAStudentIsLeftWithoutSupervisor_ThrowsBadRequest()
        {
            var student = SelfUpdatingUser("stu-1", "Student", supervisorId: "sup-1");
            var request = UpdateRequestFor(student);
            request.SupervisorUserId = null;

            var act = () => _sut.UpdateUserAsync("stu-1", request);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Student must have a supervisor");
        }

        [Fact]
        public async Task UpdateUserAsync_WhenNothingChanged_ReturnsIsChangedFalse_AndDoesNotSave()
        {
            var user = SelfUpdatingUser("usr-1", "Admin");

            var result = await _sut.UpdateUserAsync("usr-1", UpdateRequestFor(user));

            result.IsChanged.Should().BeFalse();
            result.RoleName.Should().Be("Admin");
            _userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
            VerifyNoEmailSent();
            _notifications.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UpdateUserAsync_SelfUpdate_SavesTheChange_WithoutSendingAnEmail()
        {
            var user = SelfUpdatingUser("usr-1", "Admin");
            var request = UpdateRequestFor(user);
            request.FullName = "Brand New Name";
            _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            var result = await _sut.UpdateUserAsync("usr-1", request);

            result.IsChanged.Should().BeTrue();
            result.FullName.Should().Be("Brand New Name");
            user.FullName.Should().Be("Brand New Name");
            _userManager.Verify(m => m.UpdateAsync(user), Times.Once);

            // "Your account was updated by the administration" makes no sense
            // when you edited your own profile.
            VerifyNoEmailSent();
            _notifications.Verify(n => n.NotifyUserUpdatedAsync("usr-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUserAsync_ByAnAdmin_SavesTheChange_AndEmailsTheUser()
        {
            var target = MakeUser("usr-1", "Old Name");
            var admin = MakeUser("admin-1", "Admin One");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(target);
            _userManager.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(admin);
            SetRoles(target, "Supervisor");
            SetRoles(admin, "Admin");
            ActAs("admin-1");

            var request = UpdateRequestFor(target);
            request.FullName = "New Name";
            _userManager.Setup(m => m.UpdateAsync(target)).ReturnsAsync(IdentityResult.Success);

            var result = await _sut.UpdateUserAsync("usr-1", request);

            result.IsChanged.Should().BeTrue();
            _email.Verify(e => e.SendEmailAsync(
                target.Email!,
                "Brainova - Account Updated",
                It.Is<string>(body => body.Contains("Full Name changed from: Old Name to: New Name"))),
                Times.Once);
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.Updated && p.ByUserId == "admin-1"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenIdentityRejectsTheUpdate_ThrowsBadRequest()
        {
            var user = SelfUpdatingUser("usr-1", "Admin");
            var request = UpdateRequestFor(user);
            request.PhoneNumber = "0591111111";
            _userManager.Setup(m => m.UpdateAsync(user))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure" }));

            var act = () => _sut.UpdateUserAsync("usr-1", request);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Concurrency failure");
        }

        // =====================================================================
        // ResetUserPasswordAsync
        // =====================================================================

        [Fact]
        public async Task ResetUserPasswordAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            var act = () => _sut.ResetUserPasswordAsync("ghost", new ChangeUserPasswordRequest { NewPassword = "NewPassw0rd!" });

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        [Fact]
        public async Task ResetUserPasswordAsync_WhenNewPasswordEqualsTheOldOne_ThrowsBadRequest()
        {
            var user = MakeUser("usr-1", "User One");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.CheckPasswordAsync(user, "NewPassw0rd!")).ReturnsAsync(true);

            var act = () => _sut.ResetUserPasswordAsync("usr-1", new ChangeUserPasswordRequest { NewPassword = "NewPassw0rd!" });

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("New password must be different from the old password.");
            _userManager.Verify(m => m.ResetPasswordAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ResetUserPasswordAsync_WhenIdentityRejectsThePassword_ThrowsBadRequest_AndSendsNoEmail()
        {
            var user = MakeUser("usr-1", "User One");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.CheckPasswordAsync(user, "weak")).ReturnsAsync(false);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
            _userManager.Setup(m => m.ResetPasswordAsync(user, "reset-token", "weak"))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too short" }));

            var act = () => _sut.ResetUserPasswordAsync("usr-1", new ChangeUserPasswordRequest { NewPassword = "weak" });

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Password too short");
            VerifyNoEmailSent();
        }

        [Fact]
        public async Task ResetUserPasswordAsync_HappyPath_ResetsThePassword_AndEmailsTheUser()
        {
            var user = MakeUser("usr-1", "User One");
            _userManager.Setup(m => m.FindByIdAsync("usr-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.CheckPasswordAsync(user, "NewPassw0rd!")).ReturnsAsync(false);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
            _userManager.Setup(m => m.ResetPasswordAsync(user, "reset-token", "NewPassw0rd!"))
                        .ReturnsAsync(IdentityResult.Success);

            var message = await _sut.ResetUserPasswordAsync("usr-1", new ChangeUserPasswordRequest { NewPassword = "NewPassw0rd!" });

            message.Should().Be("Password reset and email sent.");
            _userManager.Verify(m => m.ResetPasswordAsync(user, "reset-token", "NewPassw0rd!"), Times.Once);
            _email.Verify(e => e.SendEmailAsync(user.Email!, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        // =====================================================================
        // DeleteUsersAsync (bulk)
        // =====================================================================

        [Fact]
        public async Task DeleteUsersAsync_WhenNoIdsAreGiven_ThrowsBadRequest()
        {
            var act = () => _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string>() });

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("No user ids provided");
        }

        [Fact]
        public async Task DeleteUsersAsync_WhenAUserDoesNotExist_ReportsItAsFailed()
        {
            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "ghost" } });

            result.Deleted.Should().BeEmpty();
            var failed = result.Failed.Should().ContainSingle().Subject;
            failed.Id.Should().Be("ghost");
            failed.Reason.Should().Be("User not found");
        }

        [Fact]
        public async Task DeleteUsersAsync_HappyPath_DeletesTheUser_AndNotifiesAdmins()
        {
            ActAs("admin-1");
            var admin = MakeUser("adm-2", "Admin Two");
            _userManager.Setup(m => m.FindByIdAsync("adm-2")).ReturnsAsync(admin);
            SetRoles(admin, "Admin");
            _userManager.Setup(m => m.DeleteAsync(admin)).ReturnsAsync(IdentityResult.Success);

            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "adm-2" } });

            result.Failed.Should().BeEmpty();
            result.Deleted.Should().ContainSingle().Which.Id.Should().Be("adm-2");
            _userManager.Verify(m => m.DeleteAsync(admin), Times.Once);
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.Deleted
                                               && p.UserId == "adm-2"
                                               && p.ByUserId == "admin-1"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteUsersAsync_WhenTheSameIdIsSentTwice_DeletesItOnce()
        {
            var admin = MakeUser("adm-2", "Admin Two");
            _userManager.Setup(m => m.FindByIdAsync("adm-2")).ReturnsAsync(admin);
            SetRoles(admin, "Admin");
            _userManager.Setup(m => m.DeleteAsync(admin)).ReturnsAsync(IdentityResult.Success);

            var result = await _sut.DeleteUsersAsync(
                new DeleteUsersRequest { UserIds = new List<string> { "adm-2", "adm-2" } });

            result.Deleted.Should().ContainSingle();
            _userManager.Verify(m => m.DeleteAsync(admin), Times.Once);
        }

        // Deleting a supervisor who still has students would orphan them.
        [Fact]
        public async Task DeleteUsersAsync_WhenSupervisorStillHasStudents_RefusesToDelete()
        {
            var supervisor = MakeUser("sup-1", "Dr. Supervisor");
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            _userManager.Setup(m => m.FindByIdAsync("sup-1")).ReturnsAsync(supervisor);
            SetRoles(supervisor, "Supervisor");
            SetAllUsers(supervisor, student);

            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "sup-1" } });

            result.Deleted.Should().BeEmpty();
            result.Failed.Should().ContainSingle()
                  .Which.Reason.Should().Be("students are assigned to this supervisor");
            _userManager.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        // Questions that students already answered are kept (orphaned) so old
        // reports stay readable; unanswered ones are deleted with the supervisor.
        [Fact]
        public async Task DeleteUsersAsync_ForASupervisor_OrphansAnsweredQuestions_AndRemovesUnansweredOnes()
        {
            var supervisor = MakeUser("sup-1", "Dr. Supervisor");
            _userManager.Setup(m => m.FindByIdAsync("sup-1")).ReturnsAsync(supervisor);
            SetRoles(supervisor, "Supervisor");
            SetAllUsers(supervisor);                       // no students
            _userManager.Setup(m => m.DeleteAsync(supervisor)).ReturnsAsync(IdentityResult.Success);

            var answered = new ReportQuestion { Id = Guid.NewGuid(), Code = "A", SupervisorId = "sup-1" };
            var unanswered = new ReportQuestion { Id = Guid.NewGuid(), Code = "B", SupervisorId = "sup-1" };
            var questionRepo = _uow.SetupRepo(new List<ReportQuestion> { answered, unanswered });
            _uow.SetupRepo(new List<ReportAnswer> { new() { QuestionId = answered.Id } });

            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "sup-1" } });

            result.Deleted.Should().ContainSingle().Which.Id.Should().Be("sup-1");

            answered.SupervisorId.Should().BeNull();
            questionRepo.Verify(r => r.Update(answered), Times.Once);
            questionRepo.Verify(r => r.Remove(unanswered), Times.Once);
            questionRepo.Verify(r => r.Remove(answered), Times.Never);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // A student's cases, reports and feedback reference the student row.
        [Fact]
        public async Task DeleteUsersAsync_WhenStudentHasMriCases_RefusesToDelete()
        {
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            _userManager.Setup(m => m.FindByIdAsync("stu-1")).ReturnsAsync(student);
            SetRoles(student, "Student");
            _uow.SetupRepo(new List<MriCase> { new() { StudentId = "stu-1" } });

            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "stu-1" } });

            result.Deleted.Should().BeEmpty();
            result.Failed.Should().ContainSingle()
                  .Which.Reason.Should().Be("student has MRI cases / related records");
            _userManager.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUsersAsync_WhenAStudentIsDeleted_AlsoNotifiesTheirSupervisor()
        {
            var student = MakeUser("stu-1", "Student One", supervisorId: "sup-1");
            _userManager.Setup(m => m.FindByIdAsync("stu-1")).ReturnsAsync(student);
            SetRoles(student, "Student");
            _uow.SetupRepo(new List<MriCase>());           // no cases → deletable
            _userManager.Setup(m => m.DeleteAsync(student)).ReturnsAsync(IdentityResult.Success);

            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "stu-1" } });

            result.Deleted.Should().ContainSingle();
            _notifications.Verify(n => n.NotifySupervisorStudentsChangedAsync(
                "sup-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteUsersAsync_WhenIdentityRefusesTheDelete_ReportsTheIdentityError()
        {
            var admin = MakeUser("adm-2", "Admin Two");
            _userManager.Setup(m => m.FindByIdAsync("adm-2")).ReturnsAsync(admin);
            SetRoles(admin, "Admin");
            _userManager.Setup(m => m.DeleteAsync(admin))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Cannot delete" }));

            var result = await _sut.DeleteUsersAsync(new DeleteUsersRequest { UserIds = new List<string> { "adm-2" } });

            result.Deleted.Should().BeEmpty();
            result.Failed.Should().ContainSingle().Which.Reason.Should().Be("Cannot delete");
        }

        // One bad row must not abort the rest of the batch.
        [Fact]
        public async Task DeleteUsersAsync_WhenOneDeleteThrows_ReportsItAndContinuesWithTheOthers()
        {
            var broken = MakeUser("adm-2", "Broken");
            var fine = MakeUser("adm-3", "Fine");
            _userManager.Setup(m => m.FindByIdAsync("adm-2")).ReturnsAsync(broken);
            _userManager.Setup(m => m.FindByIdAsync("adm-3")).ReturnsAsync(fine);
            SetRoles(broken, "Admin");
            SetRoles(fine, "Admin");
            _userManager.Setup(m => m.DeleteAsync(broken)).ThrowsAsync(new Exception("FK constraint"));
            _userManager.Setup(m => m.DeleteAsync(fine)).ReturnsAsync(IdentityResult.Success);

            var result = await _sut.DeleteUsersAsync(
                new DeleteUsersRequest { UserIds = new List<string> { "adm-2", "adm-3" } });

            result.Failed.Should().ContainSingle()
                  .Which.Reason.Should().Be("Delete failed because of related data");
            result.Deleted.Should().ContainSingle().Which.Id.Should().Be("adm-3");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static ApplicationUser MakeUser(string id, string fullName, string? supervisorId = null)
            => new()
            {
                Id = id,
                FullName = fullName,
                UserName = id,
                Email = id + "@brainova.test",
                PhoneNumber = "0590000000",
                SupervisorUserId = supervisorId
            };

        private void SetRoles(ApplicationUser user, params string[] roles)
            => _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles.ToList());

        /// <summary>Makes _userManager.Users (the whole users table) serve these users.</summary>
        private void SetAllUsers(params ApplicationUser[] users)
            => _userManager.Setup(m => m.Users).Returns(users.ToList().BuildMock());

        /// <summary>Puts a logged-in user with this id on the fake HTTP context.</summary>
        private void ActAs(string userId)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("Id", userId) }, "test"))
            };
            _http.Setup(a => a.HttpContext).Returns(context);
        }

        /// <summary>A user who is logged in and editing their OWN profile.</summary>
        private ApplicationUser SelfUpdatingUser(string id, string role, string? supervisorId = null)
        {
            var user = MakeUser(id, "Original Name", supervisorId);
            _userManager.Setup(m => m.FindByIdAsync(id)).ReturnsAsync(user);
            SetRoles(user, role);
            ActAs(id);
            return user;
        }

        /// <summary>An update request that changes nothing — tests then modify one field.</summary>
        private static UpdateUserRequest UpdateRequestFor(ApplicationUser user) => new()
        {
            FullName = user.FullName,
            Email = user.Email!,
            UserName = user.UserName!,
            PhoneNumber = user.PhoneNumber!,
            SupervisorUserId = user.SupervisorUserId
        };

        private static CreateUserRequest NewUserRequest(string? supervisorId = null) => new()
        {
            FullName = "New User",
            Email = "new.user@brainova.test",
            UserName = "newuser",
            PhoneNumber = "0590000000",
            SupervisorUserId = supervisorId
        };

        /// <summary>The repository "creates" whatever user it is given and reports success.</summary>
        private void RepositoryCreatesUsersWithRole(string roleName)
            => _userRepository.Setup(r => r.CreateUserWithRoleAsync(It.IsAny<ApplicationUser>(), roleName))
                .Returns((ApplicationUser u, string _) =>
                    Task.FromResult<(bool Success, string Message, ApplicationUser? User)>((true, "created", u)));

        private void VerifyNoUserWasCreated()
            => _userRepository.Verify(r => r.CreateUserWithRoleAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);

        private void VerifyNoEmailSent()
            => _email.Verify(e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.Realtime;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// AuthenticationService: login, current user, registration, email
    /// confirmation and the password flows.
    ///
    /// Everything it depends on is mocked: UserManager and SignInManager
    /// (ASP.NET Identity), the email sender and the notification service.
    /// IConfiguration is NOT mocked — it is a real in-memory configuration
    /// holding a throw-away JWT key, because building one is easier than
    /// mocking GetSection(...)["..."].
    /// </summary>
    public class AuthenticationServiceTests
    {
        private const string Password = "Passw0rd!";

        private readonly Mock<UserManager<ApplicationUser>> _userManager = IdentityMocks.CreateUserManager();
        private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
        private readonly Mock<IEmailSender> _email = new();
        private readonly Mock<INotificationService> _notifications = new();
        private readonly AuthenticationService _sut;

        public AuthenticationServiceTests()
        {
            _signInManager = IdentityMocks.CreateSignInManager(_userManager);
            _sut = CreateSut(ValidJwtConfig());
        }

        // =====================================================================
        // LoginAsync
        // =====================================================================

        [Fact]
        public async Task LoginAsync_WhenUserDoesNotExist_ThrowsBadRequest()
        {
            // No setup: FindByEmailAsync and FindByNameAsync both return null.
            var act = () => _sut.LoginAsync(Login("ghost@brainova.test"));

            // Same message as a wrong password — the API must not reveal
            // whether an account exists.
            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Invalid email/username or password");
        }

        [Fact]
        public async Task LoginAsync_WhenUserIsBlocked_ThrowsForbidden_WithoutCheckingThePassword()
        {
            var user = ExistingUser("blocked@brainova.test");
            user.IsBlocked = true;

            var act = () => _sut.LoginAsync(Login(user.Email!));

            await act.Should().ThrowAsync<ForbiddenException>().WithMessage("Your account is blocked");
            _signInManager.Verify(s => s.CheckPasswordSignInAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsWrong_ThrowsBadRequest()
        {
            var user = ExistingUser("a@brainova.test");
            PasswordCheckReturns(user, IdentitySignInResult.Failed);

            var act = () => _sut.LoginAsync(Login(user.Email!));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Invalid email/username or password");
        }

        [Fact]
        public async Task LoginAsync_WhenAccountIsLockedOut_ThrowsForbidden()
        {
            var user = ExistingUser("a@brainova.test");
            PasswordCheckReturns(user, IdentitySignInResult.LockedOut);

            var act = () => _sut.LoginAsync(Login(user.Email!));

            await act.Should().ThrowAsync<ForbiddenException>().WithMessage("Your account is locked");
        }

        [Fact]
        public async Task LoginAsync_WhenEmailIsNotConfirmed_ThrowsBadRequest()
        {
            var user = ExistingUser("a@brainova.test");
            PasswordCheckReturns(user, IdentitySignInResult.NotAllowed);

            var act = () => _sut.LoginAsync(Login(user.Email!));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Please confirm your email");
        }

        // Wrong passwords must count towards lockout: the third argument of
        // CheckPasswordSignInAsync (lockoutOnFailure) has to be true.
        [Fact]
        public async Task LoginAsync_ChecksThePasswordWithLockoutEnabled()
        {
            var user = ExistingUser("a@brainova.test", "Student");
            PasswordCheckReturns(user, IdentitySignInResult.Success);

            await _sut.LoginAsync(Login(user.Email!));

            _signInManager.Verify(s => s.CheckPasswordSignInAsync(user, Password, true), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_OnSuccess_ReturnsAJwtCarryingTheUserIdAndRoles()
        {
            var user = ExistingUser("admin@brainova.test", "Admin", "SuperAdmin");
            PasswordCheckReturns(user, IdentitySignInResult.Success);

            var result = await _sut.LoginAsync(Login(user.Email!));

            // Decode the token (no signature validation needed here) and look at its claims.
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

            jwt.Issuer.Should().Be("brainova-tests");
            jwt.Claims.Should().Contain(c => c.Type == "Id" && c.Value == user.Id);
            jwt.Claims.Should().Contain(c => c.Type == "Email" && c.Value == "admin@brainova.test");
            jwt.Claims.Where(c => c.Type == "Role").Select(c => c.Value)
               .Should().BeEquivalentTo(new[] { "Admin", "SuperAdmin" });
            jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task LoginAsync_WhenEmailLookupFindsNothing_FallsBackToUserName()
        {
            var user = new ApplicationUser { UserName = "abood", Email = "abood@brainova.test", FullName = "Abood" };
            _userManager.Setup(m => m.FindByEmailAsync("abood")).ReturnsAsync((ApplicationUser?)null);
            _userManager.Setup(m => m.FindByNameAsync("abood")).ReturnsAsync(user);
            _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Student" });
            PasswordCheckReturns(user, IdentitySignInResult.Success);

            var result = await _sut.LoginAsync(Login("abood"));

            result.Token.Should().NotBeNullOrWhiteSpace();
        }

        // Fail-fast from the secrets hardening: no JWT key → a clear error,
        // never a token signed with an empty key.
        [Fact]
        public async Task LoginAsync_WhenJwtKeyIsNotConfigured_ThrowsInvalidOperation()
        {
            var sut = CreateSut(new Dictionary<string, string?>());   // empty configuration
            var user = ExistingUser("a@brainova.test", "Student");
            PasswordCheckReturns(user, IdentitySignInResult.Success);

            var act = () => sut.LoginAsync(Login(user.Email!));

            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("*jwtOptions:SecretKey*");
        }

        // =====================================================================
        // LoginForCookieAsync
        // =====================================================================

        [Fact]
        public async Task LoginForCookieAsync_ReturnsTokenUserInfoAndExpiry()
        {
            var supervisor = new ApplicationUser { Id = "sup-1", FullName = "Dr. Supervisor" };
            var user = ExistingUser("student@brainova.test", "Student");
            user.SupervisorUserId = supervisor.Id;
            _userManager.Setup(m => m.FindByIdAsync(supervisor.Id)).ReturnsAsync(supervisor);
            PasswordCheckReturns(user, IdentitySignInResult.Success);

            var (token, current, expiresUtc) = await _sut.LoginForCookieAsync(Login(user.Email!));

            token.Should().NotBeNullOrWhiteSpace();
            current.Id.Should().Be(user.Id);
            current.Email.Should().Be("student@brainova.test");
            current.Roles.Should().Equal("Student");
            current.SupervisorUserId.Should().Be("sup-1");
            current.SupervisorFullName.Should().Be("Dr. Supervisor");

            // DurationInMinutes = 30 in the test configuration.
            expiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        }

        // =====================================================================
        // GetCurrentUserAsync
        // =====================================================================

        [Fact]
        public async Task GetCurrentUserAsync_WhenPrincipalHasNoIdClaim_ThrowsUnauthorized()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

            var act = () => _sut.GetCurrentUserAsync(anonymous);

            await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Not authenticated");
        }

        [Fact]
        public async Task GetCurrentUserAsync_WhenUserNoLongerExists_ThrowsNotFound()
        {
            _userManager.Setup(m => m.FindByIdAsync("deleted-user")).ReturnsAsync((ApplicationUser?)null);

            var act = () => _sut.GetCurrentUserAsync(PrincipalWithId("deleted-user"));

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        // A valid token must stop working the moment an admin blocks the user.
        [Fact]
        public async Task GetCurrentUserAsync_WhenUserIsBlocked_ThrowsForbidden()
        {
            var user = new ApplicationUser { Id = "u-1", FullName = "X", IsBlocked = true };
            _userManager.Setup(m => m.FindByIdAsync("u-1")).ReturnsAsync(user);

            var act = () => _sut.GetCurrentUserAsync(PrincipalWithId("u-1"));

            await act.Should().ThrowAsync<ForbiddenException>().WithMessage("Your account is blocked");
        }

        [Fact]
        public async Task GetCurrentUserAsync_ReturnsTheUsersCurrentInfo()
        {
            var user = new ApplicationUser
            {
                Id = "u-1",
                UserName = "abood",
                FullName = "Abood",
                Email = "abood@brainova.test",
                PhoneNumber = "0590000000"
            };
            _userManager.Setup(m => m.FindByIdAsync("u-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Supervisor" });

            var result = await _sut.GetCurrentUserAsync(PrincipalWithId("u-1"));

            result.Id.Should().Be("u-1");
            result.UserName.Should().Be("abood");
            result.FullName.Should().Be("Abood");
            result.PhoneNumber.Should().Be("0590000000");
            result.Roles.Should().Equal("Supervisor");
            result.SupervisorUserId.Should().BeNull();
            result.SupervisorFullName.Should().BeNull();
        }

        // =====================================================================
        // RegisterStudentAsync
        // =====================================================================

        [Fact]
        public async Task RegisterStudentAsync_WhenEmailAlreadyExists_ThrowsBadRequest()
        {
            var request = RegisterRequest();
            _userManager.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(new ApplicationUser());

            var act = () => _sut.RegisterStudentAsync(request, null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Email already exists");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task RegisterStudentAsync_WhenUserNameAlreadyExists_ThrowsBadRequest()
        {
            var request = RegisterRequest();
            _userManager.Setup(m => m.FindByNameAsync(request.UserName)).ReturnsAsync(new ApplicationUser());

            var act = () => _sut.RegisterStudentAsync(request, null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Username already exists");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task RegisterStudentAsync_WhenSupervisorDoesNotExist_ThrowsNotFound()
        {
            var request = RegisterRequest();
            _userManager.Setup(m => m.FindByIdAsync(request.SupervisorUserId)).ReturnsAsync((ApplicationUser?)null);

            var act = () => _sut.RegisterStudentAsync(request, null!);

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Supervisor not found");
            VerifyNoUserWasCreated();
        }

        // The chosen "supervisor" must really have the Supervisor role —
        // otherwise a student could attach themselves to an admin or another student.
        [Fact]
        public async Task RegisterStudentAsync_WhenChosenUserIsNotASupervisor_ThrowsBadRequest()
        {
            var request = RegisterRequest();
            var notASupervisor = new ApplicationUser { Id = request.SupervisorUserId, FullName = "Another Student" };
            _userManager.Setup(m => m.FindByIdAsync(request.SupervisorUserId)).ReturnsAsync(notASupervisor);
            _userManager.Setup(m => m.GetRolesAsync(notASupervisor)).ReturnsAsync(new List<string> { "Student" });

            var act = () => _sut.RegisterStudentAsync(request, null!);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid supervisor");
            VerifyNoUserWasCreated();
        }

        [Fact]
        public async Task RegisterStudentAsync_WhenIdentityRejectsTheUser_ThrowsBadRequestWithAllErrors()
        {
            var request = RegisterRequest();
            SetupValidSupervisor(request.SupervisorUserId);
            _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
                        .ReturnsAsync(IdentityResult.Failed(
                            new IdentityError { Description = "Password too weak" },
                            new IdentityError { Description = "Password needs a digit" }));

            var act = () => _sut.RegisterStudentAsync(request, null!);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Password too weak;Password needs a digit");
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task RegisterStudentAsync_HappyPath_CreatesAnUnconfirmedStudent_SendsEmail_AndNotifies()
        {
            var request = RegisterRequest();
            SetupValidSupervisor(request.SupervisorUserId);

            ApplicationUser? created = null;
            _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
                        .Callback((ApplicationUser u, string _) => created = u)
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Student"))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
                        .ReturnsAsync("confirm-token");

            var message = await _sut.RegisterStudentAsync(request, null!);

            message.Should().Be("Registration successful. Please check your email to confirm your account.");

            // The user that was created
            created.Should().NotBeNull();
            created!.Email.Should().Be(request.Email);
            created.UserName.Should().Be(request.UserName);
            created.SupervisorUserId.Should().Be(request.SupervisorUserId);
            created.EmailConfirmed.Should().BeFalse();
            created.IsBlocked.Should().BeFalse();

            // …was put in the Student role
            _userManager.Verify(m => m.AddToRoleAsync(created, "Student"), Times.Once);

            // …got a confirmation email with a link
            _email.Verify(e => e.SendEmailAsync(
                request.Email,
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("confirm-email") && body.Contains("confirm-token"))),
                Times.Once);

            // …and admins + the supervisor were told in real time
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.Created
                                               && p.Role == "Student"
                                               && p.ByUserId == null),
                It.IsAny<CancellationToken>()), Times.Once);
            _notifications.Verify(n => n.NotifySupervisorStudentsChangedAsync(
                request.SupervisorUserId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterStudentAsync_WhenNotificationThrows_StillSucceeds()
        {
            var request = RegisterRequest();
            SetupValidSupervisor(request.SupervisorUserId);
            _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Student"))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
                        .ReturnsAsync("confirm-token");
            _notifications.Setup(n => n.NotifyAdminsUserListChangedAsync(
                    It.IsAny<UserListChangePayload>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR is down"));

            var message = await _sut.RegisterStudentAsync(request, null!);

            message.Should().StartWith("Registration successful");
        }

        // =====================================================================
        // ConfirmEmailAsync
        // =====================================================================

        [Fact]
        public async Task ConfirmEmailAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            var act = () => _sut.ConfirmEmailAsync("token", "ghost");

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        [Fact]
        public async Task ConfirmEmailAsync_WhenTokenIsRejected_ThrowsBadRequest()
        {
            var user = new ApplicationUser { Id = "u-1", FullName = "X" };
            _userManager.Setup(m => m.FindByIdAsync("u-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.ConfirmEmailAsync(user, "bad-token"))
                        .ReturnsAsync(IdentityResult.Failed());

            var act = () => _sut.ConfirmEmailAsync("bad-token", "u-1");

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Email confirmation failed");
        }

        // The link in the email carries a URL-escaped token; it must be
        // un-escaped before it is handed to Identity.
        [Fact]
        public async Task ConfirmEmailAsync_UnescapesTheToken_AndNotifiesAdmins()
        {
            var user = new ApplicationUser { Id = "u-1", FullName = "X", UserName = "x" };
            _userManager.Setup(m => m.FindByIdAsync("u-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.ConfirmEmailAsync(user, "abc+/=")).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Student" });

            var message = await _sut.ConfirmEmailAsync(Uri.EscapeDataString("abc+/="), "u-1");

            message.Should().Be("Email confirmed successfully");
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.EmailConfirmed
                                               && p.UserId == "u-1"
                                               && p.Role == "Student"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // ForgotPasswordAsync
        // =====================================================================

        [Fact]
        public async Task ForgotPasswordAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            var act = () => _sut.ForgotPasswordAsync(new ForgetPasswordRequest { Email = "ghost@brainova.test" });

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        [Fact]
        public async Task ForgotPasswordAsync_StoresA4DigitCodeValidFor15Minutes_AndEmailsIt()
        {
            var user = ExistingUser("a@brainova.test");
            _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            var ok = await _sut.ForgotPasswordAsync(new ForgetPasswordRequest { Email = user.Email! });

            ok.Should().BeTrue();
            user.CodeResetPassword.Should().MatchRegex("^[0-9]{4}$");
            user.CodeResetPasswordExpire.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));

            _userManager.Verify(m => m.UpdateAsync(user), Times.Once);
            _email.Verify(e => e.SendEmailAsync(
                user.Email!,
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains(user.CodeResetPassword!))), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenSavingTheCodeFails_ThrowsAndSendsNoEmail()
        {
            var user = ExistingUser("a@brainova.test");
            _userManager.Setup(m => m.UpdateAsync(user))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "db error" }));

            var act = () => _sut.ForgotPasswordAsync(new ForgetPasswordRequest { Email = user.Email! });

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("db error");
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // =====================================================================
        // ResetPasswordAsync
        // =====================================================================

        [Fact]
        public async Task ResetPasswordAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            var act = () => _sut.ResetPasswordAsync(ResetRequest("ghost@brainova.test", "1234"));

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenCodeIsWrong_ThrowsBadRequest()
        {
            var user = UserWithResetCode("1234", DateTime.UtcNow.AddMinutes(10));

            var act = () => _sut.ResetPasswordAsync(ResetRequest(user.Email!, "9999"));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid reset code");
            VerifyPasswordWasNotChanged();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenCodeHasExpired_ThrowsBadRequest()
        {
            var user = UserWithResetCode("1234", DateTime.UtcNow.AddMinutes(-1));

            var act = () => _sut.ResetPasswordAsync(ResetRequest(user.Email!, "1234"));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Reset code expired");
            VerifyPasswordWasNotChanged();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenNewPasswordEqualsTheOldOne_ThrowsBadRequest()
        {
            var user = UserWithResetCode("1234", DateTime.UtcNow.AddMinutes(10));
            _userManager.Setup(m => m.CheckPasswordAsync(user, Password)).ReturnsAsync(true);

            var act = () => _sut.ResetPasswordAsync(ResetRequest(user.Email!, "1234"));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("New password must be different from the old password.");
            VerifyPasswordWasNotChanged();
        }

        [Fact]
        public async Task ResetPasswordAsync_HappyPath_ChangesPassword_ClearsTheCode_AndEmails()
        {
            var user = UserWithResetCode("1234", DateTime.UtcNow.AddMinutes(10));
            _userManager.Setup(m => m.CheckPasswordAsync(user, Password)).ReturnsAsync(false);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
            _userManager.Setup(m => m.ResetPasswordAsync(user, "reset-token", Password))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            var ok = await _sut.ResetPasswordAsync(ResetRequest(user.Email!, "1234"));

            ok.Should().BeTrue();
            _userManager.Verify(m => m.ResetPasswordAsync(user, "reset-token", Password), Times.Once);

            // The code is single-use: it must be wiped after a successful reset.
            user.CodeResetPassword.Should().BeNull();
            user.CodeResetPasswordExpire.Should().BeNull();

            _email.Verify(e => e.SendEmailAsync(user.Email!, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenIdentityRejectsTheNewPassword_ThrowsAndKeepsTheCode()
        {
            var user = UserWithResetCode("1234", DateTime.UtcNow.AddMinutes(10));
            _userManager.Setup(m => m.CheckPasswordAsync(user, Password)).ReturnsAsync(false);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
            _userManager.Setup(m => m.ResetPasswordAsync(user, "reset-token", Password))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too short" }));

            var act = () => _sut.ResetPasswordAsync(ResetRequest(user.Email!, "1234"));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Password too short");
            user.CodeResetPassword.Should().Be("1234");
        }

        // =====================================================================
        // SetPasswordAsync (link sent to users created by an admin)
        // =====================================================================

        [Fact]
        public async Task SetPasswordAsync_WhenUserDoesNotExist_ThrowsNotFound()
        {
            var act = () => _sut.SetPasswordAsync(SetRequest("ghost", "token"));

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found");
        }

        [Fact]
        public async Task SetPasswordAsync_WhenTokenIsRejected_ThrowsBadRequest()
        {
            var user = new ApplicationUser { Id = "u-1", Email = "u1@brainova.test", FullName = "X" };
            _userManager.Setup(m => m.FindByIdAsync("u-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.ResetPasswordAsync(user, "bad-token", Password))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));

            var act = () => _sut.SetPasswordAsync(SetRequest("u-1", "bad-token"));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid token.");
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SetPasswordAsync_HappyPath_SetsPassword_ConfirmsEmail_AndNotifiesAdmins()
        {
            var user = new ApplicationUser { Id = "u-1", Email = "u1@brainova.test", FullName = "X", UserName = "x" };
            _userManager.Setup(m => m.FindByIdAsync("u-1")).ReturnsAsync(user);
            _userManager.Setup(m => m.ResetPasswordAsync(user, "set-token", Password))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("email-token");
            _userManager.Setup(m => m.ConfirmEmailAsync(user, "email-token")).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Supervisor" });

            var message = await _sut.SetPasswordAsync(SetRequest("u-1", "set-token"));

            message.Should().Be("Password set successfully.");
            _userManager.Verify(m => m.ConfirmEmailAsync(user, "email-token"), Times.Once);
            _email.Verify(e => e.SendEmailAsync("u1@brainova.test", It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _notifications.Verify(n => n.NotifyAdminsUserListChangedAsync(
                It.Is<UserListChangePayload>(p => p.Kind == UserListChangeKinds.EmailConfirmed
                                               && p.Role == "Supervisor"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private AuthenticationService CreateSut(Dictionary<string, string?> settings)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            return new AuthenticationService(
                _userManager.Object,
                configuration,
                _email.Object,
                _signInManager.Object,
                _notifications.Object);
        }

        /// <summary>A throw-away signing key (64 bytes; HMAC-SHA256 needs at least 32).</summary>
        private static Dictionary<string, string?> ValidJwtConfig() => new()
        {
            ["jwtOptions:SecretKey"] = Convert.ToBase64String(Enumerable.Range(1, 64).Select(i => (byte)i).ToArray()),
            ["jwtOptions:Issuer"] = "brainova-tests",
            ["jwtOptions:Audience"] = "brainova-tests-clients",
            ["jwtOptions:DurationInMinutes"] = "30"
        };

        /// <summary>Registers a user in the mocked UserManager so login can find them by email.</summary>
        private ApplicationUser ExistingUser(string email, params string[] roles)
        {
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email.Split('@')[0],
                FullName = "Test User"
            };

            _userManager.Setup(m => m.FindByEmailAsync(email)).ReturnsAsync(user);
            _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles.ToList());
            return user;
        }

        private ApplicationUser UserWithResetCode(string code, DateTime expiresUtc)
        {
            var user = ExistingUser("reset@brainova.test");
            user.CodeResetPassword = code;
            user.CodeResetPasswordExpire = expiresUtc;
            return user;
        }

        private void PasswordCheckReturns(ApplicationUser user, IdentitySignInResult result)
            => _signInManager.Setup(s => s.CheckPasswordSignInAsync(user, Password, true)).ReturnsAsync(result);

        private void SetupValidSupervisor(string supervisorId)
        {
            var supervisor = new ApplicationUser { Id = supervisorId, FullName = "Dr. Supervisor" };
            _userManager.Setup(m => m.FindByIdAsync(supervisorId)).ReturnsAsync(supervisor);
            _userManager.Setup(m => m.GetRolesAsync(supervisor)).ReturnsAsync(new List<string> { "Supervisor" });
        }

        private void VerifyNoUserWasCreated()
            => _userManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);

        private void VerifyPasswordWasNotChanged()
            => _userManager.Verify(m => m.ResetPasswordAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        private static ClaimsPrincipal PrincipalWithId(string userId)
            => new(new ClaimsIdentity(new[] { new Claim("Id", userId) }, "test"));

        private static LoginRequest Login(string emailOrUserName)
            => new() { EmailOrUserName = emailOrUserName, Password = Password };

        private static RegisterStudentRequest RegisterRequest() => new()
        {
            FullName = "New Student",
            Email = "new.student@brainova.test",
            UserName = "newstudent",
            PhoneNumber = "0590000000",
            Password = Password,
            SupervisorUserId = "sup-1"
        };

        private static ResetPasswordRequest ResetRequest(string email, string code)
            => new() { Email = email, Code = code, NewPassword = Password };

        private static SetPasswordRequest SetRequest(string userId, string token)
            => new() { UserId = userId, Token = token, NewPassword = Password };
    }
}

using Brainova.BLL.DTOs.Realtime;
using Brainova.BLL.DTOs.Response.Report;
using Brainova.BLL.Hubs;
using Brainova.BLL.Services.Classes;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// NotificationService pushes SignalR events. What matters is:
    ///   1. WHO receives it   (a user id, several user ids, or role groups)
    ///   2. WHICH event name  (the frontend listens for these exact strings)
    ///   3. that blank ids send nothing
    ///
    /// NEW CONCEPT: in the service you write  Clients.User(id).SendAsync("X", payload).
    /// SendAsync is an EXTENSION method, and Moq cannot mock extension
    /// methods. Under the hood it calls the real interface method
    ///     IClientProxy.SendCoreAsync(string method, object?[] args, CancellationToken ct)
    /// so that is the call the tests verify.
    /// </summary>
    public class NotificationServiceTests
    {
        private readonly Mock<IHubContext<NotificationHub>> _hub = new();
        private readonly Mock<IHubClients> _clients = new();
        private readonly Mock<IClientProxy> _proxy = new();
        private readonly NotificationService _sut;

        // Filled in by the mock setups below so tests can see the recipients.
        private string? _targetUser;
        private IReadOnlyList<string>? _targetUsers;
        private IReadOnlyList<string>? _targetGroups;

        public NotificationServiceTests()
        {
            _hub.Setup(h => h.Clients).Returns(_clients.Object);

            _clients.Setup(c => c.User(It.IsAny<string>()))
                    .Callback((string userId) => _targetUser = userId)
                    .Returns(_proxy.Object);

            _clients.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>()))
                    .Callback((IReadOnlyList<string> userIds) => _targetUsers = userIds)
                    .Returns(_proxy.Object);

            _clients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>()))
                    .Callback((IReadOnlyList<string> groups) => _targetGroups = groups)
                    .Returns(_proxy.Object);

            _sut = new NotificationService(_hub.Object);
        }

        // ------------------------- single-user pushes -------------------------

        [Fact]
        public async Task NotifySupervisorNewReportAsync_SendsNewReportToThatSupervisor()
        {
            await _sut.NotifySupervisorNewReportAsync("sup-1", new SupervisorNewReportResponse());

            _targetUser.Should().Be("sup-1");
            VerifySent("NewReport");
        }

        [Fact]
        public async Task NotifyStudentUnseenCountAsync_SendsUnseenCountChangedToThatStudent()
        {
            await _sut.NotifyStudentUnseenCountAsync("stu-1", 3);

            _targetUser.Should().Be("stu-1");
            VerifySent("UnseenCountChanged");
        }

        [Fact]
        public async Task NotifyUserBlockedAsync_SendsAccountBlocked()
        {
            await _sut.NotifyUserBlockedAsync("user-1");

            _targetUser.Should().Be("user-1");
            VerifySent("AccountBlocked");
        }

        [Fact]
        public async Task NotifyUserUnblockedAsync_SendsAccountUnblocked()
        {
            await _sut.NotifyUserUnblockedAsync("user-1");

            VerifySent("AccountUnblocked");
        }

        [Fact]
        public async Task NotifyUserRoleChangedAsync_SendsRoleChanged()
        {
            await _sut.NotifyUserRoleChangedAsync("user-1", "Student", "Supervisor");

            _targetUser.Should().Be("user-1");
            VerifySent("RoleChanged");
        }

        [Fact]
        public async Task NotifyUserUpdatedAsync_SendsUserUpdated()
        {
            await _sut.NotifyUserUpdatedAsync("user-1");

            VerifySent("UserUpdated");
        }

        [Fact]
        public async Task NotifySupervisorStudentsChangedAsync_SendsStudentsChanged()
        {
            await _sut.NotifySupervisorStudentsChangedAsync("sup-1");

            _targetUser.Should().Be("sup-1");
            VerifySent("StudentsChanged");
        }

        [Fact]
        public async Task NotifyStudentFeedbackDeletedAsync_SendsFeedbackDeleted()
        {
            await _sut.NotifyStudentFeedbackDeletedAsync("stu-1", Guid.NewGuid(), Guid.NewGuid(), 0);

            _targetUser.Should().Be("stu-1");
            VerifySent("FeedbackDeleted");
        }

        [Fact]
        public async Task NotifySupervisorFeedbackSeenAsync_SendsFeedbackSeenByStudent()
        {
            await _sut.NotifySupervisorFeedbackSeenAsync("sup-1", Guid.NewGuid(), Guid.NewGuid());

            VerifySent("FeedbackSeenByStudent");
        }

        // A blank recipient must be a silent no-op, never an exception:
        // callers wrap notifications in "best-effort" code paths.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SingleUserPushes_WhenRecipientIdIsBlank_SendNothing(string? id)
        {
            await _sut.NotifySupervisorNewReportAsync(id!, new SupervisorNewReportResponse());
            await _sut.NotifyStudentUnseenCountAsync(id!, 1);
            await _sut.NotifyUserBlockedAsync(id!);
            await _sut.NotifyUserUnblockedAsync(id!);
            await _sut.NotifyUserRoleChangedAsync(id!, "Student", "Admin");
            await _sut.NotifyUserUpdatedAsync(id!);
            await _sut.NotifySupervisorStudentsChangedAsync(id!);

            VerifyNothingSent();
        }

        // ------------------------- multi-user pushes --------------------------

        [Fact]
        public async Task NotifyStudentsQuestionsChangedAsync_SendsOnceToDistinctNonBlankIds()
        {
            await _sut.NotifyStudentsQuestionsChangedAsync(new[] { "stu-1", "", "stu-2", "stu-1", "  " });

            _targetUsers.Should().Equal("stu-1", "stu-2");
            VerifySent("QuestionsChanged");
        }

        [Fact]
        public async Task NotifyStudentsQuestionsChangedAsync_WhenListIsEmptyOrNull_SendsNothing()
        {
            await _sut.NotifyStudentsQuestionsChangedAsync(Array.Empty<string>());
            await _sut.NotifyStudentsQuestionsChangedAsync(new[] { "", "  " });
            await _sut.NotifyStudentsQuestionsChangedAsync(null!);

            VerifyNothingSent();
        }

        // --------------------------- role-group push --------------------------

        [Fact]
        public async Task NotifyAdminsUserListChangedAsync_SendsThePayloadToAdminAndSuperAdminGroups()
        {
            var payload = new UserListChangePayload { Kind = UserListChangeKinds.Created, UserId = "u-1" };

            await _sut.NotifyAdminsUserListChangedAsync(payload);

            _targetGroups.Should().BeEquivalentTo(new[] { "role:Admin", "role:SuperAdmin" });

            // The payload object itself is the single argument of the event.
            _proxy.Verify(p => p.SendCoreAsync(
                "UserListChanged",
                It.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], payload)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task NotifyAdminsUserListChangedAsync_WhenPayloadIsNull_SendsNothing()
        {
            await _sut.NotifyAdminsUserListChangedAsync(null!);

            VerifyNothingSent();
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private void VerifySent(string eventName)
            => _proxy.Verify(p => p.SendCoreAsync(
                eventName,
                It.Is<object?[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()), Times.Once);

        private void VerifyNothingSent()
            => _proxy.Verify(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}

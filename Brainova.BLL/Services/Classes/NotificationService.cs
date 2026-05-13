using Brainova.BLL.DTOs.Response;
using Brainova.BLL.DTOs.Response.Report;
using Brainova.BLL.Hubs;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.SignalR;

namespace Brainova.BLL.Services.Classes
{
    /// <summary>
    /// SignalR-backed implementation of <see cref="INotificationService"/>.
    /// Uses <see cref="IHubContext{THub}"/> to push messages from outside the hub
    /// (i.e. from any service or controller).
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationService(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public Task NotifyStudentNewFeedbackAsync(
            string studentId,
            StudentFeedbackNotificationResponse payload,
            int unseenCount,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return Task.CompletedTask;

            // Clients.User uses the IUserIdProvider (JwtIdUserIdProvider) to
            // route to every connection opened by that student (multi-tab/multi-device).
            return _hub.Clients.User(studentId).SendAsync(
                "NewFeedback",
                new
                {
                    feedback = payload,
                    unseenCount
                },
                ct);
        }

        public Task NotifyStudentUnseenCountAsync(
            string studentId,
            int unseenCount,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return Task.CompletedTask;

            return _hub.Clients.User(studentId).SendAsync(
                "UnseenCountChanged",
                new { unseenCount },
                ct);
        }

        public Task NotifySupervisorNewReportAsync(
            string supervisorId,
            SupervisorNewReportResponse payload,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(supervisorId)) return Task.CompletedTask;

            return _hub.Clients.User(supervisorId).SendAsync(
                "NewReport",
                new { report = payload },
                ct);
        }

        public Task NotifyStudentFeedbackUpdatedAsync(
            string studentId,
            StudentFeedbackNotificationResponse payload,
            int unseenCount,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return Task.CompletedTask;

            return _hub.Clients.User(studentId).SendAsync(
                "FeedbackUpdated",
                new
                {
                    feedback = payload,
                    unseenCount
                },
                ct);
        }

        public Task NotifyStudentFeedbackDeletedAsync(
            string studentId,
            Guid feedbackId,
            Guid reportId,
            int unseenCount,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return Task.CompletedTask;

            return _hub.Clients.User(studentId).SendAsync(
                "FeedbackDeleted",
                new
                {
                    feedbackId,
                    reportId,
                    unseenCount
                },
                ct);
        }

        public Task NotifyUserBlockedAsync(
            string userId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;

            return _hub.Clients.User(userId).SendAsync(
                "AccountBlocked",
                new { message = "Your account has been blocked." },
                ct);
        }
    }
}

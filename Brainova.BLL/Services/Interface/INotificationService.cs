using Brainova.BLL.DTOs.Response;
using Brainova.BLL.DTOs.Response.Report;

namespace Brainova.BLL.Services.Interface
{
    /// <summary>
    /// Abstraction over real-time pushes (SignalR).
    /// Callers (e.g. FeedbackService) depend on this interface, not on SignalR directly.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Push a "you got a new feedback" event to a single student in real time.
        /// The client listens for the event named <c>"NewFeedback"</c>.
        /// </summary>
        Task NotifyStudentNewFeedbackAsync(
            string studentId,
            StudentFeedbackNotificationResponse payload,
            int unseenCount,
            CancellationToken ct = default);

        /// <summary>
        /// Push the latest unseen feedback count so the bell-badge updates live
        /// (e.g. after the student marks one as seen on another device).
        /// Client event name: <c>"UnseenCountChanged"</c>.
        /// </summary>
        Task NotifyStudentUnseenCountAsync(
            string studentId,
            int unseenCount,
            CancellationToken ct = default);

        /// <summary>
        /// Push a "you have a new report to review" event to a supervisor.
        /// Fires after a student submits a report.
        /// Client event name: <c>"NewReport"</c>.
        /// </summary>
        Task NotifySupervisorNewReportAsync(
            string supervisorId,
            SupervisorNewReportResponse payload,
            CancellationToken ct = default);

        /// <summary>
        /// Notify the student that a feedback they were viewing was edited.
        /// Useful so the student's open view refreshes to the new comment.
        /// Client event name: <c>"FeedbackUpdated"</c>.
        /// </summary>
        Task NotifyStudentFeedbackUpdatedAsync(
            string studentId,
            StudentFeedbackNotificationResponse payload,
            int unseenCount,
            CancellationToken ct = default);

        /// <summary>
        /// Notify the student that a feedback was deleted by the supervisor,
        /// so the frontend can remove it from any open list/menu.
        /// Client event name: <c>"FeedbackDeleted"</c>.
        /// </summary>
        Task NotifyStudentFeedbackDeletedAsync(
            string studentId,
            Guid feedbackId,
            Guid reportId,
            int unseenCount,
            CancellationToken ct = default);

        /// <summary>
        /// Notify a user that their account has just been blocked. The frontend
        /// should treat this like a forced logout. Their JWT is still valid until
        /// it expires, so this is the only way to kick them out in real time.
        /// Client event name: <c>"AccountBlocked"</c>.
        /// </summary>
        Task NotifyUserBlockedAsync(
            string userId,
            CancellationToken ct = default);
    }
}

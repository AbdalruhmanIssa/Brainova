using Brainova.BLL.DTOs.Realtime;
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

        /// <summary>
        /// Notify a user that their account has been unblocked.
        /// Mostly informational — they were already logged out by the block,
        /// so the next login uses the unblocked state. Sent for symmetry and
        /// so any open "user list" admin views can refresh.
        /// Client event name: <c>"AccountUnblocked"</c>.
        /// </summary>
        Task NotifyUserUnblockedAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>
        /// Notify a user that their role was changed by an admin. The frontend
        /// should re-fetch the user object and re-route them to the dashboard
        /// matching the new role.
        /// Client event name: <c>"RoleChanged"</c>.
        /// </summary>
        Task NotifyUserRoleChangedAsync(
            string userId,
            string? oldRole,
            string newRole,
            CancellationToken ct = default);

        /// <summary>
        /// Notify a user that their profile was edited by an admin. The frontend
        /// should re-fetch the user object so any open profile/header info is fresh.
        /// Client event name: <c>"UserUpdated"</c>.
        /// </summary>
        Task NotifyUserUpdatedAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>
        /// Notify a list of students that their supervisor's question set changed.
        /// Frontend should invalidate the "GET /Student/Reports/questions" query.
        /// Caller pre-resolves student IDs (we keep this service dumb of DAL).
        /// Client event name: <c>"QuestionsChanged"</c>.
        /// </summary>
        Task NotifyStudentsQuestionsChangedAsync(
            IEnumerable<string> studentIds,
            CancellationToken ct = default);

        /// <summary>
        /// Notify a supervisor that their student roster changed
        /// (student assigned to them, removed from them, deleted, blocked, etc.).
        /// Frontend invalidates the supervisor's "my students" and case-list queries.
        /// Client event name: <c>"StudentsChanged"</c>.
        /// </summary>
        Task NotifySupervisorStudentsChangedAsync(
            string supervisorId,
            CancellationToken ct = default);

        /// <summary>
        /// Notify a supervisor that one of THEIR feedbacks changed because they
        /// (or one of their other tabs) added / edited / deleted it. Lets the
        /// supervisor's own feedback list re-fetch live across tabs/devices.
        /// Client event name: <c>"SupervisorFeedbackChanged"</c>.
        /// Payload kind: "Added" | "Updated" | "Deleted".
        /// </summary>
        Task NotifySupervisorFeedbackChangedAsync(
            string supervisorId,
            string kind,
            Guid feedbackId,
            Guid reportId,
            CancellationToken ct = default);

        /// <summary>
        /// Notify the supervisor that the student read (or read all of)
        /// the feedbacks they sent. Lets the supervisor's feedback list
        /// reflect <c>isSeen=true</c> live without polling.
        /// Client event name: <c>"FeedbackSeenByStudent"</c>.
        /// </summary>
        Task NotifySupervisorFeedbackSeenAsync(
            string supervisorId,
            Guid? feedbackId,
            Guid? reportId,
            CancellationToken ct = default);

        /// <summary>
        /// Broadcast to admins + super-admins that the user list changed
        /// (created / deleted / blocked / unblocked / role-changed / edited).
        /// Payload carries <c>kind</c>, the affected user's id (+ optional name/role),
        /// and the acting admin's id so the frontend can show contextual toasts
        /// and/or selectively patch its cache instead of refetching the whole list.
        /// Client event name: <c>"UserListChanged"</c>.
        /// </summary>
        Task NotifyAdminsUserListChangedAsync(
            UserListChangePayload payload,
            CancellationToken ct = default);
    }
}

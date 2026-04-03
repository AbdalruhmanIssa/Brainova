using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response.Feedback;

namespace Brainova.BLL.Services.Interface
{
    public interface IFeedbackService
    {
        Task<string> AddAsync(string supervisorId, Guid reportId, CreateFeedbackRequest request);

        Task<FeedbackResponse> GetForSupervisorAsync(string supervisorId, Guid reportId);

        Task<FeedbackResponse> GetForStudentAsync(string studentId, Guid reportId);

        Task<List<FeedbackResponse>> GetBySupervisorAsync(string supervisorId);

        Task<StudentNotificationsResult> GetAllForStudentAsync(string studentId, StudentFeedbacksQuery query,
            CancellationToken ct = default);
        Task<StudentNotificationsResult> GetUnseenForStudentAsync(
              string studentId,
              StudentFeedbacksQuery query,
              CancellationToken ct = default);

        Task MarkSeenForStudentAsync(
            string studentId,
            Guid feedbackId,
            CancellationToken ct = default);
        Task MarkAllSeenForStudentAsync(
    string studentId,
    CancellationToken ct = default);
    }
}
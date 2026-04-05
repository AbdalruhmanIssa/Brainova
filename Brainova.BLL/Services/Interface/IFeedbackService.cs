using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;

namespace Brainova.BLL.Services.Interface
{
    public interface IFeedbackService
    {
        Task<string> AddAsync(string supervisorId, Guid reportId, CreateFeedbackRequest request);

        Task<FeedbackResponse> GetForSupervisorAsync(string supervisorId, Guid reportId);

        Task<FeedbackResponse> GetForStudentAsync(string studentId, Guid reportId);

        Task<List<FeedbackResponse>> GetBySupervisorAsync(string supervisorId);

        Task<(int TotalCount, List<StudentFeedbackNotificationResponse> Items)>
         GetUnseenForStudentAsync(string studentId, CancellationToken ct = default);

        Task<(int TotalCount, int UnseenCount, List<StudentFeedbackNotificationResponse> Items)>
           GetAllForStudentAsync(string studentId, CancellationToken ct = default);

           Task MarkSeenForStudentAsync(
            string studentId,
            Guid feedbackId,
            CancellationToken ct = default);
        Task MarkAllSeenForStudentAsync(
    string studentId,
    CancellationToken ct = default);
        Task<FeedbackResponse> UpdateAsync(string supervisorId, Guid feedbackId, UpdateFeedbackRequest request, CancellationToken ct = default);

        Task<string> DeleteAsync(string supervisorId, Guid feedbackId, CancellationToken ct = default);
        Task<(int TotalCount, List<FeedbackResponse> Items)> GetAllForSupervisorAsync(
    string supervisorId,
    CancellationToken ct = default);


    }
}
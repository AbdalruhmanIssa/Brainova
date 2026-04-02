using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;

namespace Brainova.BLL.Services.Interface
{
    public interface IFeedbackService
    {
        Task<string> AddAsync(string supervisorId, Guid reportId, CreateFeedbackRequest request);
        Task<List<FeedbackResponse>> GetByReportIdAsync(Guid reportId);
        Task<List<FeedbackResponse>> GetAllAsync();
        Task<List<FeedbackResponse>> GetForStudentAsync(string studentId, Guid reportId);
        Task<string> UpdateAsync(string? supervisorId, Guid feedbackId, UpdateFeedbackRequest request);
        Task<string> DeleteAsync(string? supervisorId, Guid feedbackId);
        Task<List<FeedbackResponse>> GetBySupervisorAsync(string supervisorId);
        Task<List<FeedbackResponse>> GetUnseenForStudentAsync(string studentId);
        Task<string> MarkAsSeenAsync(string studentId, Guid feedbackId);
    }
}
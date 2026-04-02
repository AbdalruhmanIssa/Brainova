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

        Task<List<FeedbackResponse>> GetAllAsync();
    }
}
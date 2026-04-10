using Brainova.BLL.DTOs.Request;
using Brainova.DAL.Modles;

namespace Brainova.BLL.Services.Interface
{
    public interface IReportQuestionService
    {
        Task AddAsync(string supervisorId, CreateReportQuestionRequest req);
        Task<List<ReportQuestion>> GetActiveForStudentAsync(string studentId);
        Task<List<ReportQuestion>> GetAllForSupervisorAsync(string supervisorId);
        Task UpdateAsync(string supervisorId, Guid id, UpdateReportQuestionRequest req);
        Task ToggleActiveAsync(string supervisorId, Guid id);
        Task SeedDefaultQuestionsForSupervisorAsync(string supervisorId, CancellationToken ct = default);
        Task SeedDefaultQuestionsForAllExistingSupervisorsAsync(CancellationToken ct = default);
    }
}
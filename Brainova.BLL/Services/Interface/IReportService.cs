
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.DAL.Modles;

namespace Brainova.BLL.Services.Interface
{
    public interface IReportService
    {
        Task<Guid> SubmitAsync(string studentId, SubmitReportRequest req);

        Task<List<SupervisorNewReportResponse>> GetNewForSupervisorAsync(string supervisorId);
        Task<SupervisorReportDetailsRawResponse> GetSupervisorDetailsAsync(string supervisorId, Guid reportId);
        Task<ReportPdfResponse> GetSupervisorPdfDetailsAsync(string supervisorId, Guid reportId);
        Task<ReportPdfResponse> GetStudentPdfDetailsAsync(string studentId, Guid reportId);
    }

}


using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.DTOs.Response.Report;
using Brainova.DAL.Modles;

namespace Brainova.BLL.Services.Interface
{
    public interface IReportService
    {
        Task<Guid> SubmitAsync(string studentId, SubmitReportRequest req);


        Task<PagedResponse<SupervisorNewReportResponse>> GetNewForSupervisorAsync(
      string supervisorId,
      int page,
      int pageSize);
        Task<SupervisorReportDetailsRawResponse> GetSupervisorDetailsAsync(string supervisorId, Guid reportId);
        Task<ReportPdfResponse> GetSupervisorPdfDetailsAsync(string supervisorId, Guid reportId);
        Task<ReportPdfResponse> GetStudentPdfDetailsAsync(string studentId, Guid reportId);
    }

}

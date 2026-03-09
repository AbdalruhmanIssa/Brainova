namespace Brainova.BLL.Services.Interface
{
    public interface IReportPdfService
    {
        Task<byte[]> GenerateSupervisorReportPdfAsync(string supervisorId, Guid reportId, CancellationToken ct = default);
    }
}
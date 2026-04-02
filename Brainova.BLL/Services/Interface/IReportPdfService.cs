namespace Brainova.BLL.Services.Interface
{
    public interface IReportPdfService
    {
        Task<byte[]> GenerateSupervisorReportPdfAsync(string supervisorId, Guid reportId, CancellationToken ct = default);
        Task<byte[]> GenerateStudentReportPdfAsync(string studentId, Guid reportId, CancellationToken ct = default);
    }
}


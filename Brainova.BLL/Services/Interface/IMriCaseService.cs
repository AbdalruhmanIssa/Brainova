using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.DAL.Modles;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface IMriCaseService
    {
        Task<MriUploadResponse> CreateAsync(string studentId, MriUploadRequest request, CancellationToken ct = default);
        Task<string?> GetStoredFileNameAsync(Guid caseId, CancellationToken ct = default);
      
      Task<List<StudentCaseDetailsResponse>> GetMyCasesAsync(string studentId, CancellationToken ct = default);
        Task<List<SupervisorStudentCaseDetailsResponse>> GetSupervisorCasesAsync(
        string supervisorId,
        string? studentId = null,
        CancellationToken ct = default);

        // ---- Image access (used by CaseImagesController) ----

        // True when userId is the student who owns the case with this MRI file,
        // or that student's assigned supervisor.
        Task<bool> CanViewMriImageAsync(string userId, string storedFileName, CancellationToken ct = default);

        // Same rule for the Grad-CAM heatmap produced for a case.
        Task<bool> CanViewGradcamImageAsync(string userId, string gradcamFileName, CancellationToken ct = default);

        // True when this heatmap belongs to a real case (as opposed to the open demo endpoint).
        Task<bool> IsCaseGradcamFileAsync(string gradcamFileName, CancellationToken ct = default);
     

    }
}

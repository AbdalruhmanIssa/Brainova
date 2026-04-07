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
     CancellationToken ct = default);
        Task<List<AdminCaseDetailsResponse>> GetAdminCasesAsync(CancellationToken ct = default);

    }
}

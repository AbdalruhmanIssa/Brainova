using Brainova.BLL.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface IDashboardService
    {
        Task<SupervisorDashboardSummaryResponse> GetSupervisorDashboardSummaryAsync(
            string supervisorId,
            CancellationToken ct = default);
        Task<StudentDashboardSummaryResponse> GetStudentDashboardSummaryAsync(
            string studentId,
            CancellationToken ct = default);
    }
}

using Brainova.BLL.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface ISupervisorService
    {
        Task<SupervisorDashboardSummaryResponse> GetDashboardSummaryAsync(
            string supervisorId,
            CancellationToken ct = default);
    }
}

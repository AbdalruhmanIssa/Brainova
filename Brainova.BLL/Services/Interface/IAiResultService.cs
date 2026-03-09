using Brainova.BLL.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface IAiResultService
    {
      Task<PredictResponse> PredictAsync(  string studentId,
      Guid caseId, CancellationToken ct = default);
    }
}

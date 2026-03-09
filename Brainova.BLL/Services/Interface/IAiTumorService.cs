using Brainova.BLL.DTOs.Response;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface IAiTumorService
    {
        //    Task<(string label, float[] probabilities)> PredictAsync(Stream imageStream);
        Task<GradcamResultDto> GetGradcamAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);


    }
}

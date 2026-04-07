using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Brainova.BLL.Exceptions;
namespace Brainova.BLL.Services.Classes
{
    public class AiResultService : IAiResultService
    {
        private readonly IUnitOfWork _uow;
        private readonly IWebHostEnvironment _env;
        private readonly IAiTumorService _aiTumorService;

        public AiResultService(IUnitOfWork uow, IWebHostEnvironment env, IAiTumorService aiTumorService)
        {
            _uow = uow;
            _env = env;
            _aiTumorService = aiTumorService;
        }

        public async Task<PredictResponse> PredictAsync(
     string studentId,
     Guid caseId,
     CancellationToken ct = default)
        {
            // 1️⃣ Load case + ownership check
            var mriCase = await _uow.Repo<MriCase>()
                .Query()
                .FirstOrDefaultAsync(x => x.Id == caseId && x.StudentId == studentId, ct);

            if (mriCase == null)
                throw new Exception("Case not found.");

            // 2️⃣ Load image from disk
            var fullPath = Path.Combine(
                _env.ContentRootPath,
                "App_Data",
                "mri",
                mriCase.StoredFileName
            );

            if (!File.Exists(fullPath))
                throw new Exception("Image file missing.");

            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

            // 3️⃣ Call AI service (your existing stream overload)
            var ai = await _aiTumorService.GetGradcamAsync(
                stream,
                mriCase.StoredFileName,
                "image/jpeg",
                ct);

            // 4️⃣ Save AiResult
            var aiResult = new AiResult
            {
                CaseId = caseId,
                PredictionResult = ai.Label,
                ProbabilitiesJson = JsonSerializer.Serialize(ai.Probabilities),
                GradcamFileName = ai.FileName
            };

            await _uow.Repo<AiResult>().AddAsync(aiResult, ct);

            // 5️⃣ Update case status
            mriCase.Status = CaseStatus.Predicted;
            _uow.Repo<MriCase>().Update(mriCase);

            await _uow.SaveChangesAsync(ct);

            // 6️⃣ Return response
            return new PredictResponse
            {
                CaseId = caseId,
                Prediction = aiResult.PredictionResult,
                Probabilities = ai.Probabilities,
                GradcamUrl = $"/api/AiTumors/gradcam-image/{aiResult.GradcamFileName}",
                CreatedAt = aiResult.CreatedAt
            };
        }
    }
}

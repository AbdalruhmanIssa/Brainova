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

        public AiResultService(
            IUnitOfWork uow,
            IWebHostEnvironment env,
            IAiTumorService aiTumorService)
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
            // 1) Load case + ownership check
            var mriCase = await _uow.Repo<MriCase>()
                .Query()
                .FirstOrDefaultAsync(x => x.Id == caseId && x.StudentId == studentId, ct);

            if (mriCase == null)
                throw new NotFoundException("Case not found.");

            // 2) Prevent duplicate prediction before calling AI
            var existingAiResult = await _uow.Repo<AiResult>()
                .Query()
                .FirstOrDefaultAsync(x => x.CaseId == caseId, ct);

            if (existingAiResult != null)
                throw new BadRequestException("AI prediction already exists for this case.");

            // optional extra guard by status
            if (mriCase.Status == CaseStatus.Predicted || mriCase.Status == CaseStatus.Reviewed)
                throw new BadRequestException("This case has already been predicted.");

            // 3) Load image from disk
            var fullPath = Path.Combine(
                _env.ContentRootPath,
                "App_Data",
                "mri",
                mriCase.StoredFileName
            );

            if (!File.Exists(fullPath))
                throw new NotFoundException("Image file is missing.");

            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

            // 4) Call AI service
            var ai = await _aiTumorService.GetGradcamAsync(
                stream,
                mriCase.StoredFileName,
                "image/jpeg",
                ct);

            // 5) Save AiResult
            var aiResult = new AiResult
            {
                CaseId = caseId,
                PredictionResult = ai.Label,
                ProbabilitiesJson = JsonSerializer.Serialize(ai.Probabilities),
                GradcamFileName = ai.FileName
            };

            await _uow.Repo<AiResult>().AddAsync(aiResult, ct);

            // 6) Update case status
            mriCase.Status = CaseStatus.Predicted;
            _uow.Repo<MriCase>().Update(mriCase);

            await _uow.SaveChangesAsync(ct);

            // 7) Return response
            return new PredictResponse
            {
                CaseId = caseId,
                Prediction = aiResult.PredictionResult,
                Probabilities = ai.Probabilities,
                GradcamUrl = $"/api/AiTumors/gradcam-image/{aiResult.GradcamFileName}",
                CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(
                    aiResult.CreatedAt,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron"))
            };
        }
    }
}
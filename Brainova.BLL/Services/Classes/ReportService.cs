
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;

using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Brainova.BLL.Services.Classes
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _uow;

        public ReportService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Guid> SubmitAsync(string studentId, SubmitReportRequest req)
        {
            var caseRepo = _uow.Repo<MriCase>();
            var reportRepo = _uow.Repo<Report>();
            var questionRepo = _uow.Repo<ReportQuestion>();
            var answerRepo = _uow.Repo<ReportAnswer>();
            //load case

            var mriCase = await caseRepo.GetByIdAsync(req.CaseId);

            if (mriCase == null)
                throw new NotFoundException("Case not found");
            //check ownership

            if (mriCase.StudentId != studentId)
                throw new ForbiddenException("You don't own this case");
            //check status (must be uploaded, not already submitted or closed)

            if (mriCase.Status != CaseStatus.Uploaded)
                throw new BadRequestException("Report already submitted or case closed");

            bool exists = await reportRepo.ExistsAsync(r =>
                r.CaseId == req.CaseId && r.StudentId == studentId);

            if (exists)
                throw new BadRequestException("Report already exists for this case");
            //load questions

            var questions = await questionRepo.Query()
                .Where(q => q.IsActive)
                .OrderBy(q => q.Order)
                .ToListAsync();

            if (req.Answers.Count != questions.Count)
                throw new BadRequestException("All questions must be answered");

            var report = new Report
            {
                Id = Guid.NewGuid(),
                CaseId = req.CaseId,
                StudentId = studentId,
                SubmittedAt = DateTime.UtcNow
            };

            await reportRepo.AddAsync(report);

            foreach (var q in questions)
            {
                var ans = req.Answers.FirstOrDefault(a => a.QuestionId == q.Id);

                var value = ans?.AnswerValue?.Trim();

                if (q.IsRequired && string.IsNullOrWhiteSpace(value))
                    throw new BadRequestException($"Question '{q.Code}' is required.");

                if (!string.IsNullOrWhiteSpace(value))
                {
                    ValidateAnswer(q, value);
                }

                var reportAnswer = new ReportAnswer
                {
                    Id = Guid.NewGuid(),
                    ReportId = report.Id,
                    QuestionId = q.Id,
                    AnswerValue = value, // can be null

                    QuestionTextSnapshot = q.Text,
                    QuestionTypeSnapshot = q.Type
                };

                await _uow.Repo<ReportAnswer>().AddAsync(reportAnswer);
            }

            mriCase.Status = CaseStatus.ReportSubmitted;
            caseRepo.Update(mriCase);

            await _uow.SaveChangesAsync();

            return report.Id;
        }

        public async Task<List<SupervisorNewReportResponse>> GetNewForSupervisorAsync(string supervisorId)
        {
            return await _uow.Repo<Report>()
                .Query()
                .Where(r =>
                   ( r.Case.Status == CaseStatus.ReportSubmitted ||r.Case.Status==CaseStatus.Predicted) &&
                    r.Case.Student.SupervisorUserId == supervisorId)
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new SupervisorNewReportResponse
                {
                    ReportId = r.Id,
                    CaseId = r.CaseId,
                    SubmittedAt = r.SubmittedAt,
                    StudentId = r.Case.StudentId,
                    StudentName = r.Case.Student.FullName // or UserName if that’s what you want
                })
                .ToListAsync();
        }
        public async Task<SupervisorReportDetailsRawResponse> GetSupervisorDetailsAsync(string supervisorId, Guid reportId)
        {
            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Case)
                    .ThenInclude(c => c.Student)
                .Include(r => r.Case)
                    .ThenInclude(c => c.AiResult)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
                throw new NotFoundException("Report not found");

            if (report.Case.Student.SupervisorUserId != supervisorId)
                throw new BadRequestException("Not your student");

            var answers = await _uow.Repo<ReportAnswer>()
                .Query()
                .Include(a => a.Question)
                .Where(a => a.ReportId == reportId)
                .OrderBy(a => a.Question.Order)
                .ToListAsync();

            var dto = new SupervisorReportDetailsRawResponse
            {
                ReportId = report.Id,
                CaseId = report.CaseId,
                StudentId = report.Case.StudentId,
                StudentName = report.Case.Student.FullName,
                SubmittedAt = report.CreatedAt,

                StoredFileName = report.Case.StoredFileName,

                PredictionResult = report.Case.AiResult?.PredictionResult,
              

                Answers = answers.Select(a => new SupervisorReportAnswerResponse
                {
                    QuestionId = a.QuestionId,
                    Code = a.Question.Code,
                    Question = a.Question.Text,
                    Type = a.Question.Type,
                    AnswerValue = a.AnswerValue
                }).ToList()
            };

            return dto;
        }
        public async Task<ReportPdfResponse> GetSupervisorPdfDetailsAsync(string supervisorId, Guid reportId)
        {
            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Case)
                    .ThenInclude(c => c.Student)
                .Include(r => r.Case)
                    .ThenInclude(c => c.AiResult)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
                throw new NotFoundException("Report not found");

            if (report.Case.Student.SupervisorUserId != supervisorId)
                throw new BadRequestException("Not your student");

            return await BuildPdfResponseAsync(report);
        }
        public async Task<ReportPdfResponse> GetStudentPdfDetailsAsync(string studentId, Guid reportId)
        {
            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Case)
                    .ThenInclude(c => c.Student)
                .Include(r => r.Case)
                    .ThenInclude(c => c.AiResult)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
                throw new NotFoundException("Report not found");

            if (report.Case.StudentId != studentId)
                throw new BadRequestException("This report does not belong to you");

            return await BuildPdfResponseAsync(report);
        }
        private async Task<ReportPdfResponse> BuildPdfResponseAsync(Report report)
        {
            var answers = await _uow.Repo<ReportAnswer>()
                .Query()
                .Include(a => a.Question)
                .Where(a => a.ReportId == report.Id)
                .OrderBy(a => a.Question.Order)
                .ToListAsync();

            var supervisorName = "N/A";
            if (!string.IsNullOrWhiteSpace(report.Case.Student.SupervisorUserId))
            {
                var supervisor = await _uow.Repo<ApplicationUser>()
                    .Query()
                    .FirstOrDefaultAsync(u => u.Id == report.Case.Student.SupervisorUserId);

                supervisorName = supervisor?.FullName ?? "N/A";
            }

            var probabilities = new List<ProbabilityItemResponse>();

            if (!string.IsNullOrWhiteSpace(report.Case.AiResult?.ProbabilitiesJson))
            {
                try
                {
                    //  the JSON is an array of floats in the order: [Glioma, Meningioma, No Tumor, Pituitary]
                    var arr = System.Text.Json.JsonSerializer.Deserialize<float[]>(report.Case.AiResult.ProbabilitiesJson);
                    //  map them to the response DTO

                    if (arr != null && arr.Length >= 4)
                    {
                        probabilities = new List<ProbabilityItemResponse>
                {
                    new ProbabilityItemResponse { Label = "Glioma", Value = arr[0] },
                    new ProbabilityItemResponse { Label = "Meningioma", Value = arr[1] },
                    new ProbabilityItemResponse { Label = "No Tumor", Value = arr[2] },
                    new ProbabilityItemResponse { Label = "Pituitary", Value = arr[3] }
                };
                    }
                }
                catch
                {
                }
            }

            return new ReportPdfResponse
            {
                ReportId = report.Id,
                CaseId = report.CaseId,
                StudentId = report.Case.StudentId,
                StudentName = report.Case.Student.FullName,
                SupervisorName = supervisorName,
                SubmittedAt = report.SubmittedAt,
                CaseCreatedAt = report.Case.CreatedAt,
                PredictionCreatedAt = report.Case.AiResult?.CreatedAt,
                StoredFileName = report.Case.StoredFileName,
                PredictionResult = report.Case.AiResult?.PredictionResult,
                Probabilities = probabilities,
                Answers = answers.Select(a => new ReportPdfAnswerResponse
                {
                    QuestionId = a.QuestionId,
                    Question = a.Question.Text,
                    Code = a.Question.Code,
                    Type = a.Question.Type,
                    AnswerValue = a.AnswerValue
                }).ToList()
            };
        }
        private static void ValidateAnswer(ReportQuestion question, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new BadRequestException($"Answer for '{question.Code}' is required.");

            switch (question.Type)
            {
                case ReportQuestionType.Text:
                    return;

                case ReportQuestionType.SingleChoice:
                    ValidateSingleChoice(question, value);
                    return;

                default:
                    throw new BadRequestException($"Unsupported question type for '{question.Code}'.");
            }
        }

        private static void ValidateSingleChoice(ReportQuestion question, string value)
        {
            if (string.IsNullOrWhiteSpace(question.OptionsJson))
                throw new BadRequestException($"Options not defined for '{question.Code}'.");

            List<string>? options;
            try
            {
                options = JsonSerializer.Deserialize<List<string>>(question.OptionsJson);
            }
            catch
            {
                throw new BadRequestException($"Invalid options configuration for '{question.Code}'.");
            }

            if (options == null || options.Count == 0)
                throw new BadRequestException($"Options not defined for '{question.Code}'.");

            var normalizedValue = value.Trim().ToLower();

            var normalizedOptions = options
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim().ToLower())
                .ToList();

            if (!normalizedOptions.Contains(normalizedValue))
                throw new BadRequestException(
                    $"Invalid answer '{value}' for '{question.Code}'. Allowed values: {string.Join(", ", options)}"
                );
        }
    }
}
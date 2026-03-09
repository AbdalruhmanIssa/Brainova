
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
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

            var mriCase = await caseRepo.GetByIdAsync(req.CaseId);

            if (mriCase == null)
                throw new Exception("Case not found");

            if (mriCase.StudentId != studentId)
                throw new Exception("You don't own this case");

            if (mriCase.Status != CaseStatus.Uploaded)
                throw new Exception("Report already submitted or case closed");

            bool exists = await reportRepo.ExistsAsync(r =>
                r.CaseId == req.CaseId && r.StudentId == studentId);

            if (exists)
                throw new Exception("Report already exists for this case");

            var questions = await questionRepo.Query()
                .Where(q => q.IsActive)
                .OrderBy(q => q.Order)
                .ToListAsync();

            if (req.Answers.Count != questions.Count)
                throw new Exception("All questions must be answered");

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
                var answer = req.Answers.FirstOrDefault(a => a.QuestionId == q.Id);

                if (answer == null)
                    throw new Exception($"Missing answer for question {q.Code}");

                var ans = new ReportAnswer
                {
                    Id = Guid.NewGuid(),
                    ReportId = report.Id,
                    QuestionId = q.Id,
                    AnswerText = answer.AnswerText,
                    AnswerNumber = answer.AnswerNumber,
                    AnswerBool = answer.AnswerBool,
                    AnswerJson = answer.AnswerJson,

                    QuestionTextSnapshot = q.Text,
                    QuestionTypeSnapshot = q.Type
                };

                await answerRepo.AddAsync(ans);
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
                    r.Case.Status == CaseStatus.ReportSubmitted &&
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
                throw new Exception("Report not found");

            if (report.Case.Student.SupervisorUserId != supervisorId)
                throw new Exception("Not your student");

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

                    AnswerText = a.AnswerText,
                    AnswerNumber = a.AnswerNumber,
                    AnswerBool = a.AnswerBool,
                    AnswerJson = a.AnswerJson
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
                        .ThenInclude(s => s.SupervisorUser)
                .Include(r => r.Case)
                    .ThenInclude(c => c.AiResult)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
                throw new Exception("Report not found");

            if (report.Case.Student.SupervisorUserId != supervisorId)
                throw new Exception("Not your student");

            var answers = await _uow.Repo<ReportAnswer>()
                .Query()
                .Include(a => a.Question)
                .Where(a => a.ReportId == reportId)
                .OrderBy(a => a.Question.Order)
                .ToListAsync();

            var probabilities = new List<ProbabilityItemResponse>();

            if (!string.IsNullOrWhiteSpace(report.Case.AiResult?.ProbabilitiesJson))
            {
                try
                {
                    var values = JsonSerializer.Deserialize<float[]>(report.Case.AiResult.ProbabilitiesJson) ?? Array.Empty<float>();

                    var labels = new[] { "Glioma", "Meningioma", "No Tumor", "Pituitary" };

                    for (int i = 0; i < labels.Length && i < values.Length; i++)
                    {
                        probabilities.Add(new ProbabilityItemResponse
                        {
                            Label = labels[i],
                            Value = values[i]
                        });
                    }
                }
                catch
                {
                    // leave empty if malformed json
                }
            }

            return new ReportPdfResponse
            {
                ReportId = report.Id,
                CaseId = report.CaseId,

                StudentId = report.Case.StudentId,
                StudentName = report.Case.Student.FullName,
                SupervisorName = report.Case.Student.SupervisorUser?.FullName ?? "Not Assigned",

                SubmittedAt = report.SubmittedAt == default ? report.CreatedAt : report.SubmittedAt,
                CaseCreatedAt = report.Case.CreatedAt,
                PredictionCreatedAt = report.Case.AiResult?.CreatedAt,

                StoredFileName = report.Case.StoredFileName,
                PredictionResult = report.Case.AiResult?.PredictionResult,
                Probabilities = probabilities,

                Answers = answers.Select(a => new ReportPdfAnswerResponse
                {
                    QuestionId = a.QuestionId,
                    Code = a.Question.Code,
                    Question = a.QuestionTextSnapshot ?? a.Question.Text,
                    Type = a.QuestionTypeSnapshot != 0 ? a.QuestionTypeSnapshot : a.Question.Type,
                    AnswerText = a.AnswerText,
                    AnswerNumber = a.AnswerNumber,
                    AnswerBool = a.AnswerBool,
                    AnswerJson = a.AnswerJson
                }).ToList()
            };
        }

    }
}

using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.DTOs.Response.Report;
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
        private readonly INotificationService _notifications;

        public ReportService(IUnitOfWork uow, INotificationService notifications)
        {
            _uow = uow;
            _notifications = notifications;
        }

        public async Task<Guid> SubmitAsync(string studentId, SubmitReportRequest req)
        {
            await using var transaction = await _uow.BeginTransactionAsync();

           
                var caseRepo = _uow.Repo<MriCase>();
                var reportRepo = _uow.Repo<Report>();
                var questionRepo = _uow.Repo<ReportQuestion>();
                var answerRepo = _uow.Repo<ReportAnswer>();

                var mriCase = await caseRepo.GetByIdAsync(req.CaseId);

                if (mriCase == null)
                    throw new NotFoundException("Case not found");

                if (mriCase.StudentId != studentId)
                    throw new ForbiddenException("You don't own this case");

                if (mriCase.Status != CaseStatus.Uploaded)
                    throw new BadRequestException("Report already submitted or case closed");

                bool reportExists = await reportRepo.ExistsAsync(r =>
                    r.CaseId == req.CaseId && r.StudentId == studentId);

                if (reportExists)
                    throw new BadRequestException("Report already exists for this case");

                var student = await _uow.Repo<ApplicationUser>()
                    .Query()
                    .FirstOrDefaultAsync(u => u.Id == studentId);

                if (student == null)
                    throw new NotFoundException("Student not found");

                if (string.IsNullOrWhiteSpace(student.SupervisorUserId))
                    throw new BadRequestException("Student has no assigned supervisor");

                var questions = await questionRepo.Query()
                    .Where(q => q.IsActive && q.SupervisorId == student.SupervisorUserId)
                    .OrderBy(q => q.Order)
                    .ToListAsync();

                if (!questions.Any())
                    throw new BadRequestException("No active report questions found");

                var activeQuestionIds = questions.Select(q => q.Id).ToHashSet();

                var invalidSubmittedQuestion = req.Answers
                    .FirstOrDefault(a => !activeQuestionIds.Contains(a.QuestionId));

                if (invalidSubmittedQuestion != null)
                    throw new BadRequestException("One or more submitted question ids are invalid");

                var preliminaryAssessmentQuestion = questions
                    .FirstOrDefault(q => q.Code.Trim().ToLower() == "preliminary assesment");

                if (preliminaryAssessmentQuestion == null)
                    throw new BadRequestException("Preliminary assessment question is missing");

                var preliminaryAssessmentAnswer = req.Answers
                    .FirstOrDefault(a => a.QuestionId == preliminaryAssessmentQuestion.Id)?
                    .AnswerValue?
                    .Trim();

                if (string.IsNullOrWhiteSpace(preliminaryAssessmentAnswer))
                    throw new BadRequestException("Preliminary assessment answer is required");

                ValidateAnswer(preliminaryAssessmentQuestion, preliminaryAssessmentAnswer);

                bool isNoTumor = preliminaryAssessmentAnswer.Trim().ToLower() == "no tumor";

                var report = new Report
                {
                    Id = Guid.NewGuid(),
                    CaseId = req.CaseId,
                    StudentId = studentId,
                    SubmittedAt = DateTime.UtcNow
                };

                await reportRepo.AddAsync(report);
                await _uow.SaveChangesAsync(); // first save: DB generates ReportNumber

                if (report.ReportNumber <= 0)
                    throw new BadRequestException("Failed to generate report number.");

                report.ReportCode = BuildReportCode(report.ReportNumber, report.SubmittedAt);
                //reportRepo.Update(report);

                foreach (var q in questions)
                {
                    var submitted = req.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                    var value = submitted?.AnswerValue?.Trim();

                    bool shouldSkipBecauseNoTumor = isNoTumor && q.SkipWhenNoTumor;

                    if (shouldSkipBecauseNoTumor)
                    {
                        value = null;
                    }
                    else
                    {
                        if (q.IsRequired && string.IsNullOrWhiteSpace(value))
                            ValidateAnswer(q, value);

                        if (!string.IsNullOrWhiteSpace(value))
                            ValidateAnswer(q, value);
                    }

                    var answer = new ReportAnswer
                    {
                        Id = Guid.NewGuid(),
                        ReportId = report.Id,
                        QuestionId = q.Id,
                        AnswerValue = value,
                        QuestionTextSnapshot = q.Text,
                        QuestionTypeSnapshot = q.Type
                    };

                    await answerRepo.AddAsync(answer);
                }

                mriCase.Status = CaseStatus.ReportSubmitted;
                caseRepo.Update(mriCase);

                await _uow.SaveChangesAsync(); // second save
                await transaction.CommitAsync();

                // -----------------------------------------------------------
                // Real-time push (SignalR): notify the supervisor that this
                // student just submitted a new report for review. Best-effort,
                // wrapped so SignalR failures never break the API response.
                // -----------------------------------------------------------
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

                    var payload = new SupervisorNewReportResponse
                    {
                        ReportId = report.Id,
                        ReportCode = report.ReportCode,
                        CaseId = report.CaseId,
                        SubmittedAt = TimeZoneInfo.ConvertTimeFromUtc(report.SubmittedAt, tz),
                        StudentId = studentId,
                        StudentName = student.FullName
                    };

                    await _notifications.NotifySupervisorNewReportAsync(
                        student.SupervisorUserId!,
                        payload);
                }
                catch
                {
                    // Notification is best-effort. The report is already saved.
                }

                return report.Id;
            }
           

        public async Task<List<SupervisorNewReportResponse>> GetNewForSupervisorAsync(string supervisorId)
        {
            var reports = await _uow.Repo<Report>()
                .Query()
                .Where(r =>
                    (r.Case.Status == CaseStatus.ReportSubmitted || r.Case.Status == CaseStatus.Predicted) &&
                    r.Case.Student.SupervisorUserId == supervisorId)
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new SupervisorNewReportResponse
                {
                    ReportId = r.Id,
                    ReportCode = r.ReportCode,
                    CaseId = r.CaseId,
                    SubmittedAt = r.SubmittedAt,
                    StudentId = r.Case.StudentId,
                    StudentName = r.Case.Student.FullName
                })
                .ToListAsync();

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

            foreach (var report in reports)
            {
                report.SubmittedAt = TimeZoneInfo.ConvertTimeFromUtc(report.SubmittedAt, tz);
            }

            return reports;
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

            var probabilities = new List<ProbabilityItemResponse>();

            if (!string.IsNullOrWhiteSpace(report.Case.AiResult?.ProbabilitiesJson))
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<float[]>(report.Case.AiResult.ProbabilitiesJson);

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

            var dto = new SupervisorReportDetailsRawResponse
            {
                ReportId = report.Id,
                ReportCode = report.ReportCode ,
                CaseId = report.CaseId,
                StudentId = report.Case.StudentId,
                StudentName = report.Case.Student.FullName,
                StudentEmail = report.Case.Student.Email,
                SubmittedAt = TimeZoneInfo.ConvertTimeFromUtc(
    report.SubmittedAt,
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron")
),
                StoredFileName = report.Case.StoredFileName,
                PredictionResult = report.Case.AiResult?.PredictionResult,
                Probabilities = probabilities,
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
                ReportCode = report.ReportCode,
                CaseId = report.CaseId,
                StudentId = report.Case.StudentId,
                StudentName = report.Case.Student.FullName,
                SupervisorName = supervisorName,
                SubmittedAt = TimeZoneInfo.ConvertTimeFromUtc(
    report.SubmittedAt,
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron")
),
                CaseCreatedAt =TimeZoneInfo.ConvertTimeFromUtc( report.Case.CreatedAt,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron")),
               

                PredictionCreatedAt =TimeZoneInfo.ConvertTimeFromUtc( report.Case.AiResult.CreatedAt,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron")),
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
        private static string BuildReportCode(long reportNumber, DateTime submittedAtUtc)
        {
            return $"REP-{submittedAtUtc.Year}-{reportNumber:D6}";
        }
    }
}
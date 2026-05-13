using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

namespace Brainova.BLL.Services.Classes
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notifications;

        public FeedbackService(IUnitOfWork uow, INotificationService notifications)
        {
            _uow = uow;
            _notifications = notifications;
        }

        public async Task<string> AddAsync(string supervisorId, Guid reportId, CreateFeedbackRequest request)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            if (reportId == Guid.Empty)
                throw new BadRequestException("ReportId is required");

            if (request is null)
                throw new BadRequestException("Request is required");

            if (string.IsNullOrWhiteSpace(request.Comment))
                throw new BadRequestException("Comment is required");

            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Student)
                .Include(r => r.Case)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null)
                throw new NotFoundException("Report not found");

            if (report.Student is null)
                throw new BadRequestException("Student data is missing for this report");

            if (report.Case is null)
                throw new BadRequestException("MRI case data is missing for this report");

            if (string.IsNullOrWhiteSpace(report.Student.SupervisorUserId))
                throw new ForbiddenException("This student is not assigned to any supervisor");

            if (report.Student.SupervisorUserId != supervisorId)
                throw new ForbiddenException("You are not allowed to add feedback to this report");

            var alreadyExists = await _uow.Repo<Feedback>()
                .Query()
                .AnyAsync(f => f.ReportId == reportId);

            if (alreadyExists)
                throw new BadRequestException("Feedback already exists for this report");

            var feedback = new Feedback
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                SupervisorId = supervisorId,
                StudentId = report.StudentId,
                Comment = request.Comment.Trim(),
                IsSeen = false,

            };

            await _uow.Repo<Feedback>().AddAsync(feedback);

            report.Case.Status = CaseStatus.Reviewed;
            _uow.Repo<MriCase>().Update(report.Case);

            await _uow.SaveChangesAsync();

            // -----------------------------------------------------------
            // Real-time push (SignalR): notify the student that a new
            // feedback has arrived. Done AFTER the DB save succeeds so we
            // never tell the student about a feedback that didn't persist.
            // Wrapped in try/catch so a transient SignalR failure cannot
            // bubble up and fail the HTTP request — the feedback is saved
            // either way and the student can still see it via REST.
            // -----------------------------------------------------------
            try
            {
                var supervisorName = await _uow.Repo<ApplicationUser>()
                    .Query()
                    .Where(u => u.Id == supervisorId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync();

                var unseenCount = await _uow.Repo<Feedback>()
                    .Query()
                    .CountAsync(f => f.StudentId == report.StudentId && !f.IsSeen);

                var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

                var payload = new StudentFeedbackNotificationResponse
                {
                    FeedbackId = feedback.Id,
                    ReportId = report.Id,
                    ReportCode = report.ReportCode,
                    CaseId = report.CaseId,
                    SupervisorId = supervisorId,
                    SupervisorName = supervisorName,
                    Comment = feedback.Comment,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(feedback.CreatedAt, tz),
                    IsSeen = feedback.IsSeen
                };

                await _notifications.NotifyStudentNewFeedbackAsync(
                    report.StudentId,
                    payload,
                    unseenCount);
            }
            catch
            {
                // Swallow: notification is best-effort, the feedback is saved.
            }

            return "Feedback added successfully";
        }

        public async Task<FeedbackResponse> GetForSupervisorAsync(string supervisorId, Guid reportId)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var report = await _uow.Repo<Report>()
    .Query()
    .Include(r => r.Student)
    .Include(r => r.Case)
        .ThenInclude(c => c.AiResult)
    .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null)
                throw new NotFoundException("Report not found");

            if (report.Student is null)
                throw new BadRequestException("Student data is missing for this report");

            if (report.Student.SupervisorUserId != supervisorId)
                throw new ForbiddenException("You are not allowed to view feedback for this report");

            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.ReportId == reportId)
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    f => f.SupervisorId,
                    u => u.Id,
                    (f, sup) => new { f, sup }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.StudentId,
                    stu => stu.Id,
                    (x, stu) => new FeedbackResponse
                    {
                        Id = x.f.Id,
                        ReportId = x.f.ReportId,
                        ReportCode = report.ReportCode,
                        SupervisorId = x.f.SupervisorId,
                       
                        SupervisorName = x.sup.FullName,
                        StudentId = x.f.StudentId,
                        StudentName = stu.FullName,
                        PredictionResult = report.Case.AiResult != null
                            ? report.Case.AiResult.PredictionResult
                            : null,
                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt
                    }
                )
                .FirstOrDefaultAsync();

            if (feedback is null)
                throw new NotFoundException("Feedback not found for this report");
            feedback.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(
    feedback.CreatedAt,
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron")
);

            return feedback;
        }

        public async Task<FeedbackResponse> GetForStudentAsync(string studentId, Guid reportId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            var report = await _uow.Repo<Report>()
   .Query()
   .Include(r => r.Student)
   .Include(r => r.Case)
       .ThenInclude(c => c.AiResult)
   .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null)
                throw new NotFoundException("Report not found");

            if (report.StudentId != studentId)
                throw new ForbiddenException("You are not allowed to view this feedback");

            var feedbackEntity = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(f => f.ReportId == reportId && f.StudentId == studentId);

            if (feedbackEntity is null)
                throw new NotFoundException("Feedback not found for this report");

            

            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.Id == feedbackEntity.Id)
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    f => f.SupervisorId,
                    u => u.Id,
                    (f, sup) => new { f, sup }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.StudentId,
                    stu => stu.Id,
                    (x, stu) => new FeedbackResponse
                    {
                        Id = x.f.Id,
                        ReportId = x.f.ReportId,
                            ReportCode = report.ReportCode,
                            PredictionResult = report.Case.AiResult != null
                                ? report.Case.AiResult.PredictionResult
                                : null,
                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = x.sup.FullName,
                        StudentId = x.f.StudentId,
                        StudentName = stu.FullName,
                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt,
                        IsSeen = x.f.IsSeen
                    }
                )
                .FirstOrDefaultAsync();

            return feedback!;
        }

        public async Task<List<FeedbackResponse>> GetBySupervisorAsync(string supervisorId)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var feedbacks = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.SupervisorId == supervisorId)
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    f => f.SupervisorId,
                    sup => sup.Id,
                    (f, sup) => new { f, sup }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.StudentId,
                    stu => stu.Id,
                    (x, stu) => new FeedbackResponse
                    {
                        Id = x.f.Id,
                        ReportId = x.f.ReportId,
                        ReportCode=x.f.Report.ReportCode,
                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = x.sup.FullName,
                        StudentId = x.f.StudentId,
                        StudentName = stu.FullName,
                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt
                    }
                )
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return feedbacks;
        }
        public async Task<(int TotalCount, int UnseenCount, List<StudentFeedbackNotificationResponse> Items)>
GetAllForStudentAsync(string studentId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            var baseQuery = _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.StudentId == studentId);

            var totalCount = await baseQuery.CountAsync(ct);
            var unseenCount = await baseQuery.CountAsync(f => !f.IsSeen, ct);

            var items = await baseQuery
                .Join(
                    _uow.Repo<Report>().Query(),
                    f => f.ReportId,
                    r => r.Id,
                    (f, r) => new { f, r }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.SupervisorId,
                    sup => sup.Id,
                    (x, sup) => new StudentFeedbackNotificationResponse
                    {
                        FeedbackId = x.f.Id,
                        ReportId = x.f.ReportId,
                        ReportCode = x.f.Report.ReportCode,
                        CaseId = x.r.CaseId,

                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = sup.FullName,

                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt,
                        IsSeen = x.f.IsSeen
                    }
                )
                .OrderBy(x => x.IsSeen)                // unseen first
                .ThenByDescending(x => x.CreatedAt)   // newest first
                .ToListAsync(ct);
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

            foreach (var item in items)
            {
                item.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(item.CreatedAt, tz);
            }
            

            return (totalCount, unseenCount, items);
        }

        public async Task<(int TotalCount, List<StudentFeedbackNotificationResponse> Items)>
GetUnseenForStudentAsync(string studentId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            var unseenBaseQuery = _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.StudentId == studentId && !f.IsSeen);

            var totalCount = await unseenBaseQuery.CountAsync(ct);

            var items = await unseenBaseQuery
                .Join(
                    _uow.Repo<Report>().Query(),
                    f => f.ReportId,
                    r => r.Id,
                    (f, r) => new { f, r }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.SupervisorId,
                    sup => sup.Id,
                    (x, sup) => new StudentFeedbackNotificationResponse
                    {
                        FeedbackId = x.f.Id,
                        ReportId = x.f.ReportId,
                        ReportCode = x.f.Report.ReportCode,
                        CaseId = x.r.CaseId,

                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = sup.FullName,

                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt,
                        IsSeen = x.f.IsSeen
                    }
                )
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

            foreach (var item in items)
            {
                item.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(item.CreatedAt, tz);
            }

            return (totalCount, items);
        }



        public async Task MarkSeenForStudentAsync(
            string studentId,
            Guid feedbackId,
            CancellationToken ct = default)
        {
            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(
                    f => f.Id == feedbackId && f.StudentId == studentId,
                    ct);

            if (feedback is null)
                throw new NotFoundException("Feedback not found");

            if (!feedback.IsSeen)
            {
                feedback.IsSeen = true;
                await _uow.SaveChangesAsync(ct);
            }
        }
        public async Task MarkAllSeenForStudentAsync(
    string studentId,
    CancellationToken ct = default)
        {
            var feedbacks = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.StudentId == studentId && !f.IsSeen)
                .ToListAsync(ct);

            if (!feedbacks.Any())
                return;

            foreach (var feedback in feedbacks)
                feedback.IsSeen = true;

            await _uow.SaveChangesAsync(ct);
        }
        public async Task<(int TotalCount, List<FeedbackResponse> Items)> GetAllForSupervisorAsync(
    string supervisorId,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var baseQuery = _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.SupervisorId == supervisorId);

            var totalCount = await baseQuery.CountAsync(ct);

            var items = await baseQuery
                .Join(
                    _uow.Repo<Report>().Query(),
                    f => f.ReportId,
                    r => r.Id,
                    (f, r) => new { f, r }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.SupervisorId,
                    sup => sup.Id,
                    (x, sup) => new { x.f, x.r, sup }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.StudentId,
                    stu => stu.Id,
                    (x, stu) => new FeedbackResponse
                    {
                        Id = x.f.Id,
                        ReportId = x.f.ReportId,
                        ReportCode = x.f.Report.ReportCode,

                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = x.sup.FullName,

                        StudentId = x.f.StudentId,
                        StudentName = stu.FullName,

                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt,
                        IsSeen = x.f.IsSeen,

                        ReportCreatedAt = x.r.SubmittedAt,
                        PredictionResult = x.r.Case.AiResult != null
                            ? x.r.Case.AiResult.PredictionResult
                            : null
                    }
                )
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

            foreach (var item in items)
            {
                item.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(item.CreatedAt, tz);
            }

            return (totalCount, items);
        }
        public async Task<FeedbackResponse> UpdateAsync(
    string supervisorId,
    Guid feedbackId,
    UpdateFeedbackRequest request,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(f => f.Id == feedbackId, ct);

            if (feedback is null)
                throw new NotFoundException("Feedback not found");

            if (feedback.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to update this feedback");

            feedback.Comment = request.Comment.Trim();
            feedback.IsSeen = false;

            _uow.Repo<Feedback>().Update(feedback);
            await _uow.SaveChangesAsync(ct);

            var updated = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.Id == feedbackId)
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    f => f.SupervisorId,
                    sup => sup.Id,
                    (f, sup) => new { f, sup }
                )
                .Join(
                    _uow.Repo<ApplicationUser>().Query(),
                    x => x.f.StudentId,
                    stu => stu.Id,
                    (x, stu) => new FeedbackResponse
                    {
                        Id = x.f.Id,
                        ReportId = x.f.ReportId,
                        ReportCode = x.f.Report.ReportCode,
                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = x.sup.FullName,

                        StudentId = x.f.StudentId,
                        StudentName = stu.FullName,

                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt
                    }
                )
                .FirstOrDefaultAsync(ct);

            if (updated is null)
                throw new NotFoundException("Feedback not found");

            // -----------------------------------------------------------
            // Real-time push: notify the student that the feedback they
            // received has been edited. Since UpdateAsync also flips IsSeen
            // back to false, the unseen count goes up — we send both pieces
            // so the bell badge and any open feedback view stay in sync.
            // -----------------------------------------------------------
            try
            {
                var report = await _uow.Repo<Report>()
                    .Query()
                    .Where(r => r.Id == feedback.ReportId)
                    .Select(r => new { r.ReportCode, r.CaseId })
                    .FirstOrDefaultAsync(ct);

                var unseenCount = await _uow.Repo<Feedback>()
                    .Query()
                    .CountAsync(f => f.StudentId == feedback.StudentId && !f.IsSeen, ct);

                var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

                var payload = new StudentFeedbackNotificationResponse
                {
                    FeedbackId = feedback.Id,
                    ReportId = feedback.ReportId,
                    ReportCode = report?.ReportCode,
                    CaseId = report?.CaseId ?? Guid.Empty,
                    SupervisorId = feedback.SupervisorId,
                    SupervisorName = updated.SupervisorName,
                    Comment = feedback.Comment,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(feedback.CreatedAt, tz),
                    IsSeen = feedback.IsSeen
                };

                await _notifications.NotifyStudentFeedbackUpdatedAsync(
                    feedback.StudentId,
                    payload,
                    unseenCount,
                    ct);
            }
            catch
            {
                // Best-effort.
            }

            return updated;
        }
        public async Task<string> DeleteAsync(
    string supervisorId,
    Guid feedbackId,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(f => f.Id == feedbackId, ct);

            if (feedback is null)
                throw new NotFoundException("Feedback not found");

            if (feedback.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to delete this feedback");

            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Case)
                .FirstOrDefaultAsync(r => r.Id == feedback.ReportId, ct);

            if (report is null)
                throw new NotFoundException("Related report not found");

            // Capture identifiers BEFORE removal so we can still push after save.
            var studentId = feedback.StudentId;
            var removedFeedbackId = feedback.Id;
            var removedReportId = feedback.ReportId;

            _uow.Repo<Feedback>().Remove(feedback);

            report.Case.Status = CaseStatus.Predicted;
            _uow.Repo<MriCase>().Update(report.Case);

            await _uow.SaveChangesAsync(ct);

            // -----------------------------------------------------------
            // Real-time push: tell the student the feedback was deleted
            // so any open menu/list can remove it immediately. We also
            // send a fresh unseen count since deleting an unseen feedback
            // would lower it.
            // -----------------------------------------------------------
            try
            {
                var unseenCount = await _uow.Repo<Feedback>()
                    .Query()
                    .CountAsync(f => f.StudentId == studentId && !f.IsSeen, ct);

                await _notifications.NotifyStudentFeedbackDeletedAsync(
                    studentId,
                    removedFeedbackId,
                    removedReportId,
                    unseenCount,
                    ct);
            }
            catch
            {
                // Best-effort.
            }

            return "Feedback deleted successfully";
        }



    }

}

    
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response.Feedback;
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

        public FeedbackService(IUnitOfWork uow)
        {
            _uow = uow;
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

            return "Feedback added successfully";
        }

        public async Task<FeedbackResponse> GetForSupervisorAsync(string supervisorId, Guid reportId)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Student)
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
                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = x.sup.FullName,
                        StudentId = x.f.StudentId,
                        StudentName = stu.FullName,
                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt
                    }
                )
                .FirstOrDefaultAsync();

            if (feedback is null)
                throw new NotFoundException("Feedback not found for this report");

            return feedback;
        }

        public async Task<FeedbackResponse> GetForStudentAsync(string studentId, Guid reportId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            var report = await _uow.Repo<Report>()
                .Query()
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

            if (!feedbackEntity.IsSeen)
            {
                feedbackEntity.IsSeen = true;
                await _uow.SaveChangesAsync();
            }

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

        public async Task<StudentNotificationsResult> GetAllForStudentAsync(
    string studentId,
    StudentFeedbacksQuery query,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            if (query.Page <= 0)
                query.Page = 1;

            if (query.PageSize <= 0)
                query.PageSize = 10;

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
                        CaseId = x.r.CaseId,
                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = sup.FullName,
                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt,
                        IsSeen = x.f.IsSeen
                    }
                )
                .OrderBy(x => x.IsSeen)
                .ThenByDescending(x => x.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return new StudentNotificationsResult
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                UnseenCount = unseenCount,
                Items = items
            };
        }
        public async Task<StudentNotificationsResult> GetUnseenForStudentAsync(
    string studentId,
    StudentFeedbacksQuery query,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            if (query.Page <= 0)
                query.Page = 1;

            if (query.PageSize <= 0)
                query.PageSize = 10;

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
                        CaseId = x.r.CaseId,
                        SupervisorId = x.f.SupervisorId,
                        SupervisorName = sup.FullName,
                        Comment = x.f.Comment,
                        CreatedAt = x.f.CreatedAt,
                        IsSeen = x.f.IsSeen
                    }
                )
                .OrderByDescending(x => x.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return new StudentNotificationsResult
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                UnseenCount = totalCount,
                Items = items
            };
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

    }

}

    
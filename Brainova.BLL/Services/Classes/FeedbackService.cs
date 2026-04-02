using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Classes;
using Brainova.DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

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
                throw new UnauthorizedAccessException("Supervisor is not authenticated");

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
                IsSeen = false
            };

            await _uow.Repo<Feedback>().AddAsync(feedback);

            report.Case.Status = CaseStatus.Reviewed;
            _uow.Repo<MriCase>().Update(report.Case);

            await _uow.SaveChangesAsync();

            return "Feedback added successfully";
        }

        public async Task<List<FeedbackResponse>> GetBySupervisorAsync(string supervisorId)
        {
            var feedbacks = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.SupervisorId == supervisorId)
                .Select(f => new FeedbackResponse
                {
                    Id = f.Id,
                    ReportId = f.ReportId,

                    SupervisorId = f.SupervisorId,
                    SupervisorName = f.Supervisor.FullName,

                    StudentId = f.StudentId,
                    StudentName = f.Student.FullName,

                    Comment = f.Comment,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            return feedbacks;
        }

        public async Task<List<FeedbackResponse>> GetForStudentAsync(string studentId, Guid reportId)
        {
            var report = await _uow.Repo<Report>()
                .Query()
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null)
                throw new NotFoundException("Report not found");

            if (report.StudentId != studentId)
                throw new ForbiddenException("You are not allowed to view this feedback");

            var data = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.ReportId == reportId && f.StudentId == studentId)
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
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return data;
        }
        public async Task<List<FeedbackResponse>> GetAllAsync()
        {
            var data = await _uow.Repo<Feedback>()
                .Query()
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
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return data;
        }

        public async Task<string> UpdateAsync(string? supervisorId, Guid feedbackId, UpdateFeedbackRequest request)
        {
            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(f => f.Id == feedbackId);

            if (feedback is null)
                throw new NotFoundException("Feedback not found");

            if (request is null || string.IsNullOrWhiteSpace(request.Comment))
                throw new BadRequestException("Comment is required");

            if (supervisorId != null && feedback.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to update this feedback");

            feedback.Comment = request.Comment.Trim();

            _uow.Repo<Feedback>().Update(feedback);
            await _uow.SaveChangesAsync();

            return "Feedback updated successfully";
        }

        public async Task<string> DeleteAsync(string? supervisorId, Guid feedbackId)
        {
            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(f => f.Id == feedbackId);

            if (feedback is null)
                throw new NotFoundException("Feedback not found");

            if (supervisorId != null && feedback.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to delete this feedback");

            _uow.Repo<Feedback>().Remove(feedback);
            await _uow.SaveChangesAsync();

            return "Feedback deleted successfully";
        }

        public async Task<List<FeedbackResponse>> GetByReportIdAsync(Guid reportId)
        {
            var data = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.ReportId == reportId)
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

            return data;
        }
        public async Task<List<FeedbackResponse>> GetUnseenForStudentAsync(string studentId)
        {
            var data = await _uow.Repo<Feedback>()
                .Query()
                .Where(f => f.StudentId == studentId && !f.IsSeen)
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
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return data;
        }
        public async Task<string> MarkAsSeenAsync(string studentId, Guid feedbackId)
        {
            var feedback = await _uow.Repo<Feedback>()
                .Query()
                .FirstOrDefaultAsync(f => f.Id == feedbackId);

            if (feedback is null)
                throw new NotFoundException("Feedback not found");

            if (feedback.StudentId != studentId)
                throw new ForbiddenException("You are not allowed to update this feedback");

            if (!feedback.IsSeen)
            {
                feedback.IsSeen = true;
                _uow.Repo<Feedback>().Update(feedback);
                await _uow.SaveChangesAsync();
            }

            return "Feedback marked as seen";
        }
    }
    }
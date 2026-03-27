using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
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

        public async Task<string> AddAsync(string supervisorId, CreateFeedbackRequest request)
        {
            var report = await _uow.Repo<Report>()
                .Query()
                .Include(r => r.Student)
                .Include(r => r.Case)
                .FirstOrDefaultAsync(r => r.Id == request.ReportId);

            if (report is null)
                throw new NotFoundException("Report not found");

            if (string.IsNullOrWhiteSpace(report.Student.SupervisorUserId) ||
                report.Student.SupervisorUserId != supervisorId)
            {
                throw new ForbiddenException("You are not allowed to add feedback to this report");
            }

            var feedback = new Feedback
            {
                ReportId = report.Id,
                SupervisorId = supervisorId,
                StudentId = report.StudentId,
                Comment = request.Comment
            };

            await _uow.Repo<Feedback>().AddAsync(feedback);

            report.Case.Status = CaseStatus.Reviewed;
            _uow.Repo<MriCase>().Update(report.Case);

            await _uow.SaveChangesAsync();

            return "Feedback added successfully";
        }

        public async Task<List<FeedbackResponse>> GetByReportIdAsync(Guid reportId)
        {
            var data = await _uow.Repo<Feedback>()
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
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return data;
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

            if (string.IsNullOrWhiteSpace(request.Comment))
                throw new BadRequestException("Comment is required");

            if (supervisorId != null && feedback.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to update this feedback");

            feedback.Comment = request.Comment;

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

      
    }
}
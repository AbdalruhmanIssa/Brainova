using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Brainova.BLL.Services.Classes
{
    public class DashboardService : IDashboardService
    {
        private readonly IUnitOfWork _uow;

        public DashboardService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<StudentDashboardSummaryResponse> GetStudentDashboardSummaryAsync(string studentId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new UnauthorizedException("Student is not authenticated");

            var totalCases = await _uow.Repo<MriCase>()
                .Query()
                .CountAsync(c => c.StudentId == studentId, ct);

            var reportsSubmitted = await _uow.Repo<Report>()
                .Query()
                .CountAsync(r => r.StudentId == studentId, ct);

            var predictionsReady = await _uow.Repo<MriCase>()
                .Query()
                .CountAsync(c =>
                    c.StudentId == studentId &&
                    (c.Status == CaseStatus.Predicted || c.Status == CaseStatus.Reviewed), ct);

            var feedbackReceived = await _uow.Repo<Feedback>()
                .Query()
                .CountAsync(f => f.StudentId == studentId, ct);

            return new StudentDashboardSummaryResponse
            {
                TotalCases = totalCases,
                ReportsSubmitted = reportsSubmitted,
            //    PredictionsReady = predictionsReady,
                FeedbackReceived = feedbackReceived
            };
        }

        public async Task<SupervisorDashboardSummaryResponse> GetSupervisorDashboardSummaryAsync(
            string supervisorId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(supervisorId))
                throw new UnauthorizedException("Supervisor is not authenticated");

            var totalStudents = await _uow.Repo<ApplicationUser>()
                .Query()
                .CountAsync(u => u.SupervisorUserId == supervisorId, ct);

            var totalReports = await _uow.Repo<Report>()
                .Query()
                .CountAsync(r => r.Student.SupervisorUserId == supervisorId, ct);

            var feedbackGiven = await _uow.Repo<Feedback>()
                .Query()
                .CountAsync(f => f.SupervisorId == supervisorId, ct);

            var newReports = totalReports - feedbackGiven;

            if (newReports < 0)
                newReports = 0;

            return new SupervisorDashboardSummaryResponse
            {
                TotalStudents = totalStudents,
                TotalReports = totalReports,
                NewReports = newReports,
                FeedbackGiven = feedbackGiven
            };
        }
    }
}
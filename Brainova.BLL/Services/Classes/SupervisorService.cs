using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Brainova.BLL.Services.Classes
{
    public class SupervisorService : ISupervisorService
    {
        private readonly IUnitOfWork _uow;

        public SupervisorService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<SupervisorDashboardSummaryResponse> GetDashboardSummaryAsync(
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
using Brainova.DAL.Modles;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Repositories.Interface
{
    public interface IUserRepository
    {
        Task<List<ApplicationUser>> GetAllAsync();
        Task<ApplicationUser?> GetByIdAsync(string userId);

        Task<bool> BlockUserAsync(string userId);
        Task<bool> UnBlockUserAsync(string userId);
        Task<bool> IsBlockedAsync(string userId);

        Task<(bool Success, string Message, string? OldRole)> ChangeUserRoleAsync(string userId, string roleName);

        // Create without password + assign role (Option A)
        Task<(bool Success, string Message, ApplicationUser? User)> CreateUserWithRoleAsync(ApplicationUser user, string roleName);
        Task<bool> AssignSupervisorAsync(string studentUserId, string supervisorUserId);
        Task<List<ApplicationUser>> GetStudentsOfSupervisorAsync(string supervisorUserId);
        Task<List<ApplicationUser>> GetSupervisorsAsync();
    }
}

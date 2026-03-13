using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.User;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface IUserService
    {
        Task<List<UserDTO>> GetAllAsync();
        Task<UpdateUserResponse?> GetByIdAsync(string userId);

        Task<bool> BlockUserAsync(string userId);
        Task<bool> UnBlockUserAsync(string userId);
        Task<bool> IsBlockedAsync(string userId);

        Task<bool> ChangeUserRoleAsync(string userId, string roleName);

        // Create
        Task<string> CreateSupervisorAsync(CreateUserRequest request, HttpRequest httpRequest);
        Task<string> CreateAdminAsync(CreateUserRequest request, HttpRequest httpRequest);
        Task<string> CreateStudentAsync(CreateUserRequest request, HttpRequest httpRequest);
        Task<List<SupervisorOptionResponse>> GetSupervisorsAsync();
        Task<string> AssignSupervisorAsync(AssignSupervisorRequest request);
        Task<List<UserDTO>> GetMyStudentsAsync(string supervisorUserId);
        Task<string> DeleteUserAsync(string userId);
        Task<BulkDeleteUsersResponse> DeleteUsersAsync(DeleteUsersRequest request);
        Task<UserDTO> UpdateUserAsync(string userId, UpdateUserRequest request);
        Task<string> ResetUserPasswordAsync(string userId, ChangeUserPasswordRequest request);


    }
}

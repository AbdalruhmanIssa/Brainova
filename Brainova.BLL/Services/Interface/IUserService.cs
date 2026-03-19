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
        //get
        Task<List<UserDTO>> GetAllAsync();
        Task<UserDTO?> GetByIdAsync(string userId);
        Task<List<SupervisorOptionResponse>> GetSupervisorsAsync();
        Task<List<UserDTO>> GetMyStudentsAsync(string supervisorUserId);


        // Block/Unblock
        Task<bool> BlockUserAsync(string userId);
        Task<bool> UnBlockUserAsync(string userId);
        Task<bool> IsBlockedAsync(string userId);


        // Create
        Task<string> CreateSupervisorAsync(CreateUserRequest request, HttpRequest httpRequest);
        Task<string> CreateAdminAsync(CreateUserRequest request, HttpRequest httpRequest);
        Task<string> CreateStudentAsync(CreateUserRequest request, HttpRequest httpRequest);

        // Update
        Task<UpdateUserResponse> UpdateUserAsync(string userId, UpdateUserRequest request);
        Task<string> ResetUserPasswordAsync(string userId, ChangeUserPasswordRequest request);

        Task<ChangeUserRoleResponse> ChangeUserRoleAsync(ChangeUserRoleRequest request);

        // Delete
        Task<string> DeleteUserAsync(string userId);
        Task<BulkDeleteUsersResponse> DeleteUsersAsync(DeleteUsersRequest request);

    }
}

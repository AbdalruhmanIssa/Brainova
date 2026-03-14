using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Repositories.Classes
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserRepository(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<ApplicationUser>> GetAllAsync()
            => await _userManager.Users.ToListAsync();

        public async Task<ApplicationUser?> GetByIdAsync(string userId)
            => await _userManager.FindByIdAsync(userId);

        public async Task<bool> BlockUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.IsBlocked = true;
            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public async Task<bool> UnBlockUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.IsBlocked = false;
            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public async Task<bool> IsBlockedAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return false;

            return user.IsBlocked;
        }

        public async Task<(bool Success, string Message, string? OldRole)> ChangeUserRoleAsync(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return (false, "User not found", null);

            var currentRoles = await _userManager.GetRolesAsync(user);
            var oldRole = currentRoles.FirstOrDefault();

            if (oldRole == roleName)
                return (false, $"User is already in role '{roleName}'", oldRole);

            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return (false, string.Join(";", removeResult.Errors.Select(e => e.Description)), oldRole);

            var addResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addResult.Succeeded)
                return (false, string.Join(";", addResult.Errors.Select(e => e.Description)), oldRole);

            // if user WAS student and is no longer student -> clear supervisor
            if (oldRole == "Student" && roleName != "Student")
            {
                user.SupervisorUserId = null;
                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                    return (false, string.Join(";", updateResult.Errors.Select(e => e.Description)), oldRole);
            }

            return (true, "Role changed successfully", oldRole);
        }

        public async Task<(bool Success, string Message, ApplicationUser? User)> CreateUserWithRoleAsync(ApplicationUser user, string roleName)
        {
            // Create WITHOUT password (Option A)
            var createRes = await _userManager.CreateAsync(user);
            if (!createRes.Succeeded)
                return (false, string.Join(";", createRes.Errors.Select(e => e.Description)), null);

            var roleRes = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleRes.Succeeded)
                return (false, string.Join(";", roleRes.Errors.Select(e => e.Description)), null);

            return (true, "Created", user);
        }
        public async Task<bool> AssignSupervisorAsync(string studentUserId, string supervisorUserId)
        {
            var student = await _userManager.FindByIdAsync(studentUserId);
            if (student is null) return false;

            student.SupervisorUserId = supervisorUserId;
            var res = await _userManager.UpdateAsync(student);
            return res.Succeeded;
        }

        public async Task<List<ApplicationUser>> GetStudentsOfSupervisorAsync(string supervisorUserId)
        {
            return await _userManager.Users
                .Where(u => u.SupervisorUserId == supervisorUserId)
                .ToListAsync();
        }

        public async Task<List<ApplicationUser>> GetSupervisorsAsync()
        {
            // Can't filter by role in Users table directly, so we do it in service using UserManager roles.
            // Here we return all users; service will filter supervisors properly OR you can join AspNetUserRoles via DbContext later.
            return await _userManager.Users.ToListAsync();
        }
    }
}

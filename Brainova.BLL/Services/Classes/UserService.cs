using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Brainova.BLL.DTOs.User;

namespace Brainova.BLL.Services.Classes
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public UserService(
            IUserRepository userRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public async Task<List<UserDTO>> GetAllAsync()
        {
            var users = await _userRepository.GetAllAsync();
            var list = new List<UserDTO>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);

                list.Add(new UserDTO
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber,
                    Email = u.Email,
                    EmailConfirmed = u.EmailConfirmed,
                    RoleName = roles.FirstOrDefault(),
                    IsBlocked = u.IsBlocked
                });
            }

            return list;
        }

        public async Task<UserDTO?> GetByIdAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            string? supervisorId = null;
            string? supervisorName = null;

            if (role == "Student")
            {
                if (!string.IsNullOrEmpty(user.SupervisorUserId))
                {
                    var supervisor = await _userManager.FindByIdAsync(user.SupervisorUserId);

                    supervisorId = supervisor?.Id;
                    supervisorName = supervisor?.FullName;
                }
            }

            return new UserDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                RoleName = role,
                IsBlocked = user.IsBlocked,

                // ✅ ضيفي هدول
                SupervisorId = supervisorId,
                SupervisorName = supervisorName
            };
        }

        public Task<bool> BlockUserAsync(string userId) => _userRepository.BlockUserAsync(userId);
        public Task<bool> UnBlockUserAsync(string userId) => _userRepository.UnBlockUserAsync(userId);
        public Task<bool> IsBlockedAsync(string userId) => _userRepository.IsBlockedAsync(userId);
        public Task<bool> ChangeUserRoleAsync(string userId, string roleName) => _userRepository.ChangeUserRoleAsync(userId, roleName);

        public Task<string> CreateSupervisorAsync(CreateUserRequest request, HttpRequest httpRequest)
            => CreateUserWithRoleAndSetPasswordEmailAsync(request, "Supervisor", httpRequest);

        public Task<string> CreateAdminAsync(CreateUserRequest request, HttpRequest httpRequest)
            => CreateUserWithRoleAndSetPasswordEmailAsync(request, "Admin", httpRequest);

        private async Task<string> CreateUserWithRoleAndSetPasswordEmailAsync(
            CreateUserRequest request,
            string roleName,
            HttpRequest httpRequest)
        {
            if (await _userManager.FindByEmailAsync(request.Email) != null)
                throw new BadRequestException("Email already exists");

            if (await _userManager.FindByNameAsync(request.UserName) != null)
                throw new BadRequestException("Username already exists");

            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                UserName = request.UserName,
                EmailConfirmed = true,
                IsBlocked = false
            };

            var (success, message, createdUser) = await _userRepository.CreateUserWithRoleAsync(user, roleName);
            if (!success || createdUser == null)
                throw new BadRequestException(message);

            var token = await _userManager.GeneratePasswordResetTokenAsync(createdUser);
            var tokenEscaped = Uri.EscapeDataString(token);

            // var link =
            //     $"{httpRequest.Scheme}://{httpRequest.Host}/api/Identity/Auths/set-password?userId={createdUser.Id}&token={tokenEscaped}";
            var link =
                   $"{httpRequest.Scheme}://{httpRequest.Host}/set-password.html?userId={createdUser.Id}&token={tokenEscaped}"; 
            await _emailSender.SendEmailAsync(
                createdUser.Email!,
                $"Brainova - Set your password ({roleName})",
                $"<h2>Hello {createdUser.UserName}</h2>" +
                $"<p>Your {roleName} account was created.</p>" +
                $"<p>Click below to set your password:</p>" +
                $"<a href='{link}'>Set Password</a>"
            );

            return $"{roleName} created successfully. Set-password email sent.";
        }

        public async Task<List<SupervisorOptionResponse>> GetSupervisorsAsync()
        {
            var users = await _userRepository.GetSupervisorsAsync();
            var result = new List<SupervisorOptionResponse>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (roles.Contains("Supervisor"))
                {
                    result.Add(new SupervisorOptionResponse
                    {
                        Id = u.Id,
                        FullName = u.FullName
                    });
                }
            }

            return result;
        }

        public async Task<string> AssignSupervisorAsync(AssignSupervisorRequest request)
        {
            var student = await _userManager.FindByIdAsync(request.StudentUserId);
            if (student is null) throw new NotFoundException("Student not found");

            var supervisor = await _userManager.FindByIdAsync(request.SupervisorUserId);
            if (supervisor is null) throw new NotFoundException("Supervisor not found");

            var studentRoles = await _userManager.GetRolesAsync(student);
            if (!studentRoles.Contains("Student"))
                throw new BadRequestException("Target user is not a Student");

            var supRoles = await _userManager.GetRolesAsync(supervisor);
            if (!supRoles.Contains("Supervisor"))
                throw new BadRequestException("Target user is not a Supervisor");

            var ok = await _userRepository.AssignSupervisorAsync(request.StudentUserId, request.SupervisorUserId);
            if (!ok) throw new BadRequestException("Assign failed");

            return "Assigned successfully";
        }

        public async Task<List<UserDTO>> GetMyStudentsAsync(string supervisorUserId)
        {
            var students = await _userRepository.GetStudentsOfSupervisorAsync(supervisorUserId);

            var dtos = new List<UserDTO>();
            foreach (var s in students)
            {
                dtos.Add(new UserDTO
                {
                    Id = s.Id,
                    FullName = s.FullName,
                    UserName = s.UserName ?? "",
                    Email = s.Email ?? "",
                    PhoneNumber = s.PhoneNumber ?? "",
                    EmailConfirmed = s.EmailConfirmed,
                    RoleName = "Student"
                });
            }

            return dtos;
        }

        public async Task<string> DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) throw new NotFoundException("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Supervisor"))
            {
                var hasStudents = await _userManager.Users.AnyAsync(u => u.SupervisorUserId == user.Id);
                if (hasStudents)
                    throw new BadRequestException("Can't delete supervisor: has assigned students");
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(";", result.Errors.Select(e => e.Description)));

            return "User deleted successfully";
        }
    }
}

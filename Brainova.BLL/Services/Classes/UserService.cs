using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.User;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace Brainova.BLL.Services.Classes
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IUnitOfWork _uow;
      

        public UserService(
            IUserRepository userRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IUnitOfWork uow)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _uow = uow;
        }

        public async Task<List<UserDTO>> GetAllAsync()
        {
            var users = await _userRepository.GetAllAsync();
            var list = new List<UserDTO>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var roleName = roles.FirstOrDefault() ?? "";

                string? supervisorName = null;
                string? supervisorId = null;

                if (roleName == "Student" && !string.IsNullOrWhiteSpace(u.SupervisorUserId))
                {
                    supervisorId = u.SupervisorUserId;

                    var supervisor = await _userManager.FindByIdAsync(u.SupervisorUserId);
                    supervisorName = supervisor?.FullName;
                }

                list.Add(new UserDTO
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    UserName = u.UserName ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    Email = u.Email ?? "",
                    EmailConfirmed = u.EmailConfirmed,
                    RoleName = roleName,
                    IsBlocked = u.IsBlocked,

                    SupervisorId = supervisorId,
                    SupervisorName = supervisorName
                });
            }

            return list;
        }

        public async Task<UpdateUserResponse?> GetByIdAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);

            return new UpdateUserResponse
            {
              
                FullName = user.FullName,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email
               
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
        public Task<string> CreateStudentAsync(CreateUserRequest request, HttpRequest httpRequest)
        
            => CreateUserWithRoleAndSetPasswordEmailAsync(request, "Student", httpRequest);



        private async Task<string> CreateUserWithRoleAndSetPasswordEmailAsync(
     CreateUserRequest request,
     string roleName,
     HttpRequest httpRequest)
        {
            if (await _userManager.FindByEmailAsync(request.Email) != null)
                throw new BadRequestException("Email already exists");

            if (await _userManager.FindByNameAsync(request.UserName) != null)
                throw new BadRequestException("Username already exists");

            if (roleName == "Student")
            {
                if (string.IsNullOrWhiteSpace(request.SupervisorUserId))
                    throw new BadRequestException("SupervisorUserId is required for student");

                var supervisor = await _userManager.FindByIdAsync(request.SupervisorUserId);
                if (supervisor is null)
                    throw new NotFoundException("Supervisor not found");

                var roles = await _userManager.GetRolesAsync(supervisor);
                if (!roles.Contains("Supervisor"))
                    throw new BadRequestException("Invalid supervisor");
            }

            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                UserName = request.UserName,
                EmailConfirmed = true,
                IsBlocked = false,
                SupervisorUserId = roleName == "Student" ? request.SupervisorUserId : null
            };

            var (success, message, createdUser) = await _userRepository.CreateUserWithRoleAsync(user, roleName);
            if (!success || createdUser == null)
                throw new BadRequestException(message);

            var token = await _userManager.GeneratePasswordResetTokenAsync(createdUser);
            var tokenEscaped = Uri.EscapeDataString(token);

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
        public async Task<UserDTO> UpdateUserAsync(string userId, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                throw new NotFoundException("User not found");

            // Check email uniqueness except current user
            var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
            if (existingByEmail != null && existingByEmail.Id != userId)
                throw new BadRequestException("Email already exists");

            // Check username uniqueness except current user
            var existingByUserName = await _userManager.FindByNameAsync(request.UserName);
            if (existingByUserName != null && existingByUserName.Id != userId)
                throw new BadRequestException("Username already exists");

            user.FullName = request.FullName;
            user.Email = request.Email;
            user.NormalizedEmail = _userManager.NormalizeEmail(request.Email);

            user.UserName = request.UserName;
            user.NormalizedUserName = _userManager.NormalizeName(request.UserName);

            user.PhoneNumber = request.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(";", result.Errors.Select(e => e.Description)));

            var roles = await _userManager.GetRolesAsync(user);

            return new UserDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                EmailConfirmed = user.EmailConfirmed,
                RoleName = roles.FirstOrDefault() ?? "",
                IsBlocked = user.IsBlocked
            };
        }
        public async Task<BulkDeleteUsersResponse> DeleteUsersAsync(DeleteUsersRequest request)
        {
            if (request.UserIds == null || !request.UserIds.Any())
                throw new BadRequestException("No user ids provided");

            var result = new BulkDeleteUsersResponse
            {
                Message = "Bulk delete completed"
            };

            foreach (var userId in request.UserIds.Distinct())
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    result.Failed.Add(new FailedUserResult
                    {
                        Id = userId,
                        UserName = null,
                        Reason = "User not found"
                    });
                    continue;
                }

                try
                {
                    var roles = await _userManager.GetRolesAsync(user);

                    if (roles.Contains("Supervisor"))
                    {
                        var hasStudents = await _userManager.Users
                            .AnyAsync(u => u.SupervisorUserId == user.Id);

                        if (hasStudents)
                        {
                            result.Failed.Add(new FailedUserResult
                            {
                                Id = user.Id,
                                UserName = user.UserName,
                                Reason = "students are assigned to this supervisor"
                            });
                            continue;
                        }
                    }

                    if (roles.Contains("Student"))
                    {
                        var hasCases = await _uow.Repo<MriCase>()
                            .Query()
                            .AnyAsync(c => c.StudentId == user.Id);

                        if (hasCases)
                        {
                            result.Failed.Add(new FailedUserResult
                            {
                                Id = user.Id,
                                UserName = user.UserName,
                                Reason = "student has MRI cases / related records"
                            });
                            continue;
                        }
                    }

                    var deleteResult = await _userManager.DeleteAsync(user);

                    if (!deleteResult.Succeeded)
                    {
                        result.Failed.Add(new FailedUserResult
                        {
                            Id = user.Id,
                            UserName = user.UserName,
                            Reason = string.Join(";", deleteResult.Errors.Select(e => e.Description))
                        });
                        continue;
                    }

                    result.Deleted.Add(new DeletedUserResult
                    {
                        Id = user.Id,
                        UserName = user.UserName
                    });
                }
                catch (Exception ex)
                {
                    result.Failed.Add(new FailedUserResult
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Reason = "Delete failed because of related data"
                    });
                }
            }

            return result;
        }
        public async Task<string> ResetUserPasswordAsync(string userId, ChangeUserPasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                throw new NotFoundException("User not found");

            var isSamePassword = await _userManager.CheckPasswordAsync(user, request.NewPassword);

            if (isSamePassword)
                throw new BadRequestException("New password must be different from the old password.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            // send email
            await _emailSender.SendEmailAsync(
                user.Email!,
                "Brainova Password Changed",
                $"""
        Hello {user.UserName},<br/>

        Your password has been reset by the Brainova administrator.<br/>

        Your new password is:
        {request.NewPassword}
        <br/>

        Brainova Team
        """
            );

            return "Password reset and email sent.";
        }
    }
}

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
        private readonly IHttpContextAccessor _httpContextAccessor;


        public UserService(
            IUserRepository userRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
        }
        //GET

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

        public async Task<UserDTO?> GetByIdAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;
            

            var roles = await _userManager.GetRolesAsync(user);
          
            var roleName = roles.FirstOrDefault() ?? "";

            string? supervisorName = null;
            string? supervisorId = null;

            if (roleName == "Student" && !string.IsNullOrWhiteSpace(user.SupervisorUserId))
            {
                supervisorId = user.SupervisorUserId;

                var supervisor = await _userManager.FindByIdAsync(user.SupervisorUserId);
                supervisorName = supervisor?.FullName;
            }

            return new UserDTO
            {


                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                Email = user.Email ?? "",
                EmailConfirmed = user.EmailConfirmed,
                RoleName = roleName,
                IsBlocked = user.IsBlocked,

                SupervisorId = supervisorId,
                SupervisorName = supervisorName

            };
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
        //Block/Unblock

        public Task<bool> BlockUserAsync(string userId) => _userRepository.BlockUserAsync(userId);
        public Task<bool> UnBlockUserAsync(string userId) => _userRepository.UnBlockUserAsync(userId);
        public Task<bool> IsBlockedAsync(string userId) => _userRepository.IsBlockedAsync(userId);

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
                EmailConfirmed = false,
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

        //Update
        public async Task<ChangeUserRoleResponse> ChangeUserRoleAsync(ChangeUserRoleRequest request)
        {
            var validRoles = new[] { "SuperAdmin", "Admin", "Supervisor", "Student" };

            if (!validRoles.Contains(request.RoleName))
                throw new BadRequestException("Invalid role name");

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user is null)
                throw new NotFoundException("User not found");

          //  var result = await _userRepository.ChangeUserRoleAsync(request.UserId, request.RoleName);
            var currentRoles = await _userManager.GetRolesAsync(user);
            var oldRole = currentRoles.FirstOrDefault();

            //  SAME ROLE return OK, no email no repo call
            if (oldRole == request.RoleName)
            {
                return new ChangeUserRoleResponse
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    UserName = user.UserName ?? "",
                    Email = user.Email ?? "",
                    OldRole = oldRole,
                    NewRole = request.RoleName,
                    IsChanged = false
                };
            }

            var result = await _userRepository.ChangeUserRoleAsync(
                request.UserId,
                request.RoleName
                );

            if (!result.Success)
                throw new BadRequestException(result.Message);

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Brainova - Role Updated",
                $"<h3>Hello {user.UserName}</h3>" +
                $"<p>Your account role has been changed.</p>" +
                $"<p><strong>Old Role:</strong> {result.OldRole ?? "None"}</p>" +
                $"<p><strong>New Role:</strong> {request.RoleName}</p>"
            );

            return new ChangeUserRoleResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                OldRole = result.OldRole,
                NewRole = request.RoleName,
                IsChanged = true
            };
        }
        public async Task<UpdateUserResponse> UpdateUserAsync(string userId, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                throw new NotFoundException("User not found");

            var currentUserId = _httpContextAccessor.HttpContext?.User.FindFirst("Id")?.Value;
            var isSelfUpdate = currentUserId == userId;

            if (!isSelfUpdate)
            {
                var currentUser = await _userManager.FindByIdAsync(currentUserId!);
                if (currentUser is null)
                    throw new UnauthorizedAccessException("Unauthorized");

                var currentUserRoles = await _userManager.GetRolesAsync(currentUser);

                if (!currentUserRoles.Contains("Admin") && !currentUserRoles.Contains("SuperAdmin"))
                    throw new ForbiddenException("You are not allowed to update other users");
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var isStudent = userRoles.Contains("Student");

            if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
                if (existingByEmail != null && existingByEmail.Id != userId)
                    throw new BadRequestException("Email already exists");
            }

            if (!string.Equals(user.UserName, request.UserName, StringComparison.OrdinalIgnoreCase))
            {
                var existingByUserName = await _userManager.FindByNameAsync(request.UserName);
                if (existingByUserName != null && existingByUserName.Id != userId)
                    throw new BadRequestException("Username already exists");
            }

            ApplicationUser? newSupervisor = null;

            if (isStudent)
            {
                if (string.IsNullOrWhiteSpace(request.SupervisorUserId))
                    throw new BadRequestException("Student must have a supervisor");

                newSupervisor = await _userManager.FindByIdAsync(request.SupervisorUserId);
                if (newSupervisor is null)
                    throw new NotFoundException("Supervisor not found");

                var supervisorRoles = await _userManager.GetRolesAsync(newSupervisor);
                if (!supervisorRoles.Contains("Supervisor"))
                    throw new BadRequestException("Target supervisor user is not a Supervisor");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.SupervisorUserId))
                    throw new BadRequestException("Supervisor can only be assigned to students");
            }

            var oldEmail = user.Email ?? string.Empty;
            var changes = new List<string>();

            bool TrackChange(string fieldName, string? oldValue, string? newValue, Action applyChange)
            {
                var oldNormalized = oldValue?.Trim() ?? string.Empty;
                var newNormalized = newValue?.Trim() ?? string.Empty;

                if (string.Equals(oldNormalized, newNormalized, StringComparison.Ordinal))
                    return false;

                changes.Add($"{fieldName} changed from: {oldValue ?? "(empty)"} to: {newValue ?? "(empty)"}");
                applyChange();
                return true;
            }

            TrackChange("Full Name", user.FullName, request.FullName, () =>
            {
                user.FullName = request.FullName;
            });

            TrackChange("Email", user.Email, request.Email, () =>
            {
                user.Email = request.Email;
                user.NormalizedEmail = _userManager.NormalizeEmail(request.Email);
            });

            TrackChange("User Name", user.UserName, request.UserName, () =>
            {
                user.UserName = request.UserName;
                user.NormalizedUserName = _userManager.NormalizeName(request.UserName);
            });

            TrackChange("Phone Number", user.PhoneNumber, request.PhoneNumber, () =>
            {
                user.PhoneNumber = request.PhoneNumber;
            });

            if (isStudent)
            {
                string oldSupervisorName = "(unknown)";

                if (!string.IsNullOrWhiteSpace(user.SupervisorUserId))
                {
                    var oldSupervisor = await _userManager.FindByIdAsync(user.SupervisorUserId);
                    oldSupervisorName = oldSupervisor?.FullName ?? "(unknown)";
                }

                if (!string.Equals(user.SupervisorUserId, request.SupervisorUserId, StringComparison.Ordinal))
                {
                    changes.Add($"Supervisor changed from: {oldSupervisorName} to: {newSupervisor!.FullName}");
                    user.SupervisorUserId = request.SupervisorUserId;
                }
            }

            var role = userRoles.FirstOrDefault() ?? "";

            if (!changes.Any())
            {
                return new UpdateUserResponse
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    UserName = user.UserName!,
                    RoleName = role,
                    PhoneNumber = user.PhoneNumber!,
                    SupervisorId = user.SupervisorUserId,
                    IsChanged = false
                };
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(";", result.Errors.Select(e => e.Description)));

            if (!isSelfUpdate)
            {
                await SendUserUpdatedEmailAsync(user, oldEmail, changes);
            }

            return new UpdateUserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                UserName = user.UserName!,
                RoleName = role,
                PhoneNumber = user.PhoneNumber!,
                SupervisorId = user.SupervisorUserId,
                IsChanged = true
            };
        }


        private bool TrackChange(
    List<string> changes,
    string fieldName,
    string? oldValue,
    string? newValue,
    Action applyChange)
        {
            var oldNormalized = oldValue?.Trim() ?? string.Empty;
            var newNormalized = newValue?.Trim() ?? string.Empty;

            if (string.Equals(oldNormalized, newNormalized, StringComparison.Ordinal))
                return false;

            changes.Add($"{fieldName} changed from: {oldValue ?? "(empty)"} to: {newValue ?? "(empty)"}");
            applyChange();
            return true;
        }
        private async Task SendUserUpdatedEmailAsync(
    ApplicationUser user,
    string oldEmail,
    List<string> changes)
        {
            if (!changes.Any())
                return;

            var changesHtml = string.Join("", changes.Select(c => $"<li>{c}</li>"));

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Brainova - Account Updated",
                $"""
        <h2>Hello {user.FullName}</h2>
        <p>Your Brainova account information was updated by the administration.</p>
        <p>The following changes were made:</p>
        <ul>
            {changesHtml}
        </ul>
        <p>If this was unexpected, please contact support immediately.</p>
        <br/>
        Brainova Team
        """
            );

            if (!string.Equals(oldEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                await _emailSender.SendEmailAsync(
                    oldEmail,
                    "Brainova - Email Changed",
                    $"""
            <h2>Security Notice</h2>
            <p>Your Brainova account email was changed.</p>
            <p>New email address:</p>
            <b>{user.Email}</b>
            <p>If this change was not authorized, please contact support immediately.</p>
            <br/>
            Brainova Security
            """
                );
            }
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
        //Delete
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

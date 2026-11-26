using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.DTOs.Realtime;
using Brainova.BLL.DTOs.Response;
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
        private readonly IReportQuestionService _reportQuestionService;
        private readonly INotificationService _notifications;


        public UserService(
            IUserRepository userRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor,
            IReportQuestionService reportQuestionService,
            INotificationService notifications)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
            _reportQuestionService = reportQuestionService;
            _notifications = notifications;
        }
        /// <summary>
        /// Returns the id of the admin/super-admin currently performing the
        /// request (or null if none — e.g. background flows). Used to enrich
        /// the "UserListChanged" SignalR payload with "byUserId".
        /// </summary>
        private string? GetActingUserId()
            => _httpContextAccessor.HttpContext?.User?.FindFirst("Id")?.Value;

        //GET

        public async Task<List<UserDTO>> GetAllAsync()
        {
            // ----------------------------------------------------------------
            // Optimized: 3 queries total instead of 1 + N (per-user GetRoles)
            // + M (per-student supervisor lookup). With ~50 users this was
            // ~80 round-trips taking 3-5s on Azure SQL; now it's ~150ms.
            // ----------------------------------------------------------------

            // (1) all users
            var users = await _userRepository.GetAllAsync();
            if (users.Count == 0) return new List<UserDTO>();

            var userIds = users.Select(u => u.Id).ToList();

            // (2) all (userId, roleName) mappings in one JOIN — covers every user.
            //     Replaces the per-user _userManager.GetRolesAsync call.
            var userRolePairs = await (
                from ur in _uow.Repo<IdentityUserRole<string>>().Query()
                join r in _uow.Repo<IdentityRole>().Query() on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, RoleName = r.Name }
            ).ToListAsync();

            var rolesByUserId = userRolePairs
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.RoleName ?? string.Empty)
                          .Where(n => !string.IsNullOrWhiteSpace(n))
                          .ToList());

            // (3) supervisor names — one batch query for all students who have one.
            //     Replaces the per-student _userManager.FindByIdAsync call.
            var supervisorIds = users
                .Where(u =>
                    !string.IsNullOrWhiteSpace(u.SupervisorUserId) &&
                    rolesByUserId.TryGetValue(u.Id, out var rs) &&
                    rs.Contains("Student"))
                .Select(u => u.SupervisorUserId!)
                .Distinct()
                .ToList();

            var supervisorNamesById = supervisorIds.Count == 0
                ? new Dictionary<string, string>()
                : await _uow.Repo<ApplicationUser>()
                    .Query()
                    .Where(u => supervisorIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName);

            // (4) assemble DTOs in-memory — no more DB calls.
            return users.Select(u =>
            {
                var roleName = rolesByUserId.TryGetValue(u.Id, out var rs)
                    ? rs.FirstOrDefault() ?? string.Empty
                    : string.Empty;

                string? supervisorId = null;
                string? supervisorName = null;

                if (roleName == "Student" && !string.IsNullOrWhiteSpace(u.SupervisorUserId))
                {
                    supervisorId = u.SupervisorUserId;
                    supervisorNamesById.TryGetValue(u.SupervisorUserId, out supervisorName);
                }

                return new UserDTO
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
                };
            }).ToList();
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



 
public async Task<List<SupervisorStudentListItemResponse>> GetSupervisorStudentsAsync(string supervisorUserId)
    {
        var students = await _userManager.Users
            .Where(u => u.SupervisorUserId == supervisorUserId)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        var reportCounts = await _uow.Repo<Report>()
            .Query()
            .Where(r => studentIds.Contains(r.StudentId))
            .GroupBy(r => r.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        return students.Select(s => new SupervisorStudentListItemResponse
        {
            StudentId = s.Id,
            FullName = s.FullName,
            UserName = s.UserName ?? "",
            Email = s.Email ?? "",
            PhoneNumber = s.PhoneNumber ?? "",
            ReportsCount = reportCounts.FirstOrDefault(x => x.StudentId == s.Id)?.Count ?? 0
        }).ToList();
    }

           

        //Block/Unblock

        public async Task<bool> BlockUserAsync(string userId)
        {
            // Look up the user BEFORE the block so we can tell their supervisor
            // (if they're a student) that their student list changed.
            var user = await _userManager.FindByIdAsync(userId);

            var success = await _userRepository.BlockUserAsync(userId);

            // -----------------------------------------------------------
            // Real-time pushes (SignalR):
            //  1. Tell the user → frontend force-logouts.
            //  2. Tell admins → user-management table updates live.
            //  3. If a student got blocked → tell their supervisor too
            //     (their /Supervisor/Students list might show isBlocked).
            //
            // Best-effort: a notification failure must not roll back the
            // block — the user IS blocked in the DB regardless.
            // -----------------------------------------------------------
            if (success)
            {
                try
                {
                    await _notifications.NotifyUserBlockedAsync(userId);
                    await _notifications.NotifyAdminsUserListChangedAsync(new UserListChangePayload
                    {
                        Kind = UserListChangeKinds.Blocked,
                        UserId = userId,
                        UserName = user?.UserName,
                        FullName = user?.FullName,
                        ByUserId = GetActingUserId()
                    });

                    if (user != null && !string.IsNullOrWhiteSpace(user.SupervisorUserId))
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains("Student"))
                        {
                            await _notifications.NotifySupervisorStudentsChangedAsync(
                                user.SupervisorUserId);
                        }
                    }
                }
                catch
                {
                    // Best-effort.
                }
            }

            return success;
        }
        public async Task<bool> UnBlockUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var success = await _userRepository.UnBlockUserAsync(userId);

            // Symmetric with BlockUserAsync.
            if (success)
            {
                try
                {
                    await _notifications.NotifyUserUnblockedAsync(userId);
                    await _notifications.NotifyAdminsUserListChangedAsync(new UserListChangePayload
                    {
                        Kind = UserListChangeKinds.Unblocked,
                        UserId = userId,
                        UserName = user?.UserName,
                        FullName = user?.FullName,
                        ByUserId = GetActingUserId()
                    });

                    if (user != null && !string.IsNullOrWhiteSpace(user.SupervisorUserId))
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains("Student"))
                        {
                            await _notifications.NotifySupervisorStudentsChangedAsync(
                                user.SupervisorUserId);
                        }
                    }
                }
                catch
                {
                    // Best-effort.
                }
            }

            return success;
        }
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

            if (roleName == "Supervisor")
            {
                await _reportQuestionService.SeedSystemQuestionsForSupervisorAsync(createdUser.Id);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(createdUser);
            var tokenEscaped = Uri.EscapeDataString(token);

            var link =
    $"https://brainovaproject.onrender.com/auth/set-password?userId={createdUser.Id}&token={tokenEscaped}";

            await _emailSender.SendEmailAsync(
                createdUser.Email!,
                $"Brainova - Set your password ({roleName})",
                $"<h2>Hello {createdUser.UserName}</h2>" +
                $"<p>Your {roleName} account was created.</p>" +
                $"<p>Click below to set your password:</p>" +
                $"<a href='{link}'>Set Password</a>"
            );

            // Real-time push: broadcast to admins so their user-management
            // table updates without manual refresh. If a Student was created
            // and assigned to a supervisor, also tell that supervisor so their
            // "my students" list gains the new row.
            try
            {
                await _notifications.NotifyAdminsUserListChangedAsync(new UserListChangePayload
                {
                    Kind = UserListChangeKinds.Created,
                    UserId = createdUser.Id,
                    UserName = createdUser.UserName,
                    FullName = createdUser.FullName,
                    Role = roleName,
                    ByUserId = GetActingUserId()
                });

                if (roleName == "Student" &&
                    !string.IsNullOrWhiteSpace(createdUser.SupervisorUserId))
                {
                    await _notifications.NotifySupervisorStudentsChangedAsync(
                        createdUser.SupervisorUserId);
                }
            }
            catch
            {
                // Best-effort.
            }

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

            // -----------------------------------------------------------
            // Real-time pushes:
            //  1. Tell the affected user — frontend should re-fetch user +
            //     re-route to the dashboard matching the new role.
            //  2. Tell admins — the user list table / supervisor dropdown
            //     should refresh.
            // -----------------------------------------------------------
            try
            {
                await _notifications.NotifyUserRoleChangedAsync(
                    user.Id,
                    result.OldRole,
                    request.RoleName);

                await _notifications.NotifyAdminsUserListChangedAsync(new UserListChangePayload
                {
                    Kind = UserListChangeKinds.RoleChanged,
                    UserId = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Role = request.RoleName,
                    OldRole = result.OldRole,
                    ByUserId = GetActingUserId()
                });

                // If a Student got changed to anything else, their old supervisor
                // loses them from /Supervisor/Students — tell that supervisor.
                if (oldRole == "Student" &&
                    request.RoleName != "Student" &&
                    !string.IsNullOrWhiteSpace(user.SupervisorUserId))
                {
                    await _notifications.NotifySupervisorStudentsChangedAsync(
                        user.SupervisorUserId);
                }
            }
            catch
            {
                // Best-effort.
            }

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

            // Capture the supervisor id BEFORE any mutation so we can push to
            // both old + new supervisors after the save commits.
            var oldSupervisorUserId = user.SupervisorUserId;

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

            // -----------------------------------------------------------
            // Real-time pushes:
            //  1. Tell the user their profile changed (frontend re-fetches user).
            //     Even on self-updates we push so other tabs of the same user sync.
            //  2. If this is a student whose supervisor was reassigned, tell the
            //     old supervisor (their student list lost a row) and the new
            //     supervisor (their student list gained a row).
            //  3. Tell admins — the user-management table needs to refresh.
            // -----------------------------------------------------------
            try
            {
                await _notifications.NotifyUserUpdatedAsync(user.Id);

                var supervisorChanged = isStudent &&
                    !string.Equals(oldSupervisorUserId, user.SupervisorUserId, StringComparison.Ordinal);

                if (supervisorChanged)
                {
                    // Reassignment: old supervisor lost a student, new gained one.
                    if (!string.IsNullOrWhiteSpace(oldSupervisorUserId))
                    {
                        await _notifications.NotifySupervisorStudentsChangedAsync(oldSupervisorUserId);
                    }
                    if (!string.IsNullOrWhiteSpace(user.SupervisorUserId))
                    {
                        await _notifications.NotifySupervisorStudentsChangedAsync(user.SupervisorUserId);
                    }
                }
                else if (isStudent && !string.IsNullOrWhiteSpace(user.SupervisorUserId))
                {
                    // Same supervisor, but the student's info (name/email/etc.) may
                    // have changed — the supervisor's list shows that info, so push.
                    await _notifications.NotifySupervisorStudentsChangedAsync(user.SupervisorUserId);
                }

                // Always broadcast to admins+superadmins — even for self-updates.
                // A supervisor editing their own info should still be visible to
                // admins/superadmins managing the user list.
                await _notifications.NotifyAdminsUserListChangedAsync(new UserListChangePayload
                {
                    Kind = UserListChangeKinds.Updated,
                    UserId = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Role = role,
                    ByUserId = GetActingUserId()
                });
            }
            catch
            {
                // Best-effort.
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

            // Captured once so we don't re-read it every iteration. Used inside
            // the loop to tag each per-user push with the acting admin.
            var actingUserId = GetActingUserId();

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

                        // Clean up the supervisor's questions before deleting the user.
                        // Questions with no answers can be hard-deleted.
                        // Questions that have been answered get orphaned (SupervisorId = null)
                        // so historical reports remain intact.
                        var supervisorQuestions = await _uow.Repo<ReportQuestion>()
                            .Query()
                            .Where(q => q.SupervisorId == user.Id)
                            .ToListAsync();

                        if (supervisorQuestions.Any())
                        {
                            var questionIds = supervisorQuestions.Select(q => q.Id).ToList();

                            var answeredQuestionIds = await _uow.Repo<ReportAnswer>()
                                .Query()
                                .Where(a => questionIds.Contains(a.QuestionId))
                                .Select(a => a.QuestionId)
                                .Distinct()
                                .ToListAsync();

                            foreach (var q in supervisorQuestions)
                            {
                                if (answeredQuestionIds.Contains(q.Id))
                                {
                                    q.SupervisorId = null;
                                    _uow.Repo<ReportQuestion>().Update(q);
                                }
                                else
                                {
                                    _uow.Repo<ReportQuestion>().Remove(q);
                                }
                            }

                            await _uow.SaveChangesAsync();
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

                    // Push IMMEDIATELY (inside the loop) so admin tables update
                    // progressively instead of waiting for the whole bulk to finish.
                    // For a bulk of N users, this turns a single ~10s wait into N
                    // smaller refreshes spread across the operation.
                    var snapshot = new UserListChangePayload
                    {
                        Kind = UserListChangeKinds.Deleted,
                        UserId = user.Id,
                        UserName = user.UserName,
                        FullName = user.FullName,
                        Role = roles.FirstOrDefault(),
                        ByUserId = actingUserId
                    };

                    try
                    {
                        await _notifications.NotifyAdminsUserListChangedAsync(snapshot);

                        // If the deleted user was a Student, tell their supervisor too.
                        if (roles.Contains("Student") &&
                            !string.IsNullOrWhiteSpace(user.SupervisorUserId))
                        {
                            await _notifications.NotifySupervisorStudentsChangedAsync(
                                user.SupervisorUserId);
                        }
                    }
                    catch
                    {
                        // Best-effort.
                    }
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

            // Per-user pushes already happened inside the loop above
            // (progressive UI updates).
            return result;
        }

        

       
    }
}

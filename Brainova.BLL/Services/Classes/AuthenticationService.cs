using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Brainova.BLL.Services.Classes
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthenticationService(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IEmailSender emailSender,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _configuration = configuration;
            _emailSender = emailSender;
            _signInManager = signInManager;
        }

        public async Task<UserResponse> LoginAsync(LoginRequest request)
        {
            var user =
                await _userManager.FindByEmailAsync(request.EmailOrUserName)
                ?? await _userManager.FindByNameAsync(request.EmailOrUserName);

            // ✅ Security: لا تكشف إذا المستخدم موجود أو لا
            if (user is null)
                throw new BadRequestException("Invalid email/username or password");

            if (user.IsBlocked)
                throw new ForbiddenException("Your account is blocked");

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);

            if (result.Succeeded)
            {
                return new UserResponse
                {
                    Token = await CreateTokenAsync(user)
                };
            }

            if (result.IsLockedOut)
                throw new ForbiddenException("Your account is locked");

            if (result.IsNotAllowed)
                throw new BadRequestException("Please confirm your email");

            throw new BadRequestException("Invalid email/username or password");
        }

        public async Task<string> RegisterStudentAsync(RegisterStudentRequest request, HttpRequest httpRequest)
        {
            // 1) Uniqueness checks
            if (await _userManager.FindByEmailAsync(request.Email) != null)
                throw new BadRequestException("Email already exists");

            if (await _userManager.FindByNameAsync(request.UserName) != null)
                throw new BadRequestException("Username already exists");

            // 2) Validate Supervisor
            var supervisor = await _userManager.FindByIdAsync(request.SupervisorUserId);
            if (supervisor is null)
                throw new NotFoundException("Supervisor not found");

            var supRoles = await _userManager.GetRolesAsync(supervisor);
            if (!supRoles.Contains("Supervisor"))
                throw new BadRequestException("Invalid supervisor");

            // 3) Create Student
            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                UserName = request.UserName,
                EmailConfirmed = false,
                IsBlocked = false,
                SupervisorUserId = request.SupervisorUserId
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
                throw new BadRequestException(string.Join(";", createResult.Errors.Select(e => e.Description)));

            var roleResult = await _userManager.AddToRoleAsync(user, "Student");
            if (!roleResult.Succeeded)
                throw new BadRequestException(string.Join(";", roleResult.Errors.Select(e => e.Description)));


            // 4) Email confirmation
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var escaped = Uri.EscapeDataString(token);

            var confirmUrl =
                $"{httpRequest.Scheme}://{httpRequest.Host}/api/Identity/Auths/confirm-email?token={escaped}&userId={user.Id}";

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Brainova - Confirm your email",
                $"<h2>Hello {user.UserName}</h2>" +
                $"<p>Please confirm your email:</p>" +
                $"<a href='{confirmUrl}'>Confirm Email</a>"
            );

            return "Registration successful. Please check your email to confirm your account.";
        }

        public async Task<string> ConfirmEmailAsync(string token, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                throw new NotFoundException("User not found");

            var decodedToken = Uri.UnescapeDataString(token);
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded)
                throw new BadRequestException("Email confirmation failed");

            return "Email confirmed successfully";
        }

        public async Task<bool> ForgotPasswordAsync(ForgetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
                throw new NotFoundException("User not found");

            // 4-digit code
            var random = new Random();
            var code = random.Next(1000, 9999).ToString();

            user.CodeResetPassword = code;
            user.CodeResetPasswordExpire = DateTime.UtcNow.AddMinutes(15);

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new BadRequestException(string.Join(";", updateResult.Errors.Select(e => e.Description)));

            await _emailSender.SendEmailAsync(
                request.Email,
                "Brainova - Reset password",
                $"<p>Your reset code is: <strong>{code}</strong></p>" +
                "<p>This code expires in 15 minutes.</p>"
            );


            return true;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
                throw new NotFoundException("User not found");

            if (user.CodeResetPassword != request.Code)
                throw new BadRequestException("Invalid reset code");

            if (!user.CodeResetPasswordExpire.HasValue || user.CodeResetPasswordExpire < DateTime.UtcNow)
                throw new BadRequestException("Reset code expired");

            // ✅ NEW CHECK HERE
            var isSamePassword = await _userManager.CheckPasswordAsync(user, request.NewPassword);
            if (isSamePassword)
                throw new BadRequestException("New password must be different from the old password.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (!result.Succeeded)
                throw new BadRequestException(string.Join(",", result.Errors.Select(e => e.Description)));

            // clear code
            user.CodeResetPassword = null;
            user.CodeResetPasswordExpire = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new BadRequestException(string.Join(",", updateResult.Errors.Select(e => e.Description)));

            await _emailSender.SendEmailAsync(
                request.Email,
                "Brainova - Password changed",
                "<h3>Your password has been changed successfully.</h3>"
            );

            return true;
        }

        public async Task<string> SetPasswordAsync(SetPasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user is null)
                throw new NotFoundException("User not found");

            var decodedToken = Uri.UnescapeDataString(request.Token);
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);

            if (!result.Succeeded)
                throw new BadRequestException(string.Join(";", result.Errors.Select(e => e.Description)));

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Brainova - Password set",
                "<p>Your password has been set successfully. You can now login.</p>"
            );

            return "Password set successfully.";
        }

        private async Task<string> CreateTokenAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
{
    new Claim("Name", user.UserName ?? ""),
    new Claim("FullName", user.FullName ?? ""),
    new Claim("Email", user.Email ?? ""),
    new Claim("PhoneNumber", user.PhoneNumber ?? ""),
    new Claim("Id", user.Id)
};

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                claims.Add(new Claim("Role", role));

            var jwtSection = _configuration.GetSection("jwtOptions");
            var secretBase64 = jwtSection["SecretKey"] ?? throw new Exception("jwtOptions:SecretKey missing");

            var keyBytes = Convert.FromBase64String(secretBase64);
            var securityKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var issuer = jwtSection["Issuer"];
            var audience = jwtSection["Audience"];
            var minutes = double.TryParse(jwtSection["DurationInMinutes"], out var m) ? m : 60;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(minutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

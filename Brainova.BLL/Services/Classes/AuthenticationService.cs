using Brainova.BLL.DTOs.Auth;
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
            // email OR username
            ApplicationUser? user =
                await _userManager.FindByEmailAsync(request.EmailOrUserName)
                ?? await _userManager.FindByNameAsync(request.EmailOrUserName);

            if (user is null)
                throw new Exception("Invalid email/username or password");

            if (user.IsBlocked)
                throw new Exception("Your account is blocked");

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);

            if (result.Succeeded)
            {
                return new UserResponse
                {
                    Token = await CreateTokenAsync(user)
                };
            }
            else if (result.IsLockedOut)
            {
                throw new Exception("Your account is locked");
            }
            else if (result.IsNotAllowed)
            {
                // happens if RequireConfirmedEmail = true and EmailConfirmed = false
                throw new Exception("Please confirm your email");
            }
            else
            {
                throw new Exception("Invalid email/username or password");
            }
        }

        // Student self-register -> sends confirm email link
        public async Task<string> RegisterStudentAsync(RegisterStudentRequest request, HttpRequest httpRequest)
        {
            // 1️⃣ Basic duplicate checks
            if (await _userManager.FindByEmailAsync(request.Email) != null)
                throw new Exception("Email already exists");

            if (await _userManager.FindByNameAsync(request.UserName) != null)
                throw new Exception("Username already exists");

            // 2️⃣ 🔥 Validate Supervisor (INSERTED HERE)
            var supervisor = await _userManager.FindByIdAsync(request.SupervisorUserId);
            if (supervisor is null)
                throw new Exception("Supervisor not found");

            var supRoles = await _userManager.GetRolesAsync(supervisor);
            if (!supRoles.Contains("Supervisor"))
                throw new Exception("Invalid supervisor");

            // 3️⃣ Create student
            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                UserName = request.UserName,
                EmailConfirmed = false,
                IsBlocked = false,
                SupervisorUserId = request.SupervisorUserId // ✅ SAVE RELATION
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(";", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Student");

            // 4️⃣ Email confirmation
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
                throw new Exception("User not found");

            // token comes URL-escaped
            var decodedToken = Uri.UnescapeDataString(token);

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            return result.Succeeded ? "Email confirmed successfully" : "Email confirmation failed";
        }

        public async Task<bool> ForgotPasswordAsync(ForgetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null) throw new Exception("User not found");

            // Silverhand-style 4-digit code
            var random = new Random();
            var code = random.Next(1000, 9999).ToString();

            user.CodeResetPassword = code;
            user.CodeResetPasswordExpire = DateTime.UtcNow.AddMinutes(15);

            await _userManager.UpdateAsync(user);

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
            if (user is null) throw new Exception("User not found");

            if (user.CodeResetPassword != request.Code) return false;
            if (!user.CodeResetPasswordExpire.HasValue || user.CodeResetPasswordExpire < DateTime.UtcNow) return false;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (result.Succeeded)
            {
                // clear code
                user.CodeResetPassword = null;
                user.CodeResetPasswordExpire = null;
                await _userManager.UpdateAsync(user);

                await _emailSender.SendEmailAsync(
                    request.Email,
                    "Brainova - Password changed",
                    "<h3>Your password has been changed successfully.</h3>"
                );
            }

            return result.Succeeded;
        }

        

    

        private async Task<string> CreateTokenAsync(ApplicationUser user)
        {
            // Keep your Silverhand claim names ("Role" too)
            var claims = new List<Claim>
            {
                new Claim("Name", user.UserName ?? ""),
                new Claim("Email", user.Email ?? ""),
                new Claim("Id", user.Id),
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                claims.Add(new Claim("Role", role));

            var jwtSection = _configuration.GetSection("jwtOptions");

            // Base64 secret (your choice)
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

        public async Task<string> SetPasswordAsync(SetPasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user is null)
                throw new Exception("User not found");

            var decodedToken = Uri.UnescapeDataString(request.Token);

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);

            if (!result.Succeeded)
                throw new Exception(string.Join(";", result.Errors.Select(e => e.Description)));

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Brainova - Password set",
                "<p>Your password has been set successfully. You can now login.</p>"
            );

            return "Password set successfully.";
        }

    }
}


using Brainova.BLL.DTOs.Auth;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Brainova.BLL.Services.Interface
{
    public interface IAuthenticationService
    {
        Task<UserResponse> LoginAsync(LoginRequest request);

        // ✅ New: cookie-based login. Returns token (controller sets cookie) + user info for body.
        Task<(string Token, CurrentUserResponse User, DateTime ExpiresUtc)> LoginForCookieAsync(LoginRequest request);

        // ✅ New: returns fresh info about the currently authenticated user.
        Task<CurrentUserResponse> GetCurrentUserAsync(ClaimsPrincipal principal);

        Task<string> RegisterStudentAsync(RegisterStudentRequest request, HttpRequest httpRequest);
        Task<string> ConfirmEmailAsync(string token, string userId);

        Task<bool> ForgotPasswordAsync(ForgetPasswordRequest request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequest request);


        // ✅ New: set password via link
        Task<string> SetPasswordAsync(SetPasswordRequest request);

    }
}

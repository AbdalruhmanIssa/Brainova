
using Brainova.BLL.DTOs.Auth;
using Microsoft.AspNetCore.Http;

namespace Brainova.BLL.Services.Interface
{
    public interface IAuthenticationService
    {
        Task<UserResponse> LoginAsync(LoginRequest request);

        Task<string> RegisterStudentAsync(RegisterStudentRequest request, HttpRequest httpRequest);
        Task<string> ConfirmEmailAsync(string token, string userId);

        Task<bool> ForgotPasswordAsync(ForgetPasswordRequest request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequest request);

       
        // ✅ New: set password via link
        Task<string> SetPasswordAsync(SetPasswordRequest request);

    }
}

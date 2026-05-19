
using Brainova.BLL.DTOs.Auth;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brainova.PL.Areas.Identity.Controllers
{
    [Area("Identity")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AuthsController : ControllerBase
    {
        // Single source of truth for the cookie name. Keep in sync with Program.cs JwtBearer fallback.
        public const string AccessTokenCookieName = "access_token";

        private readonly IAuthenticationService _authService;
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _env;
        private readonly IAntiforgery _antiforgery;

        public AuthsController(
            IAuthenticationService authService,
            IUserService userService,
            IWebHostEnvironment env,
            IAntiforgery antiforgery)
        {
            _authService = authService;
            _userService = userService;
            _env = env;
            _antiforgery = antiforgery;
        }

        // 🔐 Login (legacy — returns token in body). Kept for backwards compatibility.
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }

        // 🍪 Cookie-based Login. Sets HttpOnly Secure cookie, body has NO token.
        [HttpPost("cookie-login")]
        public async Task<IActionResult> CookieLogin(LoginRequest request)
        {
            var (token, user, expiresUtc) = await _authService.LoginForCookieAsync(request);

            Response.Cookies.Append(AccessTokenCookieName, token, BuildAuthCookieOptions(expiresUtc));

            // Antiforgery setup depends on DataProtection, which can fail on some
            // shared hosts (no persistent key ring). Don't let CSRF setup break login —
            // the frontend can re-issue the token via GET /csrf-token if needed.
            try
            {
                IssueCsrfCookie();
            }
            catch
            {
                // Intentionally swallowed; logged elsewhere if needed.
            }

            return Ok(new CookieLoginResponse
            {
                Success = true,
                User = user
            });
        }

        // 👤 Current user info (works with either cookie or Authorization header).
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await _authService.GetCurrentUserAsync(User);
            return Ok(user);
        }

        // 🚪 Logout — clears the auth cookie (and the CSRF cookie).
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            var opts = BuildAuthCookieOptions(DateTime.UtcNow.AddDays(-1));
            Response.Cookies.Delete(AccessTokenCookieName, opts);

            var xsrfDeleteOpts = new CookieOptions
            {
                Path = "/",
                Secure = !_env.IsDevelopment(),
                SameSite = SameSiteMode.None
            };
            // Match the Partitioned flag used when the cookie was originally set —
            // browsers won't delete a partitioned cookie via a non-partitioned Set-Cookie.
            if (!_env.IsDevelopment())
            {
                xsrfDeleteOpts.Extensions.Add("Partitioned");
            }
            Response.Cookies.Delete("XSRF-TOKEN", xsrfDeleteOpts);

            return Ok(new { success = true });
        }

        // 🛡️ Endpoint frontends can call once to mint a fresh CSRF token cookie
        // (useful for SPAs that need it before their first authenticated request).
        [HttpGet("csrf-token")]
        public IActionResult GetCsrfToken()
        {
            IssueCsrfCookie();
            return Ok(new { success = true });
        }

        // 👨‍🎓 Student Register
        [HttpPost("register-student")]
        public async Task<IActionResult> RegisterStudent(RegisterStudentRequest request)
        {
            var message = await _authService.RegisterStudentAsync(request, Request);
            return Ok(new { message });
        }

        // 📩 Confirm Email
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string token, string userId)
        {
            var message = await _authService.ConfirmEmailAsync(token, userId);
            return Ok(new { message });
        }

        // 🔑 Forgot Password
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgetPasswordRequest request)
        {
            var success = await _authService.ForgotPasswordAsync(request);
            return Ok(new { success });
        }

        // 🔄 Reset Password (code flow)
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var success = await _authService.ResetPasswordAsync(request);
            return Ok(new { success });
        }

        // 🔐 Set Password (Option A – created by Admin/SuperAdmin)
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword(SetPasswordRequest request)
        {
            var message = await _authService.SetPasswordAsync(request);
            return Ok(new { message });
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private CookieOptions BuildAuthCookieOptions(DateTime expiresUtc)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,                                  // blocks JS access (XSS protection)
                Secure = !_env.IsDevelopment(),                   // HTTPS-only in non-dev
                SameSite = SameSiteMode.None,                     // frontend is on a different origin
                Path = "/",
                Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero)
            };

            // CHIPS — Partitioned cookies. REQUIRED for iOS Safari 17+ (and other modern
            // browsers with strict cross-site cookie policies / ITP) to actually STORE a
            // cross-site SameSite=None cookie. Without `Partitioned`, Safari silently
            // drops the Set-Cookie header in production and the user appears to be
            // kicked out immediately after login.
            // Safe to always emit — older browsers ignore unknown attributes.
            if (!_env.IsDevelopment())
            {
                options.Extensions.Add("Partitioned");
            }

            return options;
        }

        private void IssueCsrfCookie()
        {
            // GetAndStoreTokens writes the XSRF-TOKEN cookie (readable by JS, by design)
            // and prepares the matching request token that must come back as X-XSRF-TOKEN.
            _antiforgery.GetAndStoreTokens(HttpContext);
        }

    }
}

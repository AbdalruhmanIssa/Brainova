namespace Brainova.BLL.DTOs.Auth
{
    public class CookieLoginResponse
    {
        public bool Success { get; set; } = true;
        public CurrentUserResponse User { get; set; } = null!;
    }
}

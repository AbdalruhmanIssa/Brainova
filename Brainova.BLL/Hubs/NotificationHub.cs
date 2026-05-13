using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Brainova.BLL.Hubs
{
    /// <summary>
    /// Real-time notification hub.
    /// Clients connect to /hubs/notifications with their JWT.
    /// The server pushes notifications to a specific user by their UserId
    /// (resolved via <see cref="JwtIdUserIdProvider"/> which reads the "Id" claim).
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        // No client-callable methods are needed for the feedback use case:
        // the server pushes; the client only listens.
        //
        // Lifecycle hooks are kept for future debugging / presence tracking.
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
    }

    /// <summary>
    /// Tells SignalR which claim identifies the user.
    /// Our JWTs put the user GUID in a custom "Id" claim
    /// (see <c>AuthenticationService</c>), not in the default <c>NameIdentifier</c>.
    /// Without this, <c>Clients.User(userId)</c> would not route correctly.
    /// </summary>
    public class JwtIdUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst("Id")?.Value;
        }
    }
}

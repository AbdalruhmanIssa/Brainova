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
        // Group name prefix for role-targeted broadcasts.
        // Use RoleGroup("Admin") / RoleGroup("SuperAdmin") etc. from NotificationService
        // to push to every connection that authenticated with that role.
        public const string RoleGroupPrefix = "role:";
        public static string RoleGroup(string role) => $"{RoleGroupPrefix}{role}";

        // No client-callable methods are needed for the feedback use case:
        // the server pushes; the client only listens.
        //
        // On connect we add the connection to a group keyed by the user's role
        // (read from the JWT "Role" claim — same claim type configured in Program.cs).
        // This lets the server broadcast to e.g. "all admins" without having to
        // maintain a list of user ids.
        public override async Task OnConnectedAsync()
        {
            var role = Context.User?.FindFirst("Role")?.Value;
            if (!string.IsNullOrWhiteSpace(role))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR removes the connection from groups automatically when it
            // disconnects, so we don't need to clean up manually.
            await base.OnDisconnectedAsync(exception);
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

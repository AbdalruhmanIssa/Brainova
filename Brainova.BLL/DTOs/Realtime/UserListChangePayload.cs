namespace Brainova.BLL.DTOs.Realtime
{
    /// <summary>
    /// Constant strings used for the <see cref="UserListChangePayload.Kind"/> field.
    /// Kept as <c>const string</c> rather than an enum so the value serializes as a
    /// stable readable string ("Created", "Blocked", ...) regardless of JSON options.
    /// </summary>
    public static class UserListChangeKinds
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Blocked = "Blocked";
        public const string Unblocked = "Unblocked";
        public const string RoleChanged = "RoleChanged";
        public const string Deleted = "Deleted";
        public const string EmailConfirmed = "EmailConfirmed";
    }

    /// <summary>
    /// Payload sent with the SignalR "UserListChanged" event so admin clients
    /// know exactly what happened, to whom, and by whom — without having to
    /// diff their local cache against a fresh fetch.
    ///
    /// Required fields:
    ///   - Kind: one of <see cref="UserListChangeKinds"/>.
    ///   - UserId: the affected user.
    ///
    /// Optional fields are best-effort: callers populate what they already have
    /// loaded; we never do extra DB roundtrips just to enrich a notification.
    /// </summary>
    public class UserListChangePayload
    {
        public string Kind { get; set; } = default!;

        public string UserId { get; set; } = default!;

        /// <summary>Login name of the affected user (if cheaply available).</summary>
        public string? UserName { get; set; }

        /// <summary>Display name of the affected user (if cheaply available).</summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Role of the affected user. For Created/RoleChanged this is the new role.
        /// For others it's the user's current role (if known).
        /// </summary>
        public string? Role { get; set; }

        /// <summary>For RoleChanged: the previous role; null otherwise.</summary>
        public string? OldRole { get; set; }

        /// <summary>Id of the admin/super-admin who performed the action.</summary>
        public string? ByUserId { get; set; }
    }
}

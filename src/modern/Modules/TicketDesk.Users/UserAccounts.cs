using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;

namespace TicketDesk.Users;

/// <summary>A row of <c>dbo.IdentityRoles</c> as the admin pages show it.</summary>
public sealed record RoleInfo(string Id, string Name, string? DisplayName, string? Description);

/// <summary>
/// Account data the identity and admin pages need besides ASP.NET Core Identity's UserManager: the role list in the legacy
/// order, and the per-user e-mail destination of push notifications that the legacy register, new-user and edit-profile
/// actions maintained (<c>notifications.SubscriberPushNotificationSettings</c> / <c>PushNotificationDestinations</c>).
/// The notification tables are written with plain SQL in the legacy shape: the Notifications module owns their model.
/// </summary>
public interface IUserAccounts
{
    /// <summary>Legacy <c>RoleManager.Roles</c>: table order (the clustered key, Id).</summary>
    Task<IReadOnlyList<RoleInfo>> GetRolesAsync(CancellationToken ct);

    /// <summary>Legacy <c>AddSettingsForSubscriber</c> for a new account: enabled settings with one e-mail destination.</summary>
    Task AddEmailDestinationAsync(string userId, string? email, string displayName, CancellationToken ct);

    /// <summary>Legacy <c>ResetMailEmailDestination</c>: the e-mail destination that had the old address gets the new one.</summary>
    Task ResetEmailDestinationAsync(string userId, string? oldEmail, string? newEmail, string displayName, CancellationToken ct);
}

internal sealed class UserAccounts(TicketDeskDbContext db) : IUserAccounts
{
    public async Task<IReadOnlyList<RoleInfo>> GetRolesAsync(CancellationToken ct) =>
        await db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new RoleInfo(r.Id, r.Name, r.DisplayName, r.Description)).ToListAsync(ct);

    public async Task AddEmailDestinationAsync(string userId, string? email, string displayName, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS (SELECT 1 FROM notifications.SubscriberPushNotificationSettings WHERE SubscriberId = {userId})
                INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) VALUES ({userId}, 1);
            INSERT notifications.PushNotificationDestinations (SubscriberId, DestinationType, DestinationAddress, SubscriberName)
            VALUES ({userId}, N'email', {email}, {displayName});
            """, ct);
    }

    public async Task ResetEmailDestinationAsync(string userId, string? oldEmail, string? newEmail, string displayName, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS (SELECT 1 FROM notifications.SubscriberPushNotificationSettings WHERE SubscriberId = {userId})
                INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) VALUES ({userId}, 1);
            DECLARE @id int = (SELECT TOP (1) DestinationId FROM notifications.PushNotificationDestinations
                               WHERE SubscriberId = {userId} AND DestinationType = N'email' AND DestinationAddress = {oldEmail}
                               ORDER BY DestinationId);
            IF @id IS NULL
                INSERT notifications.PushNotificationDestinations (SubscriberId, DestinationType, DestinationAddress, SubscriberName)
                VALUES ({userId}, N'email', {newEmail}, {displayName});
            ELSE
                UPDATE notifications.PushNotificationDestinations SET DestinationAddress = {newEmail}, SubscriberName = {displayName}
                WHERE DestinationId = @id;
            """, ct);
    }
}

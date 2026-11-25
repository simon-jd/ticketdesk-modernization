using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;

namespace TicketDesk.Users;

/// <summary>A user id (IdentityUsers.Id) with the display name, or null when the id has no IdentityUsers row.</summary>
public sealed record UserRef(string Id, string? DisplayName);

/// <summary>One row of the admin user list: the user, the display names of its roles and the lock state.</summary>
public sealed record UserAccountRow(string Id, string UserName, string DisplayName, IReadOnlyList<string> RoleNames, bool IsLocked);

public sealed record UserAccountPage(IReadOnlyList<UserAccountRow> Items, int PageNumber, int PageSize, int TotalItemCount)
{
    public int PageCount => TotalItemCount > 0 ? (int)Math.Ceiling(TotalItemCount / (double)PageSize) : 0;
}

/// <summary>
/// Display names for the user-id strings that tickets and events store (no foreign keys in the legacy schema).
/// The legacy app cached all users for 15 minutes (<c>user-info-cache</c>); this reads per request.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct);

    /// <summary>Members of one role (direct membership only, like the legacy role manager), ordered by display name in SQL.</summary>
    Task<IReadOnlyList<UserRef>> GetUsersInRoleAsync(string roleName, CancellationToken ct);

    /// <summary>Legacy admin user list: by display name, 25 per page, role display names in the roles table's order.</summary>
    Task<UserAccountPage> GetAccountsPageAsync(int page, CancellationToken ct);
}

internal sealed class UserDirectory(TicketDeskDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (ids.Length == 0) return new Dictionary<string, string>();
        var rows = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<UserAccountPage> GetAccountsPageAsync(int page, CancellationToken ct)
    {
        const int size = 25;
        page = Math.Max(page, 1);
        var total = await db.Users.CountAsync(ct);
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).Skip((page - 1) * size).Take(size)
            .Select(u => new { u.Id, u.UserName, u.DisplayName, u.LockoutEndDateUtc, Roles = u.Roles.Select(r => r.RoleId).ToList() })
            .ToListAsync(ct);
        // legacy: RoleManager.Roles.ToListAsync() (table order) filtered by the user's role ids, showing DisplayName
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new { r.Id, r.DisplayName }).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var rows = users.Select(u => new UserAccountRow(u.Id, u.UserName, u.DisplayName,
            roles.Where(r => u.Roles.Contains(r.Id)).Select(r => r.DisplayName ?? "").ToList(),
            // legacy IsLocked: lockout end in the future but below DateTime.MaxValue ("disabled" is stored as MaxValue,
            // which a datetime column clamps to ...23:59:59.997, so disabled users also show as locked, as before)
            (u.LockoutEndDateUtc ?? DateTime.MinValue) > now && (u.LockoutEndDateUtc ?? DateTime.MinValue) < DateTime.MaxValue)).ToList();
        return new UserAccountPage(rows, page, size, total);
    }

    public async Task<IReadOnlyList<UserRef>> GetUsersInRoleAsync(string roleName, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == roleName)))
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserRef(u.Id, u.DisplayName))
            .ToListAsync(ct);
}

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services) =>
        services.AddScoped<IUserDirectory, UserDirectory>().AddScoped<IUserAccounts, UserAccounts>();

    public static UserRef ToUserRef(this IReadOnlyDictionary<string, string> names, string id) =>
        new(id, names.TryGetValue(id, out var name) ? name : null);
}

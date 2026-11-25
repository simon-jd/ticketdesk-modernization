using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Identity;
using TicketDesk.Users;

namespace TicketDesk.Tickets.Pages;

/// <summary>
/// User lists and display names for the ticket pages, in the legacy order: <c>TicketDeskRoleManager.GetUsersInRole</c>
/// collected each role's members in role order (each role's members in key order, duplicates dropped) and sorted the
/// result by display name with the current culture's comparer (a stable sort).
/// </summary>
public sealed class TicketUsers(TicketDeskDbContext db)
{
    public Task<IReadOnlyList<UserRef>> InternalUsersAsync(CancellationToken ct) =>
        InRolesAsync([TicketDeskRoles.InternalUsers, TicketDeskRoles.HelpDeskUsers, TicketDeskRoles.Administrators], ct);

    public Task<IReadOnlyList<UserRef>> HelpDeskUsersAsync(CancellationToken ct) =>
        InRolesAsync([TicketDeskRoles.HelpDeskUsers, TicketDeskRoles.Administrators], ct);

    public async Task<IReadOnlyList<UserRef>> InRolesAsync(string[] roles, CancellationToken ct)
    {
        var rows = await (from ur in db.UserRoles
                          join r in db.Roles on ur.RoleId equals r.Id
                          join u in db.Users on ur.UserId equals u.Id
                          where roles.Contains(r.Name)
                          select new { u.Id, u.DisplayName, Role = r.Name }).AsNoTracking().ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var collected = new List<UserRef>();
        foreach (var role in roles)
            foreach (var row in rows.Where(r => string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
                if (seen.Add(row.Id)) collected.Add(new UserRef(row.Id, row.DisplayName));
        return collected.OrderBy(u => u.DisplayName, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Display names by user id (legacy user-info cache); ids without a user are left out.</summary>
    public async Task<IReadOnlyDictionary<string, string>> NamesAsync(IEnumerable<string?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => !string.IsNullOrEmpty(i)).Select(i => i!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (wanted.Length == 0) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rows = await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.DisplayName ?? "", StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> NameAsync(string? id, CancellationToken ct) =>
        string.IsNullOrEmpty(id) ? null : (await NamesAsync([id], ct)).GetValueOrDefault(id);
}

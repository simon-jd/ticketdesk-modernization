using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;

namespace TicketDesk.Identity;

/// <summary>Role store over <c>IdentityRoles</c> (no <c>NormalizedName</c> column; see <see cref="TicketDeskUserStore"/>).</summary>
public sealed class TicketDeskRoleStore(TicketDeskDbContext db) : IQueryableRoleStore<TicketDeskRole>
{
    public IQueryable<TicketDeskRole> Roles => db.Roles;

    public async Task<IdentityResult> CreateAsync(TicketDeskRole role, CancellationToken ct)
    {
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(TicketDeskRole role, CancellationToken ct)
    {
        if (db.Entry(role).State == EntityState.Detached) db.Roles.Update(role);
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(TicketDeskRole role, CancellationToken ct)
    {
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(TicketDeskRole role, CancellationToken ct) => Task.FromResult(role.Id);
    public Task<string?> GetRoleNameAsync(TicketDeskRole role, CancellationToken ct) => Task.FromResult<string?>(role.Name);

    public Task SetRoleNameAsync(TicketDeskRole role, string? roleName, CancellationToken ct)
    {
        role.Name = roleName ?? "";
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(TicketDeskRole role, CancellationToken ct) =>
        Task.FromResult(TicketDeskUserStore.Normalize(role.Name));

    public Task SetNormalizedRoleNameAsync(TicketDeskRole role, string? normalizedName, CancellationToken ct) => Task.CompletedTask;

    public async Task<TicketDeskRole?> FindByIdAsync(string roleId, CancellationToken ct) => await db.Roles.FindAsync([roleId], ct);

    public Task<TicketDeskRole?> FindByNameAsync(string normalizedRoleName, CancellationToken ct) =>
        db.Roles.SingleOrDefaultAsync(r => r.Name == normalizedRoleName, ct);

    public void Dispose()
    {
    }
}

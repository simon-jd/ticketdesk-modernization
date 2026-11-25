using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Configurations;
using TicketDesk.Data.Entities;

namespace TicketDesk.Identity;

/// <summary>
/// ASP.NET Core Identity user store over the unchanged ASP.NET Identity 2 tables. The legacy schema has no
/// <c>NormalizedUserName</c>/<c>NormalizedEmail</c>/<c>ConcurrencyStamp</c> columns, so this store does not use the
/// EF Core Identity stores: normalized names are computed (upper invariant, like the default lookup normalizer) and
/// lookups compare against <c>UserName</c>/<c>Email</c> under the database's case-insensitive collation.
/// <c>LockoutEnd</c> maps to the UTC <c>datetime</c> column <c>LockoutEndDateUtc</c>.
/// </summary>
public sealed class TicketDeskUserStore(TicketDeskDbContext db, IdentityErrorDescriber? describer = null) :
    IUserPasswordStore<TicketDeskUser>,
    IUserEmailStore<TicketDeskUser>,
    IUserSecurityStampStore<TicketDeskUser>,
    IUserLockoutStore<TicketDeskUser>,
    IUserRoleStore<TicketDeskUser>,
    IUserClaimStore<TicketDeskUser>,
    IUserLoginStore<TicketDeskUser>,
    IQueryableUserStore<TicketDeskUser>
{
    private readonly IdentityErrorDescriber _errors = describer ?? new IdentityErrorDescriber();

    public IQueryable<TicketDeskUser> Users => db.Users;

    internal static string? Normalize(string? value) => value?.Normalize().ToUpperInvariant();

    // --- IUserStore
    public Task<string> GetUserIdAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName);

    public Task SetUserNameAsync(TicketDeskUser user, string? userName, CancellationToken ct)
    {
        user.UserName = userName ?? "";
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(Normalize(user.UserName));

    /// <summary>No column to store it in; lookups rely on the case-insensitive collation of <c>UserName</c>.</summary>
    public Task SetNormalizedUserNameAsync(TicketDeskUser user, string? normalizedName, CancellationToken ct) => Task.CompletedTask;

    public async Task<IdentityResult> CreateAsync(TicketDeskUser user, CancellationToken ct)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(TicketDeskUser user, CancellationToken ct)
    {
        if (db.Entry(user).State == EntityState.Detached) db.Users.Update(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return IdentityResult.Failed(_errors.ConcurrencyFailure());
        }
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(TicketDeskUser user, CancellationToken ct)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<TicketDeskUser?> FindByIdAsync(string userId, CancellationToken ct) =>
        await db.Users.FindAsync([userId], ct);

    public Task<TicketDeskUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.UserName == normalizedUserName, ct);

    // --- IUserPasswordStore
    public Task SetPasswordHashAsync(TicketDeskUser user, string? passwordHash, CancellationToken ct)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash != null);

    // --- IUserEmailStore
    public Task SetEmailAsync(TicketDeskUser user, string? email, CancellationToken ct)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(TicketDeskUser user, bool confirmed, CancellationToken ct)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    /// <summary>Like Identity 2 (first match); the legacy user manager required unique e-mail addresses.</summary>
    public Task<TicketDeskUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.Where(u => u.Email == normalizedEmail).OrderBy(u => u.Id).FirstOrDefaultAsync(ct);

    public Task<string?> GetNormalizedEmailAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(Normalize(user.Email));
    public Task SetNormalizedEmailAsync(TicketDeskUser user, string? normalizedEmail, CancellationToken ct) => Task.CompletedTask;

    // --- IUserSecurityStampStore
    public Task SetSecurityStampAsync(TicketDeskUser user, string stamp, CancellationToken ct)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);

    // --- IUserLockoutStore
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(TicketDeskUser user, CancellationToken ct) =>
        Task.FromResult(user.LockoutEndDateUtc is { } end
            ? new DateTimeOffset(DateTime.SpecifyKind(end, DateTimeKind.Utc))
            : (DateTimeOffset?)null);

    /// <summary>
    /// <see cref="DateTimeOffset.MaxValue"/> (how the legacy admin page disables a user) is clamped to the largest
    /// <c>datetime</c> value, 9999-12-31 23:59:59.997.
    /// </summary>
    public Task SetLockoutEndDateAsync(TicketDeskUser user, DateTimeOffset? lockoutEnd, CancellationToken ct)
    {
        user.LockoutEndDateUtc = lockoutEnd is { } end ? SqlUtcDateTime.ToStore(end.UtcDateTime) : null;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(TicketDeskUser user, CancellationToken ct)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(TicketDeskUser user, CancellationToken ct) => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(TicketDeskUser user, bool enabled, CancellationToken ct)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    // --- IUserRoleStore (role names arrive normalized; comparisons use the case-insensitive collation)
    public async Task AddToRoleAsync(TicketDeskUser user, string normalizedRoleName, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == normalizedRoleName, ct)
                   ?? throw new InvalidOperationException($"Role '{normalizedRoleName}' does not exist.");
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct))
            db.UserRoles.Add(new TicketDeskUserRole { UserId = user.Id, RoleId = role.Id });
    }

    public async Task RemoveFromRoleAsync(TicketDeskUser user, string normalizedRoleName, CancellationToken ct)
    {
        var link = await db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == user.Id && ur.Role!.Name == normalizedRoleName, ct);
        if (link != null) db.UserRoles.Remove(link);
    }

    public async Task<IList<string>> GetRolesAsync(TicketDeskUser user, CancellationToken ct) =>
        await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.Role!.Name).OrderBy(n => n).ToListAsync(ct);

    public Task<bool> IsInRoleAsync(TicketDeskUser user, string normalizedRoleName, CancellationToken ct) =>
        db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.Role!.Name == normalizedRoleName, ct);

    public async Task<IList<TicketDeskUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken ct) =>
        await db.UserRoles.Where(ur => ur.Role!.Name == normalizedRoleName).Select(ur => ur.User!).ToListAsync(ct);

    // --- IUserClaimStore
    public async Task<IList<Claim>> GetClaimsAsync(TicketDeskUser user, CancellationToken ct) =>
        (await db.UserClaims.Where(c => c.UserId == user.Id && c.ClaimType != null).ToListAsync(ct))
        .Select(c => new Claim(c.ClaimType!, c.ClaimValue ?? "")).ToList();

    public Task AddClaimsAsync(TicketDeskUser user, IEnumerable<Claim> claims, CancellationToken ct)
    {
        foreach (var c in claims)
            db.UserClaims.Add(new TicketDeskUserClaim { UserId = user.Id, ClaimType = c.Type, ClaimValue = c.Value });
        return Task.CompletedTask;
    }

    public async Task ReplaceClaimAsync(TicketDeskUser user, Claim claim, Claim newClaim, CancellationToken ct)
    {
        var matches = await db.UserClaims.Where(c => c.UserId == user.Id && c.ClaimType == claim.Type && c.ClaimValue == claim.Value).ToListAsync(ct);
        foreach (var m in matches)
        {
            m.ClaimType = newClaim.Type;
            m.ClaimValue = newClaim.Value;
        }
    }

    public async Task RemoveClaimsAsync(TicketDeskUser user, IEnumerable<Claim> claims, CancellationToken ct)
    {
        foreach (var c in claims)
        {
            var matches = await db.UserClaims.Where(x => x.UserId == user.Id && x.ClaimType == c.Type && x.ClaimValue == c.Value).ToListAsync(ct);
            db.UserClaims.RemoveRange(matches);
        }
    }

    public async Task<IList<TicketDeskUser>> GetUsersForClaimAsync(Claim claim, CancellationToken ct) =>
        await db.UserClaims.Where(c => c.ClaimType == claim.Type && c.ClaimValue == claim.Value).Select(c => c.User!).ToListAsync(ct);

    // --- IUserLoginStore (external sign-in, e.g. Entra ID later, links through IdentityUserLogins)
    public Task AddLoginAsync(TicketDeskUser user, UserLoginInfo login, CancellationToken ct)
    {
        db.UserLogins.Add(new TicketDeskUserLogin { UserId = user.Id, LoginProvider = login.LoginProvider, ProviderKey = login.ProviderKey });
        return Task.CompletedTask;
    }

    public async Task RemoveLoginAsync(TicketDeskUser user, string loginProvider, string providerKey, CancellationToken ct)
    {
        var entry = await db.UserLogins.SingleOrDefaultAsync(l => l.UserId == user.Id && l.LoginProvider == loginProvider && l.ProviderKey == providerKey, ct);
        if (entry != null) db.UserLogins.Remove(entry);
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(TicketDeskUser user, CancellationToken ct) =>
        (await db.UserLogins.Where(l => l.UserId == user.Id).ToListAsync(ct))
        .Select(l => new UserLoginInfo(l.LoginProvider, l.ProviderKey, l.LoginProvider)).ToList();

    public Task<TicketDeskUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken ct) =>
        db.UserLogins.Where(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey).Select(l => l.User).SingleOrDefaultAsync(ct);

    public void Dispose()
    {
        // the DbContext belongs to the DI scope
    }
}

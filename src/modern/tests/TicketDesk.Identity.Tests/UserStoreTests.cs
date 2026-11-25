using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Seed;
using TicketDesk.Testing;

namespace TicketDesk.Identity.Tests;

/// <summary>ASP.NET Core Identity over the legacy tables of a scratch database seeded with Identity v2 hashes.</summary>
public sealed class UserStoreTests(ScratchDatabase scratch)
{
    private ServiceProvider Services()
    {
        Assert.SkipUnless(scratch.Available, SqlTestEnvironment.SkipReason);
        return Setup.Services(scratch.ConnectionString);
    }

    private static async Task<TicketDeskUser> NewUserAsync(IServiceProvider sp, string password)
    {
        var um = sp.GetRequiredService<UserManager<TicketDeskUser>>();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var user = new TicketDeskUser { UserName = $"new.{tag}@test.example", Email = $"new.{tag}@test.example", DisplayName = $"New {tag}" };
        var result = await um.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        Assert.True((await um.AddToRoleAsync(user, TicketDeskRoles.InternalUsers)).Succeeded);
        return user;
    }

    private Task<string> StoredHashAsync(string userId) =>
        scratch.ScalarAsync<string>("SELECT PasswordHash FROM dbo.IdentityUsers WHERE Id = @id", ("@id", userId));

    [Fact]
    public async Task Seeded_v2_hash_signs_in_and_is_rehashed_to_v3()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var seeded = scratch.UserInRole(TicketDeskRoles.InternalUsers);
        var hashBefore = await StoredHashAsync(seeded.Id);

        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var user = await um.FindByNameAsync(seeded.UserName);
        var result = await scope.ServiceProvider.GetRequiredService<SignInManager<TicketDeskUser>>()
            .CheckPasswordSignInAsync(user!, ScratchDatabase.UserPassword, lockoutOnFailure: true);

        Assert.True(result.Succeeded);
        Assert.True(IdentityV2PasswordHasher.Verify(hashBefore, ScratchDatabase.UserPassword));
        var hashAfter = await StoredHashAsync(seeded.Id);
        Assert.Equal(1, Convert.FromBase64String(hashAfter)[0]); // rehashed to V3 by the successful sign-in
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<TicketDeskUser>>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user!, hashAfter, ScratchDatabase.UserPassword));
        // a second sign-in verifies the V3 hash and leaves it as it is
        Assert.True((await scope.ServiceProvider.GetRequiredService<SignInManager<TicketDeskUser>>()
            .CheckPasswordSignInAsync(user!, ScratchDatabase.UserPassword, lockoutOnFailure: true)).Succeeded);
        Assert.Equal(hashAfter, await StoredHashAsync(seeded.Id));
    }

    [Fact]
    public async Task Lookups_by_name_and_email_ignore_case_without_normalized_columns()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var seeded = scratch.Users[3];
        Assert.Equal(seeded.Id, (await um.FindByNameAsync(seeded.UserName.ToUpperInvariant()))?.Id);
        Assert.Equal(seeded.Id, (await um.FindByEmailAsync(seeded.Email.ToUpperInvariant()))?.Id);
        Assert.Equal(seeded.Id, (await um.FindByIdAsync(seeded.Id))?.Id);
        Assert.Null(await um.FindByNameAsync("nobody@nowhere.test"));
    }

    [Fact]
    public async Task New_and_changed_passwords_are_stored_as_v3_hashes()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var user = await NewUserAsync(scope.ServiceProvider, "first-pass");
        try
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<TicketDeskUser>>();
            bool IsV3(string hash, string password) =>
                Convert.FromBase64String(hash)[0] == 1 && hasher.VerifyHashedPassword(user, hash, password) == PasswordVerificationResult.Success;
            Assert.True(IsV3(await StoredHashAsync(user.Id), "first-pass"));
            var stampBefore = user.SecurityStamp;

            Assert.True((await um.ChangePasswordAsync(user, "first-pass", "second-pass")).Succeeded);
            var hash = await StoredHashAsync(user.Id);
            Assert.True(IsV3(hash, "second-pass"));
            Assert.False(IdentityV2PasswordHasher.Verify(hash, "second-pass")); // the legacy algorithm cannot read it
            Assert.NotEqual(stampBefore, user.SecurityStamp);

            var token = await um.GeneratePasswordResetTokenAsync(user);
            Assert.True((await um.ResetPasswordAsync(user, token, "third-pass")).Succeeded);
            Assert.True(IsV3(await StoredHashAsync(user.Id), "third-pass"));
            Assert.Equal([TicketDeskRoles.InternalUsers], await um.GetRolesAsync(user));
        }
        finally
        {
            await um.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task Five_failures_lock_the_account_for_five_minutes()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<TicketDeskUser>>();
        var user = await NewUserAsync(scope.ServiceProvider, "right-pass");
        try
        {
            SignInResult last = SignInResult.Failed;
            for (var i = 0; i < 5; i++) last = await signIn.CheckPasswordSignInAsync(user, "wrong-pass", lockoutOnFailure: true);
            Assert.True(last.IsLockedOut);
            Assert.True((await signIn.CheckPasswordSignInAsync(user, "right-pass", lockoutOnFailure: true)).IsLockedOut);

            var end = await scratch.ScalarAsync<DateTime>("SELECT LockoutEndDateUtc FROM dbo.IdentityUsers WHERE Id = @id", ("@id", user.Id));
            Assert.InRange(end, DateTime.UtcNow.AddMinutes(4), DateTime.UtcNow.AddMinutes(6)); // stored as UTC
        }
        finally
        {
            await um.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task Disabling_with_DateTimeOffset_MaxValue_locks_out_permanently()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var user = await NewUserAsync(scope.ServiceProvider, "right-pass");
        try
        {
            // what the legacy user-admin page does to disable a user
            Assert.True((await um.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)).Succeeded);
            Assert.True(await um.IsLockedOutAsync(user));
            var stored = await scratch.ScalarAsync<DateTime>("SELECT LockoutEndDateUtc FROM dbo.IdentityUsers WHERE Id = @id", ("@id", user.Id));
            Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, 997), stored);
            var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<TicketDeskUser>>();
            Assert.True((await signIn.CheckPasswordSignInAsync(user, "right-pass", lockoutOnFailure: false)).IsLockedOut);
        }
        finally
        {
            await um.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_case_insensitively()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var existing = scratch.Users[2];
        var result = await um.CreateAsync(new TicketDeskUser { UserName = "dup." + Guid.NewGuid().ToString("N")[..6], Email = existing.Email.ToUpperInvariant(), DisplayName = "Dup" }, "abcde");
        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
        var sameName = await um.CreateAsync(new TicketDeskUser { UserName = existing.UserName.ToUpperInvariant(), Email = "x@y.test", DisplayName = "Dup" }, "abcde");
        Assert.Contains(sameName.Errors, e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName));
    }

    [Fact]
    public async Task Claims_principal_carries_legacy_roles_and_display_name()
    {
        await using var sp = Services();
        await using var scope = sp.CreateAsyncScope();
        var staff = scratch.UserInRole(TicketDeskRoles.HelpDeskUsers);
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>().FindByIdAsync(staff.Id);
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<TicketDeskUser>>().CreateAsync(user!);

        Assert.True(principal.IsInRole(TicketDeskRoles.HelpDeskUsers));
        Assert.False(principal.IsInRole(TicketDeskRoles.Administrators));
        Assert.Equal(staff.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(staff.UserName, principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(staff.DisplayName, principal.FindFirstValue(ClaimTypes.GivenName));
        Assert.NotNull(principal.FindFirstValue(new IdentityOptions().ClaimsIdentity.SecurityStampClaimType));
    }

    [Fact]
    public async Task Hashes_in_the_seeded_database_verify()
    {
        var conn = await SqlTestEnvironment.SeededDatabaseAsync();
        Assert.SkipUnless(conn != null && SqlTestEnvironment.SeedUserPassword != null,
            "set TD_SQL_TEST_CONN and TD_SEED_USER_PASSWORD (from $TD_W/.secrets/td-seed.env) to check the seeded database");
        await using var sp = Setup.Services(conn);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var users = await db.Users.Where(u => u.PasswordHash != null && u.Email!.EndsWith("@seed.ticketdesk.test")).OrderBy(u => u.Id).Take(5).ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(users);
        foreach (var u in users) Assert.True(await um.CheckPasswordAsync(u, SqlTestEnvironment.SeedUserPassword!));
    }
}

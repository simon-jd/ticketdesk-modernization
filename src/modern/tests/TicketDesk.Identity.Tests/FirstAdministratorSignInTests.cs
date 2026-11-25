using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;
using TicketDesk.Database;
using TicketDesk.Testing;

namespace TicketDesk.Identity.Tests;

/// <summary>
/// The administrator td-db create-admin writes is an account of the app as if the app had created it: its hasher is the one the
/// host configures (same options, no rehash at the first sign-in), the user store finds it by name and e-mail, the password
/// signs in, and the claims carry the administrator role.
/// </summary>
public sealed class FirstAdministratorSignInTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Td_db_hashes_with_the_options_the_host_configures()
    {
        using var sp = Setup.Services();
        var host = sp.GetRequiredService<IOptions<PasswordHasherOptions>>().Value;
        Assert.Equal((host.CompatibilityMode, host.IterationCount), (FirstAdministrator.HasherOptions.CompatibilityMode, FirstAdministrator.HasherOptions.IterationCount));
    }

    [Fact]
    public async Task The_first_administrator_signs_in_with_the_apps_identity_and_needs_no_rehash()
    {
        Assert.SkipUnless(SqlTestEnvironment.ServerConnection != null, SqlTestEnvironment.SkipReason);
        var db = new EmptyInstallDatabase();
        await db.InitializeAsync();
        try
        {
            const string email = "first.admin@example.test", password = "Identity-Secret-31";
            var created = await FirstAdministrator.CreateAsync(db.ConnectionString!, email, "First Admin", password, Ct);
            using var sp = Setup.Services(db.ConnectionString);
            using var scope = sp.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
            var user = await users.FindByNameAsync(email);
            Assert.NotNull(user);
            Assert.Equal(created.UserId, user.Id);
            Assert.Equal(user.Id, (await users.FindByEmailAsync(email))?.Id);
            Assert.True(await users.IsInRoleAsync(user, TicketDeskRoles.Administrators));
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<TicketDeskUser>>();
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash!, password));

            var hashBefore = user.PasswordHash;
            var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<TicketDeskUser>>();
            Assert.True((await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded);
            Assert.False((await signIn.CheckPasswordSignInAsync(user, password + "x", lockoutOnFailure: false)).Succeeded);
            Assert.Equal(hashBefore, await db.ScalarAsync<string>("SELECT PasswordHash FROM dbo.IdentityUsers WHERE Id = @id", ("@id", user.Id)));
            var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<TicketDeskUser>>().CreateAsync(user);
            Assert.True(principal.IsInRole(TicketDeskRoles.Administrators));
        }
        finally
        {
            await db.DisposeAsync();
        }
    }
}

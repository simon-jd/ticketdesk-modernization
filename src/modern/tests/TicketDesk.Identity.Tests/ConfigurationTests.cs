using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;

namespace TicketDesk.Identity.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Identity_options_mirror_the_legacy_user_manager()
    {
        using var sp = Setup.Services();
        var o = sp.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(5, o.Password.RequiredLength);
        Assert.False(o.Password.RequireDigit || o.Password.RequireLowercase || o.Password.RequireUppercase || o.Password.RequireNonAlphanumeric);
        Assert.True(o.User.RequireUniqueEmail);
        Assert.Equal("", o.User.AllowedUserNameCharacters);
        Assert.True(o.Lockout.AllowedForNewUsers);
        Assert.Equal(5, o.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), o.Lockout.DefaultLockoutTimeSpan);
        Assert.Equal(TimeSpan.FromMinutes(30), sp.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
    }

    [Theory]
    [InlineData("abcde", true)]
    [InlineData("abcd", false)]
    public async Task Password_rules_match_the_legacy_minimum(string password, bool valid)
    {
        await using var sp = Setup.Services();
        await using var scope = sp.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        var results = await Task.WhenAll(um.PasswordValidators.Select(v => v.ValidateAsync(um, new TicketDeskUser(), password)));
        Assert.Equal(valid, results.All(r => r.Succeeded));
    }

    public static TheoryData<string, string[], bool> PolicyMatrix => new()
    {
        { TicketDeskPolicies.InternalUser, [TicketDeskRoles.Administrators], true },
        { TicketDeskPolicies.InternalUser, [TicketDeskRoles.HelpDeskUsers], true },
        { TicketDeskPolicies.InternalUser, [TicketDeskRoles.InternalUsers], true },
        { TicketDeskPolicies.InternalUser, [TicketDeskRoles.PendingUsers], false },
        { TicketDeskPolicies.InternalUser, [], false },
        { TicketDeskPolicies.HelpDesk, [TicketDeskRoles.Administrators], true },
        { TicketDeskPolicies.HelpDesk, [TicketDeskRoles.HelpDeskUsers], true },
        { TicketDeskPolicies.HelpDesk, [TicketDeskRoles.InternalUsers], false },
        { TicketDeskPolicies.HelpDesk, [TicketDeskRoles.PendingUsers], false },
        { TicketDeskPolicies.Administrator, [TicketDeskRoles.Administrators], true },
        { TicketDeskPolicies.Administrator, [TicketDeskRoles.HelpDeskUsers], false },
        { TicketDeskPolicies.Administrator, [TicketDeskRoles.InternalUsers], false },
        { TicketDeskPolicies.Administrator, [TicketDeskRoles.PendingUsers], false },
    };

    [Theory]
    [MemberData(nameof(PolicyMatrix))]
    public async Task Policies_map_the_legacy_role_lists(string policy, string[] roles, bool allowed)
    {
        await using var sp = Setup.Services();
        var authz = sp.GetRequiredService<IAuthorizationService>();
        Assert.Equal(allowed, (await authz.AuthorizeAsync(Setup.Principal(roles), policy)).Succeeded);
    }

    [Fact]
    public async Task Anonymous_users_pass_no_policy()
    {
        await using var sp = Setup.Services();
        var authz = sp.GetRequiredService<IAuthorizationService>();
        var anonymous = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        foreach (var policy in new[] { TicketDeskPolicies.InternalUser, TicketDeskPolicies.HelpDesk, TicketDeskPolicies.Administrator })
            Assert.False((await authz.AuthorizeAsync(anonymous, policy)).Succeeded);
    }

    [Fact]
    public async Task Entra_ID_seam_is_off_by_default()
    {
        await using var sp = Setup.Services();
        var schemes = sp.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.Null(await schemes.GetSchemeAsync(EntraIdOptions.Scheme));
        // the Identity cookie itself (no policy scheme in front of it since remote authentication went with the legacy app)
        Assert.Equal(IdentityConstants.ApplicationScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
    }

    [Fact]
    public async Task Entra_ID_seam_registers_OpenID_Connect_when_enabled()
    {
        await using var sp = Setup.Services(settings: new Dictionary<string, string?>
        {
            ["Authentication:EntraId:Enabled"] = "true",
            ["Authentication:EntraId:Authority"] = "https://login.example.test/tenant/v2.0",
            ["Authentication:EntraId:ClientId"] = "00000000-0000-0000-0000-000000000000",
        });
        var scheme = await sp.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(EntraIdOptions.Scheme);
        Assert.Equal(typeof(OpenIdConnectHandler), scheme!.HandlerType);
        var oidc = sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(EntraIdOptions.Scheme);
        Assert.Equal("roles", oidc.TokenValidationParameters.RoleClaimType);
        Assert.Equal(IdentityConstants.ExternalScheme, oidc.SignInScheme);
    }

    [Fact]
    public void Entra_ID_seam_refuses_to_start_half_configured()
    {
        Assert.Throws<InvalidOperationException>(() => Setup.Services(settings: new Dictionary<string, string?> { ["Authentication:EntraId:Enabled"] = "true" }));
    }
}

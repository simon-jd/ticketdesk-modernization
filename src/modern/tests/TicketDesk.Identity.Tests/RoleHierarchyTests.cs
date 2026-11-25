using System.Security.Claims;

namespace TicketDesk.Identity.Tests;

/// <summary>The legacy schema stores only the direct role; the legacy hierarchy is added by <see cref="LegacyRoleHierarchy"/>.</summary>
public sealed class RoleHierarchyTests
{
    private static ClaimsPrincipal User(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)).Prepend(new Claim(ClaimTypes.Name, "u")), "ApplicationCookie"));

    private static async Task<string[]> Roles(ClaimsPrincipal p) =>
        (await new LegacyRoleHierarchy().TransformAsync(p)).FindAll(ClaimTypes.Role).Select(c => c.Value).Order(StringComparer.Ordinal).ToArray();

    [Theory]
    [InlineData("TdAdministrators", "TdAdministrators,TdHelpDeskUsers,TdInternalUsers")]
    [InlineData("TdHelpDeskUsers", "TdHelpDeskUsers,TdInternalUsers")]
    [InlineData("TdInternalUsers", "TdInternalUsers")]
    [InlineData("TdPendingUsers", "TdPendingUsers")]
    public async Task Direct_roles_get_the_roles_they_imply(string direct, string expected) =>
        Assert.Equal(expected.Split(','), await Roles(User(direct)));

    [Fact]
    public async Task Is_idempotent_and_leaves_the_original_alone()
    {
        var original = User("TdAdministrators");
        var once = await new LegacyRoleHierarchy().TransformAsync(original);
        var twice = await new LegacyRoleHierarchy().TransformAsync(once);
        Assert.Equal(3, twice.FindAll(ClaimTypes.Role).Count());
        Assert.Single(original.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Anonymous_principals_are_untouched()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Same(anonymous, await new LegacyRoleHierarchy().TransformAsync(anonymous));
    }
}

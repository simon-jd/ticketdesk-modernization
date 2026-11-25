using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Testing;

[assembly: AssemblyFixture(typeof(ScratchDatabase))]

namespace TicketDesk.Identity.Tests;

internal static class Setup
{
    /// <summary>The services the web host registers for Identity, over the given database (a dummy one never connects).</summary>
    public static ServiceProvider Services(string? connectionString = null, IDictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:TicketDesk"] = connectionString ?? "Server=127.0.0.1,1;Database=none;User Id=x;Password=y;TrustServerCertificate=True",
        };
        foreach (var (k, v) in settings ?? new Dictionary<string, string?>()) values[k] = v;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTicketDeskData();
        services.AddTicketDeskIdentity(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static ClaimsPrincipal Principal(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)).Prepend(new Claim(ClaimTypes.NameIdentifier, "u1")), "test"));
}

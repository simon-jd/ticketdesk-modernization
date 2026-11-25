using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TicketDesk.Data;

namespace TicketDesk.Web.Hosting;

public static class HealthChecks
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddTicketDeskHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag]);
        return services;
    }

    /// <summary><c>/health/live</c>: the process answers. <c>/health/ready</c>: the database is reachable.</summary>
    public static void MapTicketDeskHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteJson });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag), ResponseWriter = WriteJson });
    }

    private static Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.OrderBy(e => e.Key).Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}

/// <summary>
/// Resolves the context inside the check, so a missing connection string reports Unhealthy instead of failing the endpoint
/// (the stock DbContext check resolves the context while the check itself is being created).
/// </summary>
internal sealed class DatabaseHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            var db = services.GetRequiredService<TicketDeskDbContext>();
            return await db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("database reachable")
                : new HealthCheckResult(context.Registration.FailureStatus, "database unreachable");
        }
        catch (InvalidOperationException e)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, e.Message);
        }
    }
}

using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Logs;

namespace TicketDesk.Web.Hosting;

/// <summary>
/// Request telemetry to Application Insights (AKS round: the failed-requests alert and the availability / latency SLOs read the
/// <c>requests</c> table and the standard metrics). On only when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set (on AKS from
/// Key Vault through the CSI driver); local runs and tests send nothing. Every request is kept (no sampling: the SLOs count
/// them), live metrics are off, and only warnings and errors of the app's own logs go out, so the lab workspace's daily cap
/// (0.1 GB) holds.
/// </summary>
public static class Telemetry
{
    public const string ConnectionStringSetting = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static bool AddTicketDeskTelemetry(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration[ConnectionStringSetting];
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        builder.Services.AddOpenTelemetry().UseAzureMonitor(o =>
        {
            o.ConnectionString = connectionString;
            o.SamplingRatio = 1.0F;
            o.TracesPerSecond = null; // rate-limited sampling (the distro's default) would drop requests the SLOs count
            o.EnableLiveMetrics = false;
            o.EnablePerformanceCounters = false;
        });
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.Warning);
        return true;
    }
}

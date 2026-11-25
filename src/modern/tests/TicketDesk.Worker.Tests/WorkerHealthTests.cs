using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using TicketDesk.Notifications;

namespace TicketDesk.Worker.Tests;

/// <summary>AKS round: probes of the worker pod and the key-less Service Bus option.</summary>
public sealed class WorkerHealthTests
{
    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddWorkerServices(configuration).AddWorkerHealthChecks();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Not_ready_until_the_queue_processor_runs()
    {
        await using var sp = Services(new()
        {
            ["ConnectionStrings:TicketDesk"] = "Server=127.0.0.1,1;Database=none;User ID=x;Password=x;TrustServerCertificate=true",
            ["Notifications:ServiceBus:ConnectionString"] = "Endpoint=sb://localhost:1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        });
        // one worker instance: the hosted service is the one the readiness check looks at
        Assert.Same(sp.GetRequiredService<NotificationWorker>(), Assert.Single(sp.GetServices<IHostedService>().OfType<NotificationWorker>()));
        var report = await sp.GetRequiredService<HealthCheckService>().CheckHealthAsync(c => c.Tags.Contains(WorkerHealthCheck.ReadyTag), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public void Service_Bus_by_namespace_needs_no_key()
    {
        var o = new ServiceBusNotificationOptions { FullyQualifiedNamespace = "sb-tdlab-test.servicebus.windows.net" };
        Assert.True(o.Enabled);
        Assert.Equal("sb-tdlab-test.servicebus.windows.net", o.CreateClient().FullyQualifiedNamespace);
        Assert.False(new ServiceBusNotificationOptions().Enabled);
    }
}

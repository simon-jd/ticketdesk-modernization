using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TicketDesk.Data;
using TicketDesk.Notifications;
using TicketDesk.Settings;
using TicketDesk.Tickets.Pages;
using TicketDesk.Tickets.Rendering;
using TicketDesk.Worker;

// The legacy delivery ran on the web server's background threads: en-US unless the server was configured otherwise.
var culture = CultureInfo.GetCultureInfo(Environment.GetEnvironmentVariable("Worker__Culture") ?? "en-US");
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;

// Kubernetes probes (AKS round): with Worker:HealthPort the worker also answers GET /health/live (the process runs) and
// /health/ready (the Service Bus processor runs) on that port; without it there is no listening port at all, as before.
var healthPort = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables().AddCommandLine(args)
    .Build().GetValue<int>("Worker:HealthPort");
if (healthPort > 0)
{
    var web = WebApplication.CreateSlimBuilder(args);
    web.WebHost.ConfigureKestrel(k => { k.AddServerHeader = false; k.ListenAnyIP(healthPort); });
    web.Services.AddWorkerServices(web.Configuration);
    web.Services.AddWorkerHealthChecks();
    var app = web.Build();
    app.MapWorkerHealthChecks();
    app.Run();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWorkerServices(builder.Configuration);
    builder.Build().Run();
}

namespace TicketDesk.Worker
{
    public static class WorkerServices
    {
        /// <summary>The worker's services (also used by the tests, which host it themselves).</summary>
        public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTicketDeskData();
            services.AddSettingsModule(configuration);
            services.AddSingleton<LegacyTextRenderer>();
            services.AddScoped<TicketUsers>();
            services.AddSingleton<TicketEmailRenderer>();
            services.AddScoped<NotificationComposer>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddOptions<WorkerOptions>().Bind(configuration.GetSection(WorkerOptions.SectionName));
            services.AddOptions<ServiceBusNotificationOptions>().Bind(configuration.GetSection(ServiceBusNotificationOptions.SectionName))
                .Validate(o => o.Enabled, $"{ServiceBusNotificationOptions.SectionName}:ConnectionString or :FullyQualifiedNamespace is required").ValidateOnStart();
            services.AddSingleton<IMailTransport>(sp => string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<WorkerOptions>>().Value.Mail.PickupDirectory)
                ? ActivatorUtilities.CreateInstance<SmtpMailTransport>(sp)
                : ActivatorUtilities.CreateInstance<PickupDirectoryTransport>(sp));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServiceBusNotificationOptions>>().Value.CreateClient());
            services.AddSingleton<NotificationWorker>();
            services.AddHostedService(sp => sp.GetRequiredService<NotificationWorker>());
            return services;
        }

        public static IServiceCollection AddWorkerHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks().AddCheck<WorkerHealthCheck>("servicebus-processor", tags: [WorkerHealthCheck.ReadyTag]);
            return services;
        }

        public static void MapWorkerHealthChecks(this IEndpointRouteBuilder app)
        {
            app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
            app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(WorkerHealthCheck.ReadyTag) });
        }

        private static void TryAddSingleton<T>(this IServiceCollection services, T instance) where T : class =>
            Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, instance);
    }
}

namespace TicketDesk.Worker
{
    /// <summary>Ready while the queue processor runs.</summary>
    public sealed class WorkerHealthCheck(NotificationWorker worker) : IHealthCheck
    {
        public const string ReadyTag = "ready";

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(worker.IsProcessing
                ? HealthCheckResult.Healthy("processing the notification queue")
                : HealthCheckResult.Unhealthy("the notification queue processor is not running"));
    }
}

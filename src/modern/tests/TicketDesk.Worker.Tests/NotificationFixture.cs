using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketDesk.Data;
using TicketDesk.Notifications;
using TicketDesk.Testing;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Pages;
using TicketDesk.Tickets.Writes;

namespace TicketDesk.Worker.Tests;

/// <summary>
/// A copy of the baseline data (TicketDesk, with the legacy notifications.* tables) with push notifications switched on (SMTP provider, anti-noise "exclude own events", new-ticket
/// broadcast), e-mail destinations for an owner, an assignee and the broadcast list, and a help-desk author without one.
/// </summary>
public sealed class NotificationDatabase : TicketDesk.Web.Tests.BaselineDatabase
{
    public string Author { get; private set; } = "";
    public string Owner { get; private set; } = "";
    public string Assignee { get; private set; } = "";
    public Dictionary<string, int> Destination { get; } = new();
    public int BroadcastDestination { get; private set; }

    public bool Available => ConnectionString != null;

    private string? _cs;

    protected override async Task PrepareAsync(string connectionString)
    {
        _cs = connectionString;
        string Role(string role, int skip) => $"""
            SELECT ur.UserId FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = '{role}'
            WHERE NOT EXISTS (SELECT 1 FROM dbo.IdentityUserRoles u2 JOIN dbo.IdentityRoles r2 ON r2.Id = u2.RoleId WHERE u2.UserId = ur.UserId AND r2.Name = 'TdAdministrators')
            ORDER BY ur.UserId OFFSET {skip} ROWS FETCH NEXT 1 ROWS ONLY
            """;
        Author = await ScalarAsync<string>(Role("TdHelpDeskUsers", 0));
        Owner = await ScalarAsync<string>(Role("TdInternalUsers", 0));
        Assignee = await ScalarAsync<string>(Role("TdHelpDeskUsers", 1));
        await ScalarAsync<int>("DELETE notifications.PushNotificationDestinations; DELETE notifications.SubscriberPushNotificationSettings; SELECT 0;");
        await SetPushSettingsAsync(enabled: true);
        foreach (var u in new[] { Owner, Assignee })
            Destination[u] = await AddDestinationAsync(u, $"{u[..8]}@example.test", "User " + u[..4]);
        BroadcastDestination = await AddDestinationAsync(NotificationComposer.BroadcastSubscriber, "helpdesk@example.test", "Help desk");
    }

    public Task<T> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters) => Sql.ScalarAsync<T>(_cs!, sql, parameters);

    public Task SetPushSettingsAsync(bool enabled) => ScalarAsync<int>("""
        DELETE notifications.ApplicationPushNotificationSettings WHERE ApplicationName = N'TicketDesk';
        INSERT notifications.ApplicationPushNotificationSettings (ApplicationName, PushNotificationSettingsJson) VALUES (N'TicketDesk', @json);
        SELECT 0;
        """, ("@json", $$$"""
        {"DeliveryProviderSettings":[{"ProviderAssemblyQualifiedName":"TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider, TicketDesk.PushNotifications, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
          "IsEnabled":true,"ProviderConfigurationData":{"SmtpServer":"127.0.0.1","SmtpPort":1,"EnableSsl":false,"SmtpUserName":null,"SmtpPassword":null,"SmtpFromAddress":"ticketdesk@example.test","SmtpFromDisplayName":"TicketDesk"}}],
         "IsEnabled":{{{(enabled ? "true" : "false")}}},"IsBackgroundQueueEnabled":true,"DeliveryIntervalMinutes":2,"RetryAttempts":5,"RetryIntervalMinutes":2,
         "AntiNoiseSettings":{"IsConsolidationEnabled":true,"InitialConsolidationDelayMinutes":2,"MaxConsolidationDelayMinutes":10,"ExcludeSubscriberEvents":true},
         "BroadcastSettings":{"BroadcastMode":0,"IsBroadcastEnabled":true,"SendToCustomEmailAddress":null,"SendToCustomEmailDisplayName":null}}
        """));

    private async Task<int> AddDestinationAsync(string subscriber, string address, string name) => await ScalarAsync<int>("""
        IF NOT EXISTS (SELECT 1 FROM notifications.SubscriberPushNotificationSettings WHERE SubscriberId = @s)
            INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) VALUES (@s, 1);
        INSERT notifications.PushNotificationDestinations (SubscriberName, DestinationAddress, DestinationType, SubscriberId) VALUES (@n, @a, N'email', @s);
        SELECT CAST(SCOPE_IDENTITY() AS int);
        """, ("@s", subscriber), ("@a", address), ("@n", name));

    /// <summary>The app's own write path (TicketWriter) with the given change notifier.</summary>
    public ServiceProvider WriterServices(ITicketChangeNotifier notifier)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TicketDeskDbContext>(o => o.UseTicketDeskSqlServer(_cs!));
        services.AddScoped<TicketUsers>();
        services.AddScoped<TicketWriter>();
        services.AddSingleton(notifier);
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    public async Task<(int TicketId, TicketChange Change)> CreateTicketAsync(ITicketChangeNotifier? notifier = null)
    {
        var capture = new CapturingNotifier(notifier);
        await using var sp = WriterServices(capture);
        await using var scope = sp.CreateAsyncScope();
        var ticket = new TicketDesk.Data.Entities.Ticket
        {
            ProjectId = await ScalarAsync<int>("SELECT MIN(ProjectId) FROM dbo.Projects"), Title = "Projector <b>fan</b> noise", Details = "Loud **fan** in room 4.", TicketType = "Problem",
            Category = "Hardware", Owner = Owner, AssignedTo = Assignee, TagList = "projector,noise",
        };
        Assert.True(await scope.ServiceProvider.GetRequiredService<TicketWriter>().CreateAsync(ticket, new TicketActor(Author, false, true, true), CancellationToken.None));
        return (ticket.TicketId, capture.Changes.Single());
    }

    private sealed class CapturingNotifier(ITicketChangeNotifier? inner) : ITicketChangeNotifier
    {
        public List<TicketChange> Changes { get; } = [];
        public async Task TicketsChangedAsync(IReadOnlyList<TicketChange> changes, CancellationToken ct)
        {
            Changes.AddRange(changes);
            if (inner != null) await inner.TicketsChangedAsync(changes, ct);
        }
    }

    /// <summary>The worker's services over this database (no hosted service started).</summary>
    public ServiceProvider WorkerServices(Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:TicketDesk"] = _cs,
            ["Notifications:ServiceBus:ConnectionString"] = "Endpoint=sb://localhost:1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        };
        foreach (var (k, v) in extra ?? []) settings[k] = v;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(config);
        services.AddWorkerServices(config);
        return services.BuildServiceProvider();
    }
}

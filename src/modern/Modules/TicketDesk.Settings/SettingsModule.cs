using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TicketDesk.Settings;

public static class SettingsModule
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IApplicationSettingsStore, ApplicationSettingsStore>();
        services.AddScoped<IPushNotificationSettingsStore, PushNotificationSettingsStore>();
        // PushNotifications:Secrets:<provider>:<field> (e.g. PushNotifications__Secrets__Smtp__SmtpPassword)
        services.AddOptions<PushNotificationSecretsOptions>().Configure(o => configuration.GetSection(PushNotificationSecretsOptions.SectionName).Bind(o.Providers));
        services.AddSingleton<Microsoft.Extensions.Options.IOptionsChangeTokenSource<PushNotificationSecretsOptions>>(
            new Microsoft.Extensions.Options.ConfigurationChangeTokenSource<PushNotificationSecretsOptions>(configuration));
        return services;
    }
}

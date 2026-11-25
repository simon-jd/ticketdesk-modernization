// Added for the ASP.NET Core migration (System.Web adapters). Distributed under the Ms-PL like the rest of TicketDesk.

using System;
using System.Configuration;
using System.Diagnostics;
using System.Web;
using Microsoft.AspNetCore.SystemWebAdapters.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Web.Client
{
    /// <summary>
    /// Server side of the System.Web adapters, so the new ASP.NET Core app (in front of this one as a YARP gateway) can share
    /// sign-in with it during the incremental migration:
    /// <list type="bullet">
    /// <item>remote-app server + <b>remote authentication</b> server: GET /systemweb-adapters/authenticate with the shared API key
    /// returns the signed-in user's ClaimsPrincipal (name, roles) for the cookie the browser sent; </item>
    /// <item>proxy support: X-Forwarded-Host/-Proto from the gateway are honoured, so redirects and absolute URLs this app builds
    /// point at the gateway's public host. X-Forwarded-For is not (see <see cref="ForwardedForGuardModule"/>).</item>
    /// </list>
    /// No remote <b>session</b>: TicketDesk uses neither Session nor TempData (docs/migration/inventory.md §6), so there is
    /// nothing to share and no session endpoint is exposed.
    /// </summary>
    public static class SystemWebAdaptersConfig
    {
        /// <summary>appSettings key with the shared API key (a non-empty GUID). Written by the deploy step, never in the repository.</summary>
        public const string ApiKeySetting = "ticketdesk:RemoteAppApiKey";

        /// <summary>appSettings key; "true" lets X-Forwarded-For replace REMOTE_ADDR (off by default).</summary>
        public const string TrustForwardedForSetting = "ticketdesk:RemoteAppTrustForwardedFor";

        public static bool RemoteAuthenticationEnabled { get; private set; }

        public static void Register()
        {
            var apiKey = (ConfigurationManager.AppSettings[ApiKeySetting] ?? string.Empty).Trim();
            var trustForwardedFor = "true".Equals(ConfigurationManager.AppSettings[TrustForwardedForSetting], StringComparison.OrdinalIgnoreCase);
            Guid parsed;
            RemoteAuthenticationEnabled = Guid.TryParse(apiKey, out parsed) && parsed != Guid.Empty;
            if (apiKey.Length > 0 && !RemoteAuthenticationEnabled)
            {
                // The adapters reject keys that are not 32 hex digits; an invalid key would fail every request, so the
                // endpoint stays off instead and the rest of the app runs as before.
                Trace.TraceWarning("{0} is set but is not a non-empty GUID; remote authentication is disabled.", ApiKeySetting);
            }

            HttpApplicationHost.RegisterHost(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddEventLog();

                // Modules run their BeginRequest handlers in registration order: the guard must come before the proxy module.
                if (!trustForwardedFor)
                {
                    builder.Services.AddScoped<IHttpModule, ForwardedForGuardModule>();
                }

                var adapters = builder.Services.AddSystemWebAdapters()
                    .AddProxySupport(options => options.UseForwardedHeaders = true);

                if (RemoteAuthenticationEnabled)
                {
                    adapters.AddRemoteAppServer(options => options.ApiKey = apiKey)
                        .AddAuthenticationServer();
                }
            });
        }
    }
}

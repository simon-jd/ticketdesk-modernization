// TicketDesk - Attribution notice
// Contributor(s):
//
//      TicketDesk modernization lab (AKS round, 2026)
//
// This file is distributed under the terms of the Microsoft Public
// License (Ms-PL). See http://opensource.org/licenses/MS-PL
// for the complete terms of use.
//
// For any distribution that contains code from this file, this notice of
// attribution must remain intact, and a copy of the license must be
// provided to the recipient.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace TicketDesk.Domain.Infrastructure
{
    /// <summary>
    /// Microsoft Entra access tokens of the host's managed identity from the Azure instance metadata service (IMDS), one cache
    /// entry per resource (Azure SQL, Key Vault). A token is fetched again five minutes before it expires; IMDS errors
    /// (429/5xx, or 404 while a new identity propagates) are retried with back-off. Used by
    /// <see cref="ManagedIdentitySqlTokenInterceptor"/> (EF6 and the shared key ring's raw connections) and by the Data
    /// Protection key-wrap credential of the web app.
    /// </summary>
    public sealed class ManagedIdentityTokens
    {
        private const string ImdsTokenUrl = "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=";
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

        private readonly object _gate = new object();
        private readonly string _clientId;
        private readonly Dictionary<string, Tuple<string, DateTimeOffset>> _cache = new Dictionary<string, Tuple<string, DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);

        /// <param name="clientId">A user-assigned identity's client id; null or empty: the system-assigned identity.</param>
        public ManagedIdentityTokens(string clientId)
        {
            _clientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();
        }

        /// <summary>The access token and its expiry for <paramref name="resource"/> (for example https://database.windows.net/).</summary>
        public Tuple<string, DateTimeOffset> GetToken(string resource)
        {
            lock (_gate)
            {
                Tuple<string, DateTimeOffset> cached;
                if (_cache.TryGetValue(resource, out cached) && DateTimeOffset.UtcNow < cached.Item2 - RefreshMargin)
                {
                    return cached;
                }
                Exception last = null;
                for (var attempt = 1; attempt <= 4; attempt++)
                {
                    try
                    {
                        var url = ImdsTokenUrl + Uri.EscapeDataString(resource) +
                                  (_clientId == null ? string.Empty : "&client_id=" + Uri.EscapeDataString(_clientId));
                        var request = (HttpWebRequest)WebRequest.Create(url);
                        request.Headers["Metadata"] = "true";
                        request.Proxy = null; // IMDS is link-local: never through a proxy
                        request.Timeout = 10000;
                        using (var response = (HttpWebResponse)request.GetResponse())
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            var json = JObject.Parse(reader.ReadToEnd());
                            var token = (string)json["access_token"];
                            var expiresOn = Convert.ToInt64(((JValue)json["expires_on"]).Value, CultureInfo.InvariantCulture);
                            if (string.IsNullOrEmpty(token))
                            {
                                throw new InvalidOperationException("IMDS answered without an access token");
                            }
                            var entry = Tuple.Create(token, DateTimeOffset.FromUnixTimeSeconds(expiresOn));
                            _cache[resource] = entry;
                            return entry;
                        }
                    }
                    catch (Exception e)
                    {
                        last = e;
                        Thread.Sleep(500 * attempt);
                    }
                }
                throw new InvalidOperationException(
                    "No access token for " + resource + " from the managed identity (instance metadata service): " + last.Message, last);
            }
        }
    }
}

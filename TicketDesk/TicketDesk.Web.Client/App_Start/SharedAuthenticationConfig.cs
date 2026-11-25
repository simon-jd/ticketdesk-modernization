// Added for the ASP.NET Core migration (shared sign-in). Distributed under the Ms-PL like the rest of TicketDesk.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Xml.Linq;
using Azure.Extensions.AspNetCore.DataProtection.Keys;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Interop;
using TicketDesk.Domain.Infrastructure;

namespace TicketDesk.Web.Client
{
    /// <summary>
    /// One sign-in cookie for the legacy app and the new ASP.NET Core app while they coexist. With
    /// <c>ticketdesk:SharedCookieKeyRing=database</c> the OWIN cookie (<c>.AspNet.ApplicationCookie</c>, name and options
    /// unchanged) is written and read in ASP.NET Core's ticket format (Microsoft.Owin.Security.Interop), protected with the
    /// ASP.NET Core Data Protection key ring stored in <c>dbo.DataProtectionKeys</c> — the table the new app uses
    /// (PersistKeysToDbContext). Either app can then issue the cookie and both accept it. Without the setting the cookie
    /// stays protected by the IIS machine key as before.
    /// <para>On Azure (AKS round): with <c>ticketdesk:DataProtectionKeyVaultKeyId</c> every new key of the ring is stored wrapped
    /// with that Key Vault key, through the VM's managed identity (<c>ticketdesk:KeyVaultManagedIdentityClientId</c> for a
    /// user-assigned one), exactly like the new app (<c>DataProtection:KeyVaultKeyId</c>), so each app unwraps what the other
    /// wrote. On Azure SQL the key ring's own connections get the managed identity's token like EF6's
    /// (<see cref="ManagedIdentitySqlTokenInterceptor.ApplyAccessToken"/>).</para>
    /// </summary>
    public static class SharedAuthenticationConfig
    {
        /// <summary>appSettings key; "database" turns the shared cookie on. Written by the deploy step.</summary>
        public const string KeyRingSetting = "ticketdesk:SharedCookieKeyRing";

        /// <summary>Data Protection application name of both apps.</summary>
        public const string ApplicationName = "TicketDesk";

        /// <summary>Purposes of the new app's Identity cookie protector (ASP.NET Core cookie handler, scheme Identity.Application).</summary>
        private static readonly string[] CookiePurposes =
            { "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", "Identity.Application", "v2" };

        /// <summary>appSettings key: URI of the Key Vault key that wraps the key ring (https://&lt;vault&gt;.vault.azure.net/keys/&lt;name&gt;); empty: off.</summary>
        public const string KeyVaultKeySetting = "ticketdesk:DataProtectionKeyVaultKeyId";

        /// <summary>appSettings key: client id of a user-assigned identity for Key Vault; empty: the system-assigned identity.</summary>
        public const string KeyVaultClientIdSetting = "ticketdesk:KeyVaultManagedIdentityClientId";

        public static bool Enabled
        {
            get { return "database".Equals((ConfigurationManager.AppSettings[KeyRingSetting] ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }
        }

        public static ISecureDataFormat<AuthenticationTicket> CreateTicketFormat()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["TicketDesk"].ConnectionString;
            var services = new ServiceCollection();
            var dataProtection = services.AddDataProtection()
                .SetApplicationName(ApplicationName)
                .AddKeyManagementOptions(o => o.XmlRepository = new SqlKeyRingRepository(connectionString));
            var keyId = (ConfigurationManager.AppSettings[KeyVaultKeySetting] ?? "").Trim();
            if (keyId.Length > 0)
            {
                Uri keyUri;
                if (!Uri.TryCreate(keyId, UriKind.Absolute, out keyUri) || keyUri.Scheme != Uri.UriSchemeHttps ||
                    !keyUri.AbsolutePath.StartsWith("/keys/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConfigurationErrorsException(KeyVaultKeySetting + " must be the https URI of a Key Vault key");
                }
                dataProtection.ProtectKeysWithAzureKeyVault(keyUri, new ManagedIdentityTokenCredential(ConfigurationManager.AppSettings[KeyVaultClientIdSetting]));
            }
            var provider = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
            var protector = provider.CreateProtector(CookiePurposes[0], CookiePurposes[1], CookiePurposes[2]);
            return new AspNetTicketDataFormat(new DataProtectorShim(protector));
        }
    }

    /// <summary>
    /// <see cref="IXmlRepository"/> over <c>dbo.DataProtectionKeys</c> (Id, FriendlyName, Xml): the same rows EF Core's
    /// DataProtectionKey entity reads and writes in the new app (td-db script V0010 creates the table).
    /// </summary>
    public sealed class SqlKeyRingRepository : IXmlRepository
    {
        private readonly string _connectionString;

        public SqlKeyRingRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            var elements = new List<XElement>();
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("SELECT Xml FROM dbo.DataProtectionKeys WHERE Xml IS NOT NULL ORDER BY Id", connection))
            {
                ManagedIdentitySqlTokenInterceptor.ApplyAccessToken(connection); // Azure SQL: the managed identity's token
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) elements.Add(XElement.Parse(reader.GetString(0)));
                }
            }
            return new ReadOnlyCollection<XElement>(elements);
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("INSERT dbo.DataProtectionKeys (FriendlyName, Xml) VALUES (@friendlyName, @xml)", connection))
            {
                command.Parameters.AddWithValue("@friendlyName", (object)friendlyName ?? DBNull.Value);
                command.Parameters.AddWithValue("@xml", element.ToString(SaveOptions.DisableFormatting));
                ManagedIdentitySqlTokenInterceptor.ApplyAccessToken(connection);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}

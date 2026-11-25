// TicketDesk - Attribution notice
// Contributor(s):
//
//      TicketDesk modernization lab (Azure SQL re-platform, 2026)
//
// This file is distributed under the terms of the Microsoft Public
// License (Ms-PL). See http://opensource.org/licenses/MS-PL
// for the complete terms of use.
//
// For any distribution that contains code from this file, this notice of
// attribution must remain intact, and a copy of the license must be
// provided to the recipient.

using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Data.SqlClient;
using System.Threading;

namespace TicketDesk.Domain.Infrastructure
{
    /// <summary>
    /// EF6 connection interceptor that signs the connections EF6 opens to Azure SQL Database in with a Microsoft Entra access
    /// token of the host's managed identity, read from the Azure instance metadata service (IMDS). The connection string then
    /// carries no user name or password, and the server can run with Entra-only authentication.
    /// </summary>
    /// <remarks>
    /// <para>Off unless appSettings <c>ticketdesk:SqlAuthentication</c> is <c>ManagedIdentity</c> (the deploy step sets it on the
    /// Azure VM). <c>ticketdesk:SqlManagedIdentityClientId</c> selects a user-assigned identity; empty means the VM's
    /// system-assigned identity.</para>
    /// <para>Only a closed <see cref="SqlConnection"/> to <c>*.database.windows.net</c> whose connection string has no
    /// credentials of its own (no integrated security, user id, password or Authentication keyword) and no token yet gets one,
    /// so SQL Server Express with integrated security keeps working unchanged. The token is valid for hours: it is cached and
    /// fetched again five minutes before it expires (<see cref="ManagedIdentityTokens"/>). SqlClient keys its pools by connection string and token, so a new token
    /// starts a new pool and the old one drains.</para>
    /// <para>EF 6.1's <c>Database.Exists()</c> (run by the app's startup check and by the migrations initializer) always
    /// probes <c>master</c> (<c>SELECT Count(*) FROM sys.databases WHERE [name]=...</c>), and a contained database user
    /// cannot log in to <c>master</c>: EF then throws "credentials missing for master connection" and the app answers
    /// 500. Such a probe is opened against the app's own database instead (the Initial Catalog of the TicketDesk
    /// connection string), where <c>sys.databases</c> lists that database too, so the answer is the same.</para>
    /// </remarks>
    public sealed class ManagedIdentitySqlTokenInterceptor : IDbConnectionInterceptor
    {
        public const string ModeSetting = "ticketdesk:SqlAuthentication";
        public const string ClientIdSetting = "ticketdesk:SqlManagedIdentityClientId";
        private const string Resource = "https://database.windows.net/";
        private static int _registered;
        private static ManagedIdentitySqlTokenInterceptor _current;

        private readonly ManagedIdentityTokens _tokens;
        private readonly string _database;

        public ManagedIdentitySqlTokenInterceptor(string clientId, string database)
        {
            _tokens = new ManagedIdentityTokens(clientId);
            _database = string.IsNullOrWhiteSpace(database) ? null : database.Trim();
        }

        /// <summary>Registers the interceptor once per app domain when the appSettings ask for it; returns whether it is active.</summary>
        public static bool RegisterFromAppSettings()
        {
            var mode = ConfigurationManager.AppSettings[ModeSetting];
            if (!string.Equals(mode, "ManagedIdentity", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (Interlocked.Exchange(ref _registered, 1) == 0)
            {
                var cs = ConfigurationManager.ConnectionStrings["TicketDesk"];
                var database = cs == null ? null : new SqlConnectionStringBuilder(cs.ConnectionString).InitialCatalog;
                var interceptor = new ManagedIdentitySqlTokenInterceptor(ConfigurationManager.AppSettings[ClientIdSetting], database);
                DbInterception.Add(interceptor);
                Volatile.Write(ref _current, interceptor);
            }
            return true;
        }

        /// <summary>
        /// The same token for a <see cref="SqlConnection"/> that code opens itself, outside EF6 (the shared sign-in's key ring
        /// repository, SharedAuthenticationConfig): a no-op unless <c>ticketdesk:SqlAuthentication</c> is <c>ManagedIdentity</c>
        /// and the connection goes to Azure SQL without credentials of its own. Call it before <c>Open()</c>.
        /// </summary>
        public static void ApplyAccessToken(SqlConnection connection)
        {
            if (connection == null || !RegisterFromAppSettings())
            {
                return;
            }
            SpinWait.SpinUntil(() => Volatile.Read(ref _current) != null, TimeSpan.FromSeconds(5)); // a concurrent first registration
            var current = Volatile.Read(ref _current);
            if (current != null && connection.State == ConnectionState.Closed && string.IsNullOrEmpty(connection.AccessToken) &&
                NeedsToken(connection.ConnectionString))
            {
                connection.AccessToken = current.GetToken();
            }
        }

        /// <summary>True for an Azure SQL Database endpoint whose connection string brings no credentials of its own.</summary>
        public static bool NeedsToken(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return false;
            }
            var b = new SqlConnectionStringBuilder(connectionString);
            if (b.IntegratedSecurity || !string.IsNullOrEmpty(b.UserID) || !string.IsNullOrEmpty(b.Password) ||
                b.Authentication != SqlAuthenticationMethod.NotSpecified)
            {
                return false;
            }
            var host = b.DataSource ?? string.Empty;
            if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring(4);
            }
            var comma = host.IndexOf(',');
            if (comma >= 0)
            {
                host = host.Substring(0, comma);
            }
            return host.Trim().EndsWith(".database.windows.net", StringComparison.OrdinalIgnoreCase);
        }

        public void Opening(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
            var sql = connection as SqlConnection;
            if (sql == null || sql.State != ConnectionState.Closed || !string.IsNullOrEmpty(sql.AccessToken) || !NeedsToken(sql.ConnectionString))
            {
                return;
            }
            if (_database != null)
            {
                var b = new SqlConnectionStringBuilder(sql.ConnectionString);
                if (string.Equals(b.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
                {
                    b.InitialCatalog = _database; // EF6's existence probe: see remarks
                    sql.ConnectionString = b.ConnectionString;
                }
            }
            sql.AccessToken = GetToken();
        }

        private string GetToken()
        {
            return _tokens.GetToken(Resource).Item1;
        }

        // The other interception points are not used.
        public void BeginningTransaction(DbConnection connection, BeginTransactionInterceptionContext interceptionContext) { }
        public void BeganTransaction(DbConnection connection, BeginTransactionInterceptionContext interceptionContext) { }
        public void Closing(DbConnection connection, DbConnectionInterceptionContext interceptionContext) { }
        public void Closed(DbConnection connection, DbConnectionInterceptionContext interceptionContext) { }
        public void ConnectionStringGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void ConnectionStringGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void ConnectionStringSetting(DbConnection connection, DbConnectionPropertyInterceptionContext<string> interceptionContext) { }
        public void ConnectionStringSet(DbConnection connection, DbConnectionPropertyInterceptionContext<string> interceptionContext) { }
        public void ConnectionTimeoutGetting(DbConnection connection, DbConnectionInterceptionContext<int> interceptionContext) { }
        public void ConnectionTimeoutGot(DbConnection connection, DbConnectionInterceptionContext<int> interceptionContext) { }
        public void DatabaseGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void DatabaseGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void DataSourceGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void DataSourceGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void Disposing(DbConnection connection, DbConnectionInterceptionContext interceptionContext) { }
        public void Disposed(DbConnection connection, DbConnectionInterceptionContext interceptionContext) { }
        public void EnlistingTransaction(DbConnection connection, EnlistTransactionInterceptionContext interceptionContext) { }
        public void EnlistedTransaction(DbConnection connection, EnlistTransactionInterceptionContext interceptionContext) { }
        public void Opened(DbConnection connection, DbConnectionInterceptionContext interceptionContext) { }
        public void ServerVersionGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void ServerVersionGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext) { }
        public void StateGetting(DbConnection connection, DbConnectionInterceptionContext<ConnectionState> interceptionContext) { }
        public void StateGot(DbConnection connection, DbConnectionInterceptionContext<ConnectionState> interceptionContext) { }
    }
}

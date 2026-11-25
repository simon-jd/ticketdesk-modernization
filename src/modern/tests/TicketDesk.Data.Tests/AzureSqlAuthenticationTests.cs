using Microsoft.Data.SqlClient;

namespace TicketDesk.Data.Tests;

/// <summary>
/// AKS round: the web app and the worker reach Azure SQL as the pods' workload identity, with no password in the connection
/// string. SqlClient 7 ships the Entra providers in Microsoft.Data.SqlClient.Extensions.Azure (referenced by TicketDesk.Data).
/// </summary>
public sealed class AzureSqlAuthenticationTests
{
    [Theory]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
    public void Entra_authentication_providers_are_available(SqlAuthenticationMethod method) =>
        Assert.NotNull(SqlAuthenticationProvider.GetProvider(method));

    [Fact]
    public void The_AKS_connection_string_carries_no_secret()
    {
        var b = new SqlConnectionStringBuilder(
            "Server=tcp:sql-tdlab-000000.database.windows.net,1433;Database=sqldb-ticketdesk;Authentication=Active Directory Workload Identity;Encrypt=True;Connect Timeout=30");
        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, b.Authentication);
        Assert.Equal("", b.Password);
        Assert.Equal("", b.UserID);
    }
}

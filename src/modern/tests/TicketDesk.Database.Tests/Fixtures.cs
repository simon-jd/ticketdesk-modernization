using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Attachments;
using TicketDesk.Data;
using TicketDesk.Reports;
using TicketDesk.Testing;
using TicketDesk.Tickets;
using TicketDesk.Users;

namespace TicketDesk.Database.Tests;

/// <summary>Scratch database with the legacy schema only (no td-db migrations): where a migration test starts.</summary>
public sealed class LegacyOnlyDatabase : ScratchDatabase
{
    protected override bool ApplyModernSchema => false;
}

/// <summary>
/// Scratch database at the expand stage (no contract): the expand scripts' own tests and the legacy write batches they served.
/// The app itself runs on the contracted schema since Round 4 (ScratchDatabase applies it).
/// </summary>
public sealed class ExpandOnlyDatabase : ScratchDatabase
{
    protected override bool ApplyContract => false;
}

/// <summary>A scratch database for one test, dropped when the test disposes it.</summary>
public static class TempDatabase
{
    public static async Task<T> CreateAsync<T>() where T : ScratchDatabase, new()
    {
        Assert.SkipUnless(SqlTestEnvironment.ServerConnection != null, SqlTestEnvironment.SkipReason);
        var db = new T();
        await db.InitializeAsync();
        return db;
    }
}

/// <summary>The app's query services (tickets, reports) over one database, as the web host registers them.</summary>
public sealed class AppServices : IAsyncDisposable
{
    private readonly ServiceProvider _sp;

    public AppServices(string connectionString)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TicketDesk"] = connectionString,
            ["Attachments:RootPath"] = Path.Combine(Path.GetTempPath(), "td-db-tests-no-attachments"),
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment, TestHostEnvironment>();
        services.AddDbContext<TicketDeskDbContext>(o => o.UseTicketDeskSqlServer(connectionString));
        services.AddAuthorizationCore();
        services.AddUsersModule().AddAttachmentsModule(config).AddTicketsModule().AddReportsModule();
        services.AddScoped<LinqReportQueries>();
        _sp = services.BuildServiceProvider(validateScopes: true);
    }

    public async Task<T> Run<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _sp.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public ValueTask DisposeAsync() => _sp.DisposeAsync();

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
    }
}

internal static class SqlHelpers
{
    public static async Task<int> ExecAsync(string conn, string sql, params (string, object?)[] p)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = new SqlCommand(sql, c);
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync();
    }
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TicketDesk.Testing;

[assembly: AssemblyFixture(typeof(ScratchDatabase))]
[assembly: AssemblyFixture(typeof(TicketDesk.Data.Tests.SeededDatabase))]

namespace TicketDesk.Data.Tests;

/// <summary>The database seeded by ops/dev/seed.sh (read-only use).</summary>
public sealed class SeededDatabase : IAsyncLifetime
{
    public string? ConnectionString { get; private set; }
    public async ValueTask InitializeAsync() => ConnectionString = await SqlTestEnvironment.SeededDatabaseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal static class Db
{
    public static TicketDeskDbContext Create(string connectionString, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<TicketDeskDbContext>();
        options.UseTicketDeskSqlServer(connectionString);
        if (interceptors.Length > 0) options.AddInterceptors(interceptors);
        return new TicketDeskDbContext(options.Options);
    }

    /// <summary>A context that never connects (model-only tests).</summary>
    public static TicketDeskDbContext ModelOnly() => Create("Server=127.0.0.1,1;Database=none;User Id=x;Password=y;TrustServerCertificate=True");
}

/// <summary>Counts the SQL commands EF sends.</summary>
internal sealed class CommandCounter : DbCommandInterceptor
{
    public int Count;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

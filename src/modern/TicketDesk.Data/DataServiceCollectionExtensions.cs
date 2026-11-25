using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TicketDesk.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Same connection-string name as the legacy Web.config.</summary>
    public const string ConnectionStringName = "TicketDesk";

    /// <summary>
    /// Registers <see cref="TicketDeskDbContext"/>. The connection string is read when a context is created, so the host
    /// starts without one (the readiness check then reports the database as unhealthy).
    /// </summary>
    public static IServiceCollection AddTicketDeskData(this IServiceCollection services)
    {
        services.AddDbContext<TicketDeskDbContext>((sp, options) =>
        {
            var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is not configured.");
            options.UseTicketDeskSqlServer(cs);
        });
        return services;
    }

    public static DbContextOptionsBuilder UseTicketDeskSqlServer(this DbContextOptionsBuilder options, string connectionString)
    {
        return options
            .UseSqlServer(connectionString)
            // EF6 always loaded Include()d collections in one query. EF Core makes the choice explicit here:
            // a query with several collection Includes must say AsSplitQuery() or AsSingleQuery(), otherwise it throws.
            .ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
    }
}

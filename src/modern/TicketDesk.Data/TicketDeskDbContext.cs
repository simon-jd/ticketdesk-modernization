using Microsoft.EntityFrameworkCore;
using TicketDesk.Data.Entities;

namespace TicketDesk.Data;

/// <summary>
/// One EF Core context over the shared legacy database (EF6 used three: domain, Identity, push notifications).
/// It never creates or migrates schema: no migrations exist and nothing calls EnsureCreated/Migrate. The
/// <c>notifications.*</c> tables (push notifications) are mapped too (<c>NotificationConfigurations</c>).
/// </summary>
public sealed class TicketDeskDbContext(DbContextOptions<TicketDeskDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<TicketEventNotification> TicketEventNotifications => Set<TicketEventNotification>();
    public DbSet<TicketSubscriber> TicketSubscribers => Set<TicketSubscriber>();
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    public DbSet<TicketDeskUser> Users => Set<TicketDeskUser>();
    public DbSet<TicketDeskRole> Roles => Set<TicketDeskRole>();
    public DbSet<TicketDeskUserRole> UserRoles => Set<TicketDeskUserRole>();
    public DbSet<TicketDeskUserClaim> UserClaims => Set<TicketDeskUserClaim>();
    public DbSet<TicketDeskUserLogin> UserLogins => Set<TicketDeskUserLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Configurations.MappingConventions.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketDeskDbContext).Assembly);
    }
}

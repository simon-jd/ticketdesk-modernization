using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketDesk.Data.Entities;

namespace TicketDesk.Data.Configurations;

// Mapped to the TicketDesk 2.5 schema as EF6 left it (model snapshot of Td2.5.022 and the Td2.5.017 FK). No schema
// changes: indexes and constraint names stay owned by the database and are not part of this model.

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects", MappingConventions.Schema);
        b.HasKey(p => p.ProjectId);
        b.Property(p => p.ProjectName).HasMaxLength(100).IsRequired();
        b.Property(p => p.ProjectDescription).HasMaxLength(500);
        b.Property(p => p.Version).IsRowVersion();
    }
}

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        // No trigger since the contract stage (td-db C0001, after the legacy app was retired): the expand trigger
        // TR_Tickets_AssignedDate kept AssignedDate right for legacy writes and made EF Core avoid UPDATE ... OUTPUT
        // (HasTrigger), which SQL Server rejects on a table with triggers. Now the ticket writer sets AssignedDate itself,
        // CK_Tickets_AssignedDate enforces it, and EF Core reads generated values with OUTPUT again. This model therefore needs
        // a contracted database (td-db migrate --contract --legacy-retired); on an expand-only one, ticket updates fail (334).
        b.ToTable("Tickets", MappingConventions.Schema);
        b.HasKey(t => t.TicketId);
        // The column has DEFAULT ((1)) but EF6 always wrote ProjectId (DatabaseGenerated.None), so no HasDefaultValue here:
        // EF Core would otherwise skip writing 0 and let the default silently move the ticket to project 1.
        b.Property(t => t.ProjectId);
        b.Property(t => t.TicketType).HasMaxLength(50).IsRequired();
        b.Property(t => t.Category).HasMaxLength(50).IsRequired();
        b.Property(t => t.Title).HasMaxLength(500).IsRequired();
        b.Property(t => t.Details).IsRequired();
        b.Property(t => t.TagList).HasMaxLength(100);
        b.Property(t => t.CreatedBy).HasMaxLength(256).IsRequired();
        b.Property(t => t.CreatedDate).DatabaseDefaultNeverWritten("SYSDATETIMEOFFSET()");
        b.Property(t => t.EstimatedDuration).HasPrecision(18, 2);
        b.Property(t => t.ActualDuration).HasPrecision(18, 2);
        b.Property(t => t.Owner).HasMaxLength(256).IsRequired();
        b.Property(t => t.AssignedTo).HasMaxLength(256);
        b.Property(t => t.TicketStatus).HasConversion<int>();
        b.Property(t => t.CurrentStatusSetBy).HasMaxLength(256).IsRequired();
        b.Property(t => t.LastUpdateBy).HasMaxLength(256).IsRequired();
        b.Property(t => t.Priority).HasMaxLength(25);
        b.Property(t => t.Version).IsRowVersion();

        // Td2.5.017 created this FK without cascade (the EF6 model snapshot claims cascade; the database wins).
        b.HasOne(t => t.Project).WithMany(p => p.Tickets).HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketEventConfiguration : IEntityTypeConfiguration<TicketEvent>
{
    public void Configure(EntityTypeBuilder<TicketEvent> b)
    {
        b.ToTable("TicketEvents", MappingConventions.Schema);
        b.HasKey(e => new { e.TicketId, e.EventId });
        b.Property(e => e.TicketId).ValueGeneratedNever();
        // identity column inside a composite key: EF Core only applies the identity convention to single-column keys
        b.Property(e => e.EventId).UseIdentityColumn();
        b.Property(e => e.EventDescription).HasMaxLength(500);
        b.Property(e => e.EventBy).HasMaxLength(256).IsRequired();
        b.Property(e => e.EventDate).DatabaseDefaultNeverWritten("SYSDATETIMEOFFSET()");
        // DEFAULT ((16384)) exists for rows older than Td2.5.019; EF6 always wrote the value, so no HasDefaultValue.
        b.Property(e => e.ForActivity).HasConversion<int>();
        b.Property(e => e.Version).IsRowVersion();

        b.HasOne(e => e.Ticket).WithMany(t => t.TicketEvents).HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TicketSubscriberConfiguration : IEntityTypeConfiguration<TicketSubscriber>
{
    public void Configure(EntityTypeBuilder<TicketSubscriber> b)
    {
        b.ToTable("TicketSubscribers", MappingConventions.Schema);
        b.HasKey(s => new { s.TicketId, s.SubscriberId });
        b.Property(s => s.SubscriberId).HasMaxLength(256);
        b.HasOne(s => s.Ticket).WithMany(t => t.TicketSubscribers).HasForeignKey(s => s.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TicketEventNotificationConfiguration : IEntityTypeConfiguration<TicketEventNotification>
{
    public void Configure(EntityTypeBuilder<TicketEventNotification> b)
    {
        b.ToTable("TicketEventNotifications", MappingConventions.Schema);
        b.HasKey(n => new { n.TicketId, n.EventId, n.SubscriberId });
        b.Property(n => n.SubscriberId).HasMaxLength(256);
        b.Property(n => n.Version).IsRowVersion();

        b.HasOne(n => n.TicketEvent).WithMany(e => e.TicketEventNotifications)
            .HasForeignKey(n => new { n.TicketId, n.EventId }).OnDelete(DeleteBehavior.Cascade);
        // TdDomainContext.OnModelCreating: WillCascadeOnDelete(false)
        b.HasOne(n => n.TicketSubscriber).WithMany()
            .HasForeignKey(n => new { n.TicketId, n.SubscriberId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> b)
    {
        b.ToTable("TicketTags", MappingConventions.Schema);
        b.HasKey(t => t.TicketTagId);
        b.Property(t => t.TagName).HasMaxLength(100).IsRequired();
        b.HasOne(t => t.Ticket).WithMany(t => t.TicketTags).HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> b)
    {
        b.ToTable("UserSettings", MappingConventions.Schema);
        b.HasKey(s => s.UserId);
        b.Property(s => s.UserId).HasMaxLength(256);
    }
}

internal sealed class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> b)
    {
        b.ToTable("ApplicationSettings", MappingConventions.Schema);
        b.HasKey(s => s.ApplicationName);
        b.Property(s => s.ApplicationName).HasMaxLength(128);
    }
}

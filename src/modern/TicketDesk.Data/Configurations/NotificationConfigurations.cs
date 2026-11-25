using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketDesk.Data.Entities;

namespace TicketDesk.Data.Configurations;

// notifications.* as the legacy push-notification context left it (columns, keys and FKs checked by SchemaMappingTests
// against the restored legacy database).

internal static class NotificationSchema
{
    public const string Name = "notifications";
}

// Lengths are characters (sys.columns.max_length is bytes: nvarchar(256) = 512), as SchemaMappingTests compares them.
internal sealed class ApplicationPushNotificationSettingConfiguration : IEntityTypeConfiguration<ApplicationPushNotificationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationPushNotificationSetting> b)
    {
        b.ToTable("ApplicationPushNotificationSettings", NotificationSchema.Name);
        b.HasKey(s => s.ApplicationName);
        b.Property(s => s.ApplicationName).HasMaxLength(128);
    }
}

internal sealed class SubscriberPushNotificationSettingConfiguration : IEntityTypeConfiguration<SubscriberPushNotificationSetting>
{
    public void Configure(EntityTypeBuilder<SubscriberPushNotificationSetting> b)
    {
        b.ToTable("SubscriberPushNotificationSettings", NotificationSchema.Name);
        b.HasKey(s => s.SubscriberId);
        b.Property(s => s.SubscriberId).HasMaxLength(256);
        b.HasMany(s => s.PushNotificationDestinations).WithOne().HasForeignKey(d => d.SubscriberId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PushNotificationDestinationConfiguration : IEntityTypeConfiguration<PushNotificationDestination>
{
    public void Configure(EntityTypeBuilder<PushNotificationDestination> b)
    {
        b.ToTable("PushNotificationDestinations", NotificationSchema.Name);
        b.HasKey(d => d.DestinationId);
        b.Property(d => d.SubscriberName).HasMaxLength(100);
        b.Property(d => d.DestinationAddress).HasMaxLength(256);
        b.Property(d => d.DestinationType).HasMaxLength(50);
        b.Property(d => d.SubscriberId).HasMaxLength(256);
    }
}

internal sealed class PushNotificationItemConfiguration : IEntityTypeConfiguration<PushNotificationItem>
{
    public void Configure(EntityTypeBuilder<PushNotificationItem> b)
    {
        b.ToTable("PushNotificationItems", NotificationSchema.Name);
        b.HasKey(i => i.PushNotificationItemId);
        b.Property(i => i.ContentSourceType).HasMaxLength(100).IsRequired();
        b.Property(i => i.SubscriberId).HasMaxLength(256).IsRequired();
        b.HasOne<PushNotificationDestination>().WithMany().HasForeignKey(i => i.DestinationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TicketPushNotificationItemConfiguration : IEntityTypeConfiguration<TicketPushNotificationItem>
{
    public void Configure(EntityTypeBuilder<TicketPushNotificationItem> b)
    {
        // EF6 table-per-type child of PushNotificationItems: same key, FK without cascade
        b.ToTable("TicketPushNotificationItems", NotificationSchema.Name);
        b.HasKey(i => i.PushNotificationItemId);
        b.Property(i => i.PushNotificationItemId).ValueGeneratedNever();
        b.HasOne<PushNotificationItem>().WithOne().HasForeignKey<TicketPushNotificationItem>(i => i.PushNotificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

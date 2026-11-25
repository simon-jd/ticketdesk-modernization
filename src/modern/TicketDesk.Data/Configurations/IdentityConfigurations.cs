using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketDesk.Data.Entities;

namespace TicketDesk.Data.Configurations;

// ASP.NET Identity 2.2.1 tables as TdIdentityContext named them (IdentityUsers, IdentityRoles, ...).

internal sealed class UserConfiguration : IEntityTypeConfiguration<TicketDeskUser>
{
    public void Configure(EntityTypeBuilder<TicketDeskUser> b)
    {
        b.ToTable("IdentityUsers", MappingConventions.Schema);
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasMaxLength(128);
        b.Property(u => u.UserName).HasMaxLength(256).IsRequired();
        b.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256);
        b.Property(u => u.LockoutEndDateUtc).HasColumnType("datetime").HasConversion(MappingConventions.UtcDateTime);

        b.HasMany(u => u.Roles).WithOne(r => r.User).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(u => u.Claims).WithOne(c => c.User).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(u => u.Logins).WithOne(l => l.User).HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<TicketDeskRole>
{
    public void Configure(EntityTypeBuilder<TicketDeskRole> b)
    {
        b.ToTable("IdentityRoles", MappingConventions.Schema);
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasMaxLength(128);
        b.Property(r => r.Name).HasMaxLength(256).IsRequired();
        b.Property(r => r.DisplayName).HasMaxLength(100);
        b.Property(r => r.Description).HasMaxLength(500);
        b.Property(r => r.Discriminator).HasMaxLength(128).IsRequired();
        b.HasMany(r => r.Users).WithOne(ur => ur.Role).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<TicketDeskUserRole>
{
    public void Configure(EntityTypeBuilder<TicketDeskUserRole> b)
    {
        b.ToTable("IdentityUserRoles", MappingConventions.Schema);
        b.HasKey(ur => new { ur.UserId, ur.RoleId });
        b.Property(ur => ur.UserId).HasMaxLength(128);
        b.Property(ur => ur.RoleId).HasMaxLength(128);
    }
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<TicketDeskUserClaim>
{
    public void Configure(EntityTypeBuilder<TicketDeskUserClaim> b)
    {
        b.ToTable("IdentityUserClaims", MappingConventions.Schema);
        b.HasKey(c => c.Id);
        b.Property(c => c.UserId).HasMaxLength(128).IsRequired();
    }
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<TicketDeskUserLogin>
{
    public void Configure(EntityTypeBuilder<TicketDeskUserLogin> b)
    {
        b.ToTable("IdentityUserLogins", MappingConventions.Schema);
        b.HasKey(l => new { l.LoginProvider, l.ProviderKey, l.UserId });
        b.Property(l => l.LoginProvider).HasMaxLength(128);
        b.Property(l => l.ProviderKey).HasMaxLength(128);
        b.Property(l => l.UserId).HasMaxLength(128);
    }
}

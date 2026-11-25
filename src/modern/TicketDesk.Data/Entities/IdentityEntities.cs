namespace TicketDesk.Data.Entities;

/// <summary>
/// Row of <c>dbo.IdentityUsers</c> (ASP.NET Identity 2.2.1 schema plus <c>DisplayName</c>). There are no
/// NormalizedUserName/NormalizedEmail/ConcurrencyStamp columns: the Identity store in TicketDesk.Identity works without them.
/// </summary>
public class TicketDeskUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    /// <summary><c>datetime</c> column, UTC by convention (read back with <see cref="DateTimeKind.Utc"/>).</summary>
    public DateTime? LockoutEndDateUtc { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }

    public ICollection<TicketDeskUserRole> Roles { get; set; } = new List<TicketDeskUserRole>();
    public ICollection<TicketDeskUserClaim> Claims { get; set; } = new List<TicketDeskUserClaim>();
    public ICollection<TicketDeskUserLogin> Logins { get; set; } = new List<TicketDeskUserLogin>();
}

/// <summary>
/// Row of <c>dbo.IdentityRoles</c>. EF6 mapped <c>IdentityRole</c> and <c>TicketDeskRole</c> to this table with TPH;
/// the discriminator is kept as a plain column.
/// </summary>
public class TicketDeskRole
{
    public const string TicketDeskDiscriminator = "TicketDeskRole";
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string Discriminator { get; set; } = TicketDeskDiscriminator;
    public ICollection<TicketDeskUserRole> Users { get; set; } = new List<TicketDeskUserRole>();
}

public class TicketDeskUserRole
{
    public string UserId { get; set; } = "";
    public string RoleId { get; set; } = "";
    public TicketDeskUser? User { get; set; }
    public TicketDeskRole? Role { get; set; }
}

public class TicketDeskUserClaim
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
    public TicketDeskUser? User { get; set; }
}

public class TicketDeskUserLogin
{
    public string LoginProvider { get; set; } = "";
    public string ProviderKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public TicketDeskUser? User { get; set; }
}

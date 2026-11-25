namespace TicketDesk.Identity;

/// <summary>Bound from configuration section <c>Authentication</c>.</summary>
public sealed class TicketDeskAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public EntraIdOptions EntraId { get; set; } = new();
}

/// <summary>
/// Seam for Entra ID sign-in (OpenID Connect). Off until the Entra app registration exists; tenant/client values come
/// from configuration or Key Vault, never from the repository.
/// </summary>
public sealed class EntraIdOptions
{
    public const string Scheme = "EntraId";
    public bool Enabled { get; set; }
    /// <summary>e.g. https://login.microsoftonline.com/{tenant}/v2.0</summary>
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string CallbackPath { get; set; } = "/signin-oidc";
    /// <summary>The directory tenant whose users may sign in (the <c>tid</c> claim); empty: taken from the issuer.</summary>
    public string? TenantId { get; set; }
    /// <summary>False only for a local test identity provider on plain HTTP.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
    /// <summary>Label of the second sign-in button on the sign-in page.</summary>
    public string DisplayName { get; set; } = "Entra ID";
}

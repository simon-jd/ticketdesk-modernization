using Azure.Core.Cryptography;
using Azure.Extensions.AspNetCore.DataProtection.Keys;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;

namespace TicketDesk.Identity;

/// <summary>
/// The Data Protection key ring in the database (<c>dbo.DataProtectionKeys</c>, td-db script V0010): every pod of the new app
/// reads it, so cookies and antiforgery tokens survive restarts and work across replicas. (During coexistence the legacy app
/// read the same table through Microsoft.Owin.Security.Interop for the shared sign-in cookie; that ended with Round 4.)
/// </summary>
public sealed class TicketDeskKeyRingContext(DbContextOptions<TicketDeskKeyRingContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<DataProtectionKey>(e =>
        {
            e.ToTable("DataProtectionKeys", "dbo");
            e.HasKey(k => k.Id);
        });
}

public static class SharedSignIn
{
    /// <summary>Data Protection application name (the default would be the content root path, different per deployment).</summary>
    public const string ApplicationName = "TicketDesk";

    public const string KeyRingSetting = "DataProtection:KeyRing";

    /// <summary>
    /// URI of a Key Vault key (<c>https://&lt;vault&gt;.vault.azure.net/keys/&lt;name&gt;</c>, versionless or versioned). Set: every new
    /// key of the ring is stored wrapped with it (envelope encryption: the key's XML holds the wrapped content key, the Key Vault
    /// key never leaves the vault); keys written before stay readable. Off when empty (local runs, tests).
    /// </summary>
    public const string KeyVaultKeySetting = "DataProtection:KeyVaultKeyId";

    /// <summary>
    /// Data Protection for the app: application name <see cref="ApplicationName"/>; keys in the database
    /// (<c>DataProtection:KeyRing=Database</c>, set in the shipped appsettings.json) or the framework default location;
    /// wrapped with the Key Vault key <see cref="KeyVaultKeySetting"/> when it is set. The credential is
    /// <see cref="DefaultAzureCredential"/>: the pod's workload identity on AKS (set <c>AZURE_TOKEN_CREDENTIALS</c> to
    /// <c>WorkloadIdentityCredential</c> there to skip the rest of the chain). Until its retirement the legacy app wrapped with
    /// the same key through its VM's managed identity (TicketDesk.Web.Client SharedAuthenticationConfig).
    /// </summary>
    public static IServiceCollection AddTicketDeskDataProtection(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTicketDeskDataProtection(configuration, keyResolver: null);

    /// <param name="keyResolver">Tests: resolves the Key Vault key locally instead of through Key Vault.</param>
    internal static IServiceCollection AddTicketDeskDataProtection(this IServiceCollection services, IConfiguration configuration,
        IKeyEncryptionKeyResolver? keyResolver)
    {
        var keyRing = configuration[KeyRingSetting] ?? "Default";
        var dp = services.AddDataProtection().SetApplicationName(ApplicationName);
        if (keyRing.Equals("Database", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<TicketDeskKeyRingContext>((sp, o) =>
            {
                var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString(DataServiceCollectionExtensions.ConnectionStringName);
                if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("The database key ring needs ConnectionStrings:TicketDesk.");
                o.UseSqlServer(cs);
            });
            dp.PersistKeysToDbContext<TicketDeskKeyRingContext>();
        }
        var keyId = configuration[KeyVaultKeySetting];
        if (!string.IsNullOrWhiteSpace(keyId))
        {
            if (!Uri.TryCreate(keyId.Trim(), UriKind.Absolute, out var keyUri) || keyUri.Scheme != Uri.UriSchemeHttps ||
                !keyUri.AbsolutePath.StartsWith("/keys/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{KeyVaultKeySetting} must be the https URI of a Key Vault key (https://<vault>.vault.azure.net/keys/<name>).");
            if (keyResolver is null) dp.ProtectKeysWithAzureKeyVault(keyUri, new DefaultAzureCredential());
            else dp.ProtectKeysWithAzureKeyVault(keyUri.ToString(), keyResolver);
        }
        return services;
    }
}

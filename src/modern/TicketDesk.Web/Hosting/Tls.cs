using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TicketDesk.Web.Hosting;

/// <summary>Section <c>Tls</c>: HTTPS in Kestrel itself (AKS: the Azure load balancer forwards TCP 443, no ingress controller).</summary>
public sealed class TlsOptions
{
    public const string SectionName = "Tls";

    /// <summary>
    /// PEM file with the certificate (and chain). On AKS the Secrets Store CSI driver writes the Key Vault certificate here
    /// (object type secret: private key + chain in one PEM file) and rewrites it when a new version appears. Empty: no HTTPS.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>PEM file with the private key; empty when it is in <see cref="CertificatePath"/> (the CSI driver's format).</summary>
    public string? KeyPath { get; set; }

    public int HttpsPort { get; set; } = 8443;

    /// <summary>Plain HTTP for the kubelet's probes (inside the cluster only; the load balancer forwards 443 alone).</summary>
    public int HttpPort { get; set; } = 8080;

    /// <summary>How often a TLS handshake may look at the files again (a rotated certificate is served after at most this).</summary>
    public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromSeconds(10);
}

public static class TlsHosting
{
    /// <summary>Listens on <see cref="TlsOptions.HttpsPort"/> with the reloading certificate and on <see cref="TlsOptions.HttpPort"/> when a certificate is configured.</summary>
    public static bool AddTicketDeskTls(this WebApplicationBuilder builder)
    {
        var o = builder.Configuration.GetSection(TlsOptions.SectionName).Get<TlsOptions>() ?? new TlsOptions();
        if (string.IsNullOrWhiteSpace(o.CertificatePath)) return false;
        var certificate = new ReloadingPemCertificate(o.CertificatePath, o.KeyPath, o.ReloadInterval, TimeProvider.System);
        certificate.Load(); // fail at start-up, not at the first handshake, when the files are missing or unreadable
        builder.Services.AddSingleton(certificate);
        builder.WebHost.ConfigureKestrel(k =>
        {
            // explicit endpoints replace ASPNETCORE_HTTP_PORTS, so the HTTP port is listed here too
            k.ListenAnyIP(o.HttpPort);
            k.ListenAnyIP(o.HttpsPort, l => l.UseHttps(h => h.ServerCertificateSelector = (_, _) => certificate.Current));
        });
        return true;
    }
}

/// <summary>
/// The server certificate from PEM files, re-read when their content changes (checked at most every
/// <c>reloadInterval</c>, on a TLS handshake). A file that cannot be read or parsed (half-written during a rotation) keeps the
/// certificate that was loaded before. Kubernetes and the CSI driver replace mounted files by swapping a symlink, so the
/// content hash, not a file watcher, decides.
/// </summary>
public sealed class ReloadingPemCertificate(string certificatePath, string? keyPath, TimeSpan reloadInterval, TimeProvider clock)
{
    private readonly Lock _gate = new();
    private X509Certificate2? _current;
    private byte[] _hash = [];
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;

    /// <summary>Times a new certificate replaced the previous one (0 after the first load).</summary>
    public int Reloads { get; private set; }

    public X509Certificate2 Current
    {
        get
        {
            if (_current != null && clock.GetUtcNow() - _checkedAt < reloadInterval) return _current;
            lock (_gate)
            {
                if (_current == null || clock.GetUtcNow() - _checkedAt >= reloadInterval) TryReload();
                return _current!;
            }
        }
    }

    /// <summary>First load; throws when the files cannot be used.</summary>
    public X509Certificate2 Load()
    {
        lock (_gate)
        {
            if (!TryReload(throwOnError: true)) throw new InvalidOperationException($"No certificate in {certificatePath}.");
            return _current!;
        }
    }

    private bool TryReload(bool throwOnError = false)
    {
        _checkedAt = clock.GetUtcNow();
        try
        {
            var certPem = File.ReadAllText(certificatePath);
            var keyPem = string.IsNullOrWhiteSpace(keyPath) ? certPem : File.ReadAllText(keyPath);
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(certPem + "\n" + keyPem));
            if (_current != null && hash.AsSpan().SequenceEqual(_hash)) return true;
            var loaded = X509Certificate2.CreateFromPem(certPem, keyPem);
            if (!loaded.HasPrivateKey) throw new CryptographicException("the PEM files hold no private key for the certificate");
            if (_current != null) Reloads++;
            _current = loaded;
            _hash = hash;
            return true;
        }
        catch (Exception e) when (!throwOnError && _current != null && e is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            return false; // keep serving the certificate loaded before
        }
    }
}

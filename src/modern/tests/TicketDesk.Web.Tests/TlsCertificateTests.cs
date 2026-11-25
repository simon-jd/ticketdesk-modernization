using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TicketDesk.Web.Hosting;

namespace TicketDesk.Web.Tests;

/// <summary>AKS round: Kestrel's certificate comes from the PEM file the CSI driver rewrites when Key Vault has a new version.</summary>
public sealed class TlsCertificateTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("td-tls-").FullName;
    private readonly ManualClock _clock = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void A_rotated_certificate_is_served_after_the_reload_interval()
    {
        var path = Path.Combine(_dir, "tls.pem");
        var a = WriteCsiStylePem(path, "td-a.example");
        var cert = new ReloadingPemCertificate(path, null, TimeSpan.FromSeconds(10), _clock);
        Assert.Equal(a, cert.Load().Thumbprint);

        var b = WriteCsiStylePem(path, "td-b.example");
        _clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(a, cert.Current.Thumbprint); // not looked at yet
        _clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(b, cert.Current.Thumbprint);
        Assert.True(cert.Current.HasPrivateKey);
        Assert.Equal(1, cert.Reloads);
    }

    [Fact]
    public void A_half_written_file_keeps_the_certificate_loaded_before()
    {
        var path = Path.Combine(_dir, "tls.pem");
        var a = WriteCsiStylePem(path, "td-a.example");
        var cert = new ReloadingPemCertificate(path, null, TimeSpan.Zero, _clock);
        cert.Load();
        File.WriteAllText(path, "-----BEGIN PRIVATE KEY-----\nAAAA");
        Assert.Equal(a, cert.Current.Thumbprint);
        File.Delete(path);
        Assert.Equal(a, cert.Current.Thumbprint);
        Assert.Equal(0, cert.Reloads);
    }

    [Fact]
    public void Separate_certificate_and_key_files_as_in_a_kubernetes_tls_secret()
    {
        var (certPem, keyPem, thumbprint) = NewPem("td-kind.example");
        File.WriteAllText(Path.Combine(_dir, "tls.crt"), certPem);
        File.WriteAllText(Path.Combine(_dir, "tls.key"), keyPem);
        var cert = new ReloadingPemCertificate(Path.Combine(_dir, "tls.crt"), Path.Combine(_dir, "tls.key"), TimeSpan.Zero, _clock);
        Assert.Equal(thumbprint, cert.Load().Thumbprint);
    }

    [Fact]
    public void Start_up_fails_without_a_usable_certificate()
    {
        var cert = new ReloadingPemCertificate(Path.Combine(_dir, "missing.pem"), null, TimeSpan.Zero, _clock);
        Assert.ThrowsAny<Exception>(() => cert.Load());
    }

    /// <summary>The Key Vault provider's PEM for a certificate fetched as a secret: private key first, then the certificate.</summary>
    private static string WriteCsiStylePem(string path, string name)
    {
        var (certPem, keyPem, thumbprint) = NewPem(name);
        File.WriteAllText(path + ".tmp", keyPem + "\n" + certPem);
        File.Move(path + ".tmp", path, overwrite: true);
        return thumbprint;
    }

    private static (string Cert, string Key, string Thumbprint) NewPem(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        return (cert.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem(), cert.Thumbprint);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}

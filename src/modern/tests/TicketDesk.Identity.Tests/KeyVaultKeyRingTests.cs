using System.Security.Cryptography;
using System.Xml.Linq;
using Azure.Core.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TicketDesk.Identity.Tests;

/// <summary>
/// DataProtection:KeyVaultKeyId (AKS round): the key ring is wrapped with a Key Vault key. Key Vault itself is replaced by a
/// local RSA key behind the same resolver interface the Azure package calls (wrap = RSA-OAEP, as Key Vault does).
/// </summary>
public sealed class KeyVaultKeyRingTests
{
    private const string KeyId = "https://kv-tdlab-test.vault.azure.net/keys/td-dataprotection/0123456789abcdef0123456789abcdef";

    [Fact]
    public void Off_by_default_keys_are_stored_unwrapped()
    {
        using var sp = Build(new Dictionary<string, string?>(), new MemoryRepository(), resolver: null);
        Assert.Null(sp.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor);
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("http://kv.vault.azure.net/keys/td-dataprotection")]
    [InlineData("https://kv.vault.azure.net/secrets/td-dataprotection")]
    public void A_key_id_that_is_not_a_Key_Vault_key_URI_is_refused(string keyId)
    {
        var e = Assert.Throws<InvalidOperationException>(() =>
            Build(new Dictionary<string, string?> { [SharedSignIn.KeyVaultKeySetting] = keyId }, new MemoryRepository(), resolver: null));
        Assert.Contains(SharedSignIn.KeyVaultKeySetting, e.Message);
    }

    [Fact]
    public void A_Key_Vault_key_URI_turns_on_the_Azure_Key_Vault_encryptor()
    {
        using var sp = Build(Settings(), new MemoryRepository(), resolver: null);
        var encryptor = sp.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor;
        Assert.Equal("Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlEncryptor", encryptor?.GetType().FullName);
    }

    [Fact]
    public void Keys_are_stored_wrapped_and_another_instance_with_the_key_reads_them()
    {
        var repo = new MemoryRepository();
        var vault = new LocalKeyResolver();
        string protectedText;
        using (var first = Build(Settings(), repo, vault))
            protectedText = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("ticket 42");

        var stored = Assert.Single(repo.Elements);
        var encrypted = stored.Descendants("encryptedKey").Single();
        Assert.Equal(KeyId, encrypted.Element("kid")?.Value);
        Assert.DoesNotContain("masterKey", stored.ToString(), StringComparison.Ordinal); // the plain key material is not stored
        // the legacy app (net48, Azure.Extensions.AspNetCore.DataProtection.Keys 1.6.1 in packages.config) loads the decryptor
        // type by this name, version included: both apps must ship the same package version
        var decryptor = stored.Descendants().Single(e => e.Name.LocalName == "encryptedSecret").Attribute("decryptorType")?.Value;
        Assert.Equal("Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptor, Azure.Extensions.AspNetCore.DataProtection.Keys, " +
                     "Version=1.6.1.0, Culture=neutral, PublicKeyToken=92742159e12e44c8", decryptor);
        Assert.True(vault.Wraps >= 1);

        // a second pod (or the legacy app) with access to the same Key Vault key
        using (var second = Build(Settings(), repo, vault))
            Assert.Equal("ticket 42", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(protectedText));
        Assert.True(vault.Unwraps >= 1);

        // without the key the ring cannot be read
        using var without = Build(new Dictionary<string, string?>(), repo, resolver: null);
        Assert.ThrowsAny<Exception>(() => without.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(protectedText));
    }

    private static Dictionary<string, string?> Settings() => new() { [SharedSignIn.KeyVaultKeySetting] = KeyId };

    private static ServiceProvider Build(Dictionary<string, string?> settings, IXmlRepository repo, IKeyEncryptionKeyResolver? resolver)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddTicketDeskDataProtection(configuration, resolver);
        services.Configure<KeyManagementOptions>(o => o.XmlRepository = repo);
        return services.BuildServiceProvider();
    }

    private sealed class MemoryRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];
        public IReadOnlyList<XElement> Elements => _elements;
        public IReadOnlyCollection<XElement> GetAllElements() => _elements.Select(e => new XElement(e)).ToList();
        public void StoreElement(XElement element, string friendlyName) => _elements.Add(new XElement(element));
    }

    private sealed class LocalKeyResolver : IKeyEncryptionKeyResolver, IKeyEncryptionKey
    {
        private readonly RSA _rsa = RSA.Create(2048);
        public int Wraps { get; private set; }
        public int Unwraps { get; private set; }
        public string KeyId => KeyVaultKeyRingTests.KeyId;

        public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default) =>
            keyId == KeyVaultKeyRingTests.KeyId || keyId.StartsWith("https://kv-tdlab-test.vault.azure.net/keys/td-dataprotection", StringComparison.Ordinal)
                ? this : throw new InvalidOperationException($"unknown key {keyId}");

        public Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default) => Task.FromResult(Resolve(keyId, cancellationToken));

        public byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
        {
            Assert.Equal("RSA-OAEP", algorithm);
            Wraps++;
            return _rsa.Encrypt(key.Span, RSAEncryptionPadding.OaepSHA1);
        }

        public Task<byte[]> WrapKeyAsync(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default) =>
            Task.FromResult(WrapKey(algorithm, key, cancellationToken));

        public byte[] UnwrapKey(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
        {
            Unwraps++;
            return _rsa.Decrypt(encryptedKey.Span, RSAEncryptionPadding.OaepSHA1);
        }

        public Task<byte[]> UnwrapKeyAsync(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(UnwrapKey(algorithm, encryptedKey, cancellationToken));
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;
using TicketDesk.Seed;

namespace TicketDesk.Identity.Tests;

/// <summary>
/// Password hashes after the legacy app is retired, without a database: the host hashes in the Identity V3 format, and legacy
/// Identity V2 hashes (the ASP.NET Identity 2 algorithm, reimplemented in tools/Seed) still verify, with a request to rehash.
/// (Until Round 4 the host stayed in V2 compatibility mode so the legacy app could verify every hash.)
/// </summary>
public sealed class PasswordCompatibilityTests
{
    private const string Password = "legacy-pass-1";
    private static readonly byte[] Salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    private static readonly TicketDeskUser User = new() { UserName = "someone@example.test" };

    private static readonly ServiceProvider Host = Setup.Services();

    /// <summary>The hasher exactly as the web host resolves it (scoped service).</summary>
    private static IPasswordHasher<TicketDeskUser> HasherFromHost() =>
        Host.CreateScope().ServiceProvider.GetRequiredService<IPasswordHasher<TicketDeskUser>>();

    [Fact]
    public void Host_configures_IdentityV3_with_the_framework_iteration_count()
    {
        using var sp = Setup.Services();
        var options = sp.GetRequiredService<IOptions<PasswordHasherOptions>>().Value;
        Assert.Equal(PasswordHasherCompatibilityMode.IdentityV3, options.CompatibilityMode);
        Assert.Equal(100_000, options.IterationCount);
    }

    [Fact]
    public void Legacy_v2_hash_verifies_and_asks_for_a_rehash()
    {
        var legacyHash = IdentityV2PasswordHasher.Hash(Password, Salt);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, HasherFromHost().VerifyHashedPassword(User, legacyHash, Password));
        Assert.Equal(PasswordVerificationResult.Failed, HasherFromHost().VerifyHashedPassword(User, legacyHash, Password + "x"));
    }

    [Fact]
    public void New_hashes_are_v3_with_sha512_and_the_legacy_algorithm_cannot_verify_them()
    {
        var hash = HasherFromHost().HashPassword(User, Password);
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(61, bytes.Length); // 0x01 | PRF | iterations | salt length | 16-byte salt | 32-byte subkey
        Assert.Equal(1, bytes[0]);
        Assert.Equal(2, bytes[4]); // KeyDerivationPrf.HMACSHA512 (big-endian UInt32, low byte)
        Assert.Equal(100_000, (bytes[5] << 24) | (bytes[6] << 16) | (bytes[7] << 8) | bytes[8]);
        Assert.Equal(PasswordVerificationResult.Success, HasherFromHost().VerifyHashedPassword(User, hash, Password));
        Assert.False(IdentityV2PasswordHasher.Verify(hash, Password)); // why a return to the legacy app would need password resets
    }

    [Fact]
    public void A_v2_compatibility_hasher_still_verifies_v3_hashes()
    {
        // rollback to an image from before Round 4 (V2 compatibility mode) keeps working for users whose hash is already V3
        var v2Mode = new PasswordHasher<TicketDeskUser>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        Assert.Equal(PasswordVerificationResult.Success, v2Mode.VerifyHashedPassword(User, HasherFromHost().HashPassword(User, Password), Password));
    }
}

using System.Security.Cryptography;
using System.Text;

namespace TicketDesk.Seed;

/// <summary>Scale presets: tickets drive everything else proportionally.</summary>
public sealed record ScalePreset(string Name, int Tickets, int Users, int Projects)
{
    public static readonly ScalePreset XS = new("XS", 200, 24, 3);
    public static readonly ScalePreset S = new("S", 10_000, 250, 5);
    public static readonly ScalePreset M = new("M", 100_000, 2_500, 10);
    public static readonly ScalePreset L = new("L", 1_000_000, 25_000, 25);

    public static ScalePreset Parse(string name) => name.ToUpperInvariant() switch
    {
        "XS" => XS, "S" => S, "M" => M, "L" => L,
        _ => throw new ArgumentException($"unknown scale '{name}' (XS, S, M, L)"),
    };
}

/// <summary>
/// SplitMix64: tiny, fast and — unlike System.Random — specified, so the data set is identical on every runtime.
/// Each ticket gets its own stream (seed mixed with the ticket id), so generation order never changes the output.
/// </summary>
public sealed class Prng(ulong seed)
{
    private ulong _state = seed;

    public static Prng For(long seed, string stream, long index = 0)
    {
        ulong h = 1469598103934665603UL; // FNV-1a over the stream name
        foreach (var c in stream) { h ^= c; h *= 1099511628211UL; }
        return new Prng(Mix((ulong)seed ^ h ^ Mix((ulong)index + 0x9E3779B97F4A7C15UL)));
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public ulong NextULong() => Mix(_state += 0x9E3779B97F4A7C15UL);

    /// <summary>Uniform in [0, max).</summary>
    public int Next(int max) => max <= 0 ? 0 : (int)(NextULong() % (ulong)max);

    public int Next(int min, int max) => min + Next(max - min);

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>Zipf-like pick: low indexes are much more frequent (s ≈ 1).</summary>
    public int Zipf(int n)
    {
        var u = NextDouble();
        var x = Math.Exp(u * Math.Log(n + 1)) - 1; // inverse CDF of a continuous 1/x density on [1, n+1)
        return Math.Clamp((int)x, 0, n - 1);
    }

    /// <summary>Exponential with the given mean.</summary>
    public double Exponential(double mean) => -mean * Math.Log(1 - NextDouble());

    public Guid NextGuid()
    {
        Span<byte> b = stackalloc byte[16];
        BitConverter.TryWriteBytes(b, NextULong());
        BitConverter.TryWriteBytes(b[8..], NextULong());
        b[7] = (byte)((b[7] & 0x0F) | 0x40); // version 4 layout
        b[8] = (byte)((b[8] & 0x3F) | 0x80);
        return new Guid(b);
    }
}

/// <summary>
/// ASP.NET Identity 2.x password hash (Microsoft.AspNet.Identity.Crypto.HashPassword): 0x00 marker, 16-byte salt,
/// 32-byte PBKDF2-HMAC-SHA1 subkey with 1000 iterations, base64. ASP.NET Core Identity verifies this format
/// ("V2") and reports SuccessRehashNeeded.
/// </summary>
public static class IdentityV2PasswordHasher
{
    public const int Iterations = 1000;

    public static string Hash(string password, ReadOnlySpan<byte> salt16)
    {
        if (salt16.Length != 16) throw new ArgumentException("salt must be 16 bytes", nameof(salt16));
        var subkey = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt16, Iterations, HashAlgorithmName.SHA1, 32);
        var output = new byte[49];
        salt16.CopyTo(output.AsSpan(1));
        subkey.CopyTo(output.AsSpan(17));
        return Convert.ToBase64String(output);
    }

    public static bool Verify(string hash, string password)
    {
        var bytes = Convert.FromBase64String(hash);
        if (bytes.Length != 49 || bytes[0] != 0) return false;
        var subkey = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), bytes.AsSpan(1, 16), Iterations, HashAlgorithmName.SHA1, 32);
        return CryptographicOperations.FixedTimeEquals(subkey, bytes.AsSpan(17));
    }
}

/// <summary>Word lists for plausible help-desk data (all invented; no real people).</summary>
internal static class Vocabulary
{
    public static readonly string[] FirstNames =
    [
        "Ava", "Liam", "Noah", "Emma", "Olivia", "Mia", "Lucas", "Ethan", "Sofia", "Amelia", "Mateo", "Aria", "Leo", "Zoe", "Ivy",
        "Hana", "Ken", "Ravi", "Priya", "Chen", "Wei", "Yuki", "Omar", "Layla", "Nina", "Felix", "Jonas", "Clara", "Marta", "Diego",
        "Elena", "Tomas", "Ines", "Kofi", "Amara", "Sean", "Aoife", "Lars", "Freya", "Anika", "Rohan", "Mei", "Jin", "Sara", "Ali",
    ];

    public static readonly string[] LastNames =
    [
        "Smith", "Garcia", "Chen", "Patel", "Kim", "Nguyen", "Muller", "Rossi", "Silva", "Kowalski", "Okafor", "Haddad", "Larsen",
        "Novak", "Tanaka", "Sato", "Cohen", "Murphy", "Dubois", "Schmidt", "Fischer", "Ivanova", "Lopez", "Martin", "Brown", "Wilson",
        "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris", "Clark", "Lewis", "Walker", "Young", "King", "Wright",
    ];

    public static readonly string[] Projects =
    [
        "IT Help Desk", "Facilities", "HR Services", "Finance Systems", "Field Engineering", "Customer Portal", "Data Platform",
        "Security Operations", "Network Ops", "ERP Rollout", "Mobile Apps", "Warehouse Systems", "Legal Requests", "Procurement",
        "Marketing Web", "Payroll", "Training", "Telephony", "Print Services", "Identity & Access", "Analytics", "Integration Hub",
        "Release Management", "Asset Management", "Vendor Support",
    ];

    public static readonly string[] Things =
    [
        "printer", "laptop", "docking station", "monitor", "VPN client", "Outlook profile", "Teams meeting room", "badge reader",
        "conference phone", "Wi-Fi", "shared drive", "SharePoint site", "ERP login", "payroll report", "expense app", "CRM export",
        "backup job", "SSL certificate", "build server", "test database", "keyboard", "headset", "scanner", "mobile device",
        "password reset tool", "file share quota", "license server", "timesheet app", "intranet page", "label printer",
    ];

    public static readonly string[] Problems =
    [
        "is offline", "keeps disconnecting", "shows an error", "is very slow", "cannot be reached", "needs replacing", "fails to start",
        "rejects my password", "lost its settings", "prints blank pages", "is missing", "crashes on launch", "needs access for a new hire",
        "times out", "shows the wrong data", "stopped syncing", "needs an upgrade", "is making noise", "is locked", "requires approval",
    ];

    public static readonly string[] Places =
    [
        "on the 3rd floor", "in building B", "in the Boston office", "at the front desk", "in meeting room 4", "for the sales team",
        "in the warehouse", "since this morning", "after the update", "for remote staff", "in the lab", "for the finance team",
    ];

    public static readonly string[] Sentences =
    [
        "It started after the latest update.", "Restarting did not help.", "Several people on my team see the same thing.",
        "This blocks month-end work.", "I attached a screenshot of the message.", "It works from home but not in the office.",
        "The error code is 0x80004005.", "Please advise on a workaround.", "We tried a different cable already.",
        "The issue comes and goes during the day.", "Our manager approved the request.", "No urgency, whenever you have time.",
        "It worked fine last week.", "The vendor says it is on our side.", "We need this before Friday.",
    ];

    public static readonly string[] Replies =
    [
        "Thanks, looking into it.", "Can you confirm the asset tag?", "I pushed a fix, please try again.", "Replaced the unit.",
        "Escalated to the network team.", "Works for me now, can you check?", "Please restart and let me know.", "Ordered a new part.",
        "Account unlocked.", "Waiting on the vendor.", "Still happening here.", "Confirmed fixed, thank you!", "Added you to the group.",
    ];

    public static readonly string[] Tags = BuildTags();

    private static string[] BuildTags()
    {
        string[] baseTags =
        [
            "printer", "network", "vpn", "email", "outlook", "laptop", "hardware", "software", "access", "password", "onboarding",
            "offboarding", "license", "wifi", "phone", "erp", "crm", "payroll", "backup", "security", "certificate", "database", "build",
            "monitor", "keyboard", "badge", "sharepoint", "teams", "mobile", "storage", "performance", "outage", "upgrade", "request",
            "facilities", "hr", "finance", "urgent", "vendor", "training",
        ];
        var extra = Enumerable.Range(1, 110).Select(i => $"{baseTags[i % baseTags.Length]}-{i:D3}");
        return [.. baseTags, .. extra];
    }
}

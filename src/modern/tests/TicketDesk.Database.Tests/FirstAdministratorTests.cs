using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TicketDesk.Testing;

namespace TicketDesk.Database.Tests;

/// <summary>
/// td-db create-admin: the first administrator of an empty installation (the legacy first-run setup's step, retired, X1). It
/// creates exactly one account with the app's V3 hash, refuses while an administrator or an account with that e-mail exists,
/// and takes the password only from standard input or a file, never printing it.
/// </summary>
public sealed class FirstAdministratorTests
{
    private const string Email = "first.admin@example.test";
    private const string Name = "First Admin";
    private const string Password = "Stdin-Secret-4711";

    private static Func<string, string?> Env(string? connection) => n => n == "TD_DB_CONN" ? connection : null;

    private static async Task<(int Code, string Out, string Err)> Run(string? connection, TextReader? stdin, params string[] args)
    {
        var (o, e) = (new StringWriter(), new StringWriter());
        var code = await Cli.RunAsync(["create-admin", .. args], o, e, Env(connection), stdin);
        return (code, o.ToString(), e.ToString());
    }

    private static Task<int> Users(ScratchDatabase db) => db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.IdentityUsers");

    private static Task<int> Admins(ScratchDatabase db) => db.ScalarAsync<int>(
        "SELECT COUNT(*) FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE r.Name = N'TdAdministrators'");

    [Fact]
    public async Task Creates_the_first_administrator_of_an_empty_install_once()
    {
        await using var db = await TempDatabase.CreateAsync<EmptyInstallDatabase>();
        Assert.Equal(0, await Users(db));

        var (code, stdout, stderr) = await Run(db.ConnectionString, new StringReader(Password + "\n"), "--email", Email, "--display-name", Name, "--password-stdin");
        Assert.True(code == 0, stderr);
        Assert.Contains($"created administrator {Email}", stdout);
        Assert.Contains("e-mail destination added", stdout); // td-seed schema creates notifications.* (Round 5b)
        Assert.DoesNotContain(Password, stdout + stderr);

        var rows = await db.RowsAsync("""
            SELECT u.UserName, u.Email, u.DisplayName, u.EmailConfirmed, u.LockoutEnabled, u.AccessFailedCount, u.PasswordHash, u.SecurityStamp, r.Name
            FROM dbo.IdentityUsers u JOIN dbo.IdentityUserRoles ur ON ur.UserId = u.Id JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId
            """);
        var row = Assert.Single(rows);
        Assert.Equal([Email, Email, Name, false, true, 0], row[..6]);
        Assert.Equal("TdAdministrators", row[8]);
        Assert.False(string.IsNullOrEmpty((string?)row[7]));
        // the app's format: Identity V3 (marker 1), HMAC-SHA512 (2), 100,000 iterations
        var hash = (string)row[6]!;
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal((byte)1, bytes[0]);
        Assert.Equal((byte)2, bytes[4]);
        Assert.Equal(100_000, (bytes[5] << 24) | (bytes[6] << 16) | (bytes[7] << 8) | bytes[8]);
        var v3 = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3 }));
        Assert.Equal(PasswordVerificationResult.Success, v3.VerifyHashedPassword(new object(), hash, Password));
        // the e-mail destination as the app's new-user path writes it (IUserAccounts.AddEmailDestinationAsync)
        var destination = Assert.Single(await db.RowsAsync("""
            SELECT s.IsEnabled, d.DestinationType, d.DestinationAddress, d.SubscriberName
            FROM notifications.SubscriberPushNotificationSettings s JOIN notifications.PushNotificationDestinations d ON d.SubscriberId = s.SubscriberId
            JOIN dbo.IdentityUsers u ON u.Id = s.SubscriberId WHERE u.Email = @e
            """, ("@e", Email)));
        Assert.Equal([true, "email", Email, Name], destination);

        // a second run: refused, nothing added
        var again = await Run(db.ConnectionString, new StringReader("Another-Secret-1\n"), "--email", "second@example.test", "--display-name", "Second", "--password-stdin");
        Assert.Equal(1, again.Code);
        Assert.Contains("refused: the database has 1 administrator(s) already", again.Err);
        Assert.Equal(1, await Users(db));
    }

    [Fact]
    public async Task Refuses_on_a_database_that_has_administrators()
    {
        await using var db = await TempDatabase.CreateAsync<ScratchDatabase>(); // the XS seed has one
        var (users, admins) = (await Users(db), await Admins(db));
        Assert.True(admins > 0);
        var (code, _, stderr) = await Run(db.ConnectionString, new StringReader(Password), "--email", Email, "--display-name", Name, "--password-stdin");
        Assert.Equal(1, code);
        Assert.Contains("refused", stderr);
        Assert.Equal((users, admins), (await Users(db), await Admins(db)));
    }

    [Fact]
    public async Task Never_resets_an_existing_account_with_that_email()
    {
        await using var db = await TempDatabase.CreateAsync<EmptyInstallDatabase>();
        await db.ScalarAsync<int>("""
            INSERT dbo.IdentityUsers (Id, DisplayName, Email, EmailConfirmed, PasswordHash, SecurityStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, UserName)
            VALUES (N'u-existing', N'Someone', @e, 0, N'old-hash', N'stamp', 0, 0, 1, 0, @e); SELECT 0;
            """, ("@e", Email));
        var (code, _, stderr) = await Run(db.ConnectionString, new StringReader(Password), "--email", Email, "--display-name", Name, "--password-stdin");
        Assert.Equal(1, code);
        Assert.Contains("an account with this e-mail exists", stderr);
        Assert.Equal("old-hash", await db.ScalarAsync<string>("SELECT PasswordHash FROM dbo.IdentityUsers WHERE Id = N'u-existing'"));
        Assert.Equal(0, await Admins(db));
    }

    [Fact]
    public async Task Reads_the_password_from_a_file()
    {
        await using var db = await TempDatabase.CreateAsync<EmptyInstallDatabase>();
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "File-Secret-0815\n", TestContext.Current.CancellationToken);
            var (code, stdout, stderr) = await Run(db.ConnectionString, null, "--email", Email, "--display-name", Name, "--password-file", file);
            Assert.True(code == 0, stderr);
            Assert.DoesNotContain("File-Secret-0815", stdout + stderr);
            var hash = await db.ScalarAsync<string>("SELECT PasswordHash FROM dbo.IdentityUsers WHERE Email = @e", ("@e", Email));
            var v3 = new PasswordHasher<object>(Options.Create(FirstAdministrator.HasherOptions));
            Assert.Equal(PasswordVerificationResult.Success, v3.VerifyHashedPassword(new object(), hash, "File-Secret-0815"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Takes_no_password_argument_checks_its_input_and_never_echoes_the_password()
    {
        await using var db = await TempDatabase.CreateAsync<EmptyInstallDatabase>();
        var cases = new (TextReader? Stdin, string[] Args, string Expect)[]
        {
            (null, ["--email", Email, "--display-name", Name, "--password", "Argv-Secret-1"], "never an argument"),
            (null, ["--email", Email, "--display-name", Name], "usage"),
            (new StringReader(Password), ["--email", Email, "--display-name", Name, "--password-stdin", "--password-file", "/nonexistent"], "usage"),
            (new StringReader("Argv-Secret-1"), ["--email", Email, "--display-name", Name, "Argv-Secret-1", "--password-stdin"], "unexpected argument"),
            (new StringReader("short"), ["--email", Email, "--display-name", Name, "--password-stdin"], "6 to 100 characters"),
            (new StringReader(Password), ["--email", "not-an-address", "--display-name", Name, "--password-stdin"], "--email"),
            (new StringReader(Password), ["--email", Email, "--display-name", " ", "--password-stdin"], "--display-name"),
        };
        foreach (var (stdin, args, expect) in cases)
        {
            var (code, stdout, stderr) = await Run(db.ConnectionString, stdin, args);
            Assert.True(code == 2, $"{string.Join(' ', args)}: exit {code}");
            Assert.Contains(expect, stderr);
            Assert.DoesNotContain("Argv-Secret-1", stdout + stderr);
            Assert.DoesNotContain("short", stdout + stderr);
        }
        Assert.Equal(0, await Users(db));
    }

    [Fact]
    public async Task Help_prints_the_usage_and_exits_0()
    {
        var (code, stdout, _) = await Run(null, null, "--help");
        Assert.Equal(0, code);
        Assert.Contains("--password-stdin | --password-file FILE", stdout);
        var (o, e) = (new StringWriter(), new StringWriter());
        Assert.Equal(0, await Cli.RunAsync(["--help"], o, e, _ => null));
        Assert.Contains("create-admin", o.ToString());
    }

    [Fact]
    public async Task Two_runs_at_once_create_one_administrator()
    {
        await using var db = await TempDatabase.CreateAsync<EmptyInstallDatabase>();
        var runs = await Task.WhenAll(Enumerable.Range(1, 2).Select(i =>
            Run(db.ConnectionString, new StringReader(Password), "--email", $"admin{i}@example.test", "--display-name", $"Admin {i}", "--password-stdin")));
        Assert.Single(runs, r => r.Code == 0);
        Assert.Equal(1, await Admins(db));
        Assert.Equal(1, await Users(db));
    }
}

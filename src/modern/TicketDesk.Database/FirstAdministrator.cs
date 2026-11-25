using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace TicketDesk.Database;

/// <summary>What <see cref="FirstAdministrator.CreateAsync"/> did: the new account, and whether its e-mail destination was added.</summary>
public sealed record FirstAdministratorResult(string UserId, bool EmailDestination);

/// <summary>Why <see cref="FirstAdministrator.CreateAsync"/> created nothing (exit 1 of <c>td-db create-admin</c>).</summary>
public sealed class FirstAdministratorRefusedException(string message) : Exception(message);

/// <summary>
/// <c>td-db create-admin</c>: the first administrator of an installation whose database has none (an empty install:
/// <c>td-seed schema --create-database</c>, <c>td-db migrate --contract --legacy-retired</c>). It replaces the legacy first-run
/// setup's administrator step (retired, X1) without its hole (S1: that anonymous POST reset the password of any existing user
/// name and made it an administrator): it refuses while any administrator exists and never touches an existing account; after
/// it, administrators manage accounts in the app (Admin, Users).
/// The account is the one the legacy wizard and the app's new-user form create: user name = e-mail, the display name, the
/// TdAdministrators role (the role hierarchy adds help desk and internal user), lockout enabled, and an e-mail destination for
/// notifications where the notification tables exist. The password is hashed by the app's hasher: ASP.NET Core Identity's
/// <see cref="PasswordHasher{TUser}"/> in Identity V3 mode with the framework's iteration count, which is what
/// <c>AddTicketDeskIdentity</c> configures (PBKDF2-HMAC-SHA512, 100,000 iterations), so the first sign-in needs no rehash.
/// </summary>
public static class FirstAdministrator
{
    public const string Role = "TdAdministrators";

    /// <summary>The same rules as the app's new-user form (UserController.RegisterFields) and the table's column sizes.</summary>
    public const int MinPasswordLength = 6, MaxPasswordLength = 100, MaxDisplayNameLength = 100, MaxEmailLength = 256;

    /// <summary>The options <c>AddTicketDeskIdentity</c> sets on the app's hasher (the iteration count stays the framework's).</summary>
    public static PasswordHasherOptions HasherOptions => new() { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3 };

    /// <summary>The hasher's user type (the hasher does not read it).</summary>
    public sealed class Account;

    public static string HashPassword(string password) =>
        new PasswordHasher<Account>(Options.Create(HasherOptions)).HashPassword(new Account(), password);

    /// <summary>Checks the input; the message never contains the password.</summary>
    public static void Validate(string? email, string? displayName, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ArgumentException($"--email must be one e-mail address (at most {MaxEmailLength} characters)");
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim() != displayName || displayName.Length > MaxDisplayNameLength)
            throw new ArgumentException($"--display-name must be 1 to {MaxDisplayNameLength} characters without leading or trailing spaces");
        if (password is null || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            throw new ArgumentException($"the password must be {MinPasswordLength} to {MaxPasswordLength} characters");
    }

    /// <summary>
    /// Creates the account in one serializable transaction (two runs at once cannot both create one). Throws
    /// <see cref="FirstAdministratorRefusedException"/> when an administrator or an account with that e-mail exists, and
    /// <see cref="InvalidOperationException"/> when the schema is not applied (no TdAdministrators role).
    /// </summary>
    public static async Task<FirstAdministratorResult> CreateAsync(string connectionString, string email, string displayName, string password,
        CancellationToken ct = default)
    {
        Validate(email, displayName, password);
        var hash = HashPassword(password);
        var id = Guid.NewGuid().ToString();
        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        async Task<object?> Scalar(string sql, params (string Name, object Value)[] p)
        {
            await using var cmd = new SqlCommand(sql, c, tx);
            foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v);
            var r = await cmd.ExecuteScalarAsync(ct);
            return r is DBNull ? null : r;
        }

        if (await Scalar("SELECT OBJECT_ID(N'dbo.IdentityUsers')") == null || await Scalar("SELECT Id FROM dbo.IdentityRoles WHERE Name = @r", ("@r", Role)) is not string roleId)
            throw new InvalidOperationException($"no {Role} role in dbo.IdentityRoles: apply the schema first (td-seed schema --create-database, td-db migrate --contract --legacy-retired)");
        // UPDLOCK: a second run waits here for the first one's commit, then sees its administrator
        var admins = (int)(await Scalar("SELECT COUNT(*) FROM dbo.IdentityUserRoles WITH (UPDLOCK) WHERE RoleId = @r", ("@r", roleId)))!;
        if (admins > 0)
            throw new FirstAdministratorRefusedException($"refused: the database has {admins} administrator(s) already; they manage accounts in the app (Admin, Users)");
        if ((int)(await Scalar("SELECT COUNT(*) FROM dbo.IdentityUsers WITH (UPDLOCK) WHERE UserName = @e OR Email = @e", ("@e", email)))! > 0)
            throw new FirstAdministratorRefusedException("refused: an account with this e-mail exists; td-db create-admin never changes an existing account");

        await Scalar("""
            INSERT dbo.IdentityUsers (Id, DisplayName, Email, EmailConfirmed, PasswordHash, SecurityStamp, PhoneNumber, PhoneNumberConfirmed,
                TwoFactorEnabled, LockoutEndDateUtc, LockoutEnabled, AccessFailedCount, UserName)
            VALUES (@id, @name, @email, 0, @hash, @stamp, NULL, 0, 0, NULL, 1, 0, @email);
            INSERT dbo.IdentityUserRoles (UserId, RoleId) VALUES (@id, @role);
            """, ("@id", id), ("@name", displayName), ("@email", email), ("@hash", hash), ("@stamp", Guid.NewGuid().ToString()), ("@role", roleId));
        // the legacy wizard's AddSettingsForSubscriber, the same rows as the app's IUserAccounts.AddEmailDestinationAsync; td-seed
        // schema creates the notifications.* tables since Round 5b, a database made otherwise without them gets none
        var destination = await Scalar("SELECT OBJECT_ID(N'notifications.PushNotificationDestinations')") != null
                          && await Scalar("SELECT OBJECT_ID(N'notifications.SubscriberPushNotificationSettings')") != null;
        if (destination)
            await Scalar("""
                INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) VALUES (@id, 1);
                INSERT notifications.PushNotificationDestinations (SubscriberId, DestinationType, DestinationAddress, SubscriberName)
                VALUES (@id, N'email', @email, @name);
                """, ("@id", id), ("@email", email), ("@name", displayName));
        await tx.CommitAsync(ct);
        return new FirstAdministratorResult(id, destination);
    }

    /// <summary>
    /// The password from standard input (one line; the newline that ends it is not part of it) or from a file (its first line):
    /// never from the command line, which other users and the shell history can read.
    /// </summary>
    public static async Task<string> ReadPasswordAsync(TextReader? stdin, string? file)
    {
        string? text = file != null ? await File.ReadAllTextAsync(file) : stdin != null ? await stdin.ReadToEndAsync() : null;
        if (text == null) throw new ArgumentException("give the password with --password-stdin or --password-file FILE");
        var end = text.IndexOfAny(['\r', '\n']);
        return end >= 0 ? text[..end] : text;
    }
}

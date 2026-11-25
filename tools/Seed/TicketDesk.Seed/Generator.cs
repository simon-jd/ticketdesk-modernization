using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace TicketDesk.Seed;

public sealed class SeedSettings
{
    public required ScalePreset Scale { get; init; }
    public long Seed { get; init; } = 42;
    /// <summary>Password every synthetic user gets (stored as an Identity v2 hash).</summary>
    public required string UserPassword { get; init; }
    /// <summary>"Now" of the data set: all timestamps lie in the <see cref="HistoryDays"/> before it.</summary>
    public DateTimeOffset BaseDate { get; init; } = new(2026, 10, 31, 17, 0, 0, TimeSpan.Zero);
    public int HistoryDays { get; init; } = 730;
    public string EmailDomain { get; init; } = "seed.ticketdesk.test";
    public IReadOnlyList<string> TicketTypes { get; init; } = ["Problem", "Question", "Request"];
    public IReadOnlyList<string> Categories { get; init; } = ["Software", "Hardware", "Network"];
    public IReadOnlyList<string> Priorities { get; init; } = ["High", "Medium", "Low"];
    /// <summary>Role name → IdentityRoles.Id of the target database.</summary>
    public required IReadOnlyDictionary<string, string> RoleIds { get; init; }
    /// <summary>Id of the existing "Default" project; seeded projects get the following ids.</summary>
    public int FirstProjectId { get; init; } = 1;
    public int ChunkSize { get; init; } = 10_000;
}

public sealed record SeedUser(string Id, string UserName, string Email, string DisplayName, string Role);

/// <summary>One batch of tickets with all their child rows, ready for SqlBulkCopy (parents first).</summary>
public sealed class SeedChunk
{
    public required DataTable Tickets { get; init; }
    public required DataTable Tags { get; init; }
    public required DataTable Subscribers { get; init; }
    public required DataTable Events { get; init; }
    public required DataTable Notifications { get; init; }

    public IEnumerable<(TableDef Def, DataTable Table)> InLoadOrder()
    {
        yield return (TicketDeskSchema.Tickets, Tickets);
        yield return (TicketDeskSchema.TicketTags, Tags);
        yield return (TicketDeskSchema.TicketSubscribers, Subscribers);
        yield return (TicketDeskSchema.TicketEvents, Events);
        yield return (TicketDeskSchema.TicketEventNotifications, Notifications);
    }
}

/// <summary>
/// Deterministic generator of TicketDesk data. Same settings ⇒ byte-identical rows. Tickets follow the domain
/// rules of TicketActionManager (who may assign/resolve/close, status transitions, event descriptions, subscribers
/// and per-subscriber event notifications) so the legacy app renders and acts on them normally.
/// </summary>
public sealed class Generator(SeedSettings settings)
{
    private IReadOnlyList<SeedUser>? _users;
    private SeedUser[]? _owners;
    private SeedUser[]? _staff;

    public SeedSettings Settings => settings;

    public IReadOnlyList<SeedUser> Users => _users ??= BuildUsers();

    public IReadOnlyList<int> ProjectIds =>
        Enumerable.Range(settings.FirstProjectId, settings.Scale.Projects).ToList();

    private IReadOnlyList<SeedUser> BuildUsers()
    {
        var rng = Prng.For(settings.Seed, "users");
        var n = settings.Scale.Users;
        var admins = Math.Max(1, n / 50);
        var staff = Math.Max(2, n * 15 / 100);
        var pending = n / 100;
        var list = new List<SeedUser>(n);
        for (int i = 0; i < n; i++)
        {
            var first = rng.Pick(Vocabulary.FirstNames);
            var last = rng.Pick(Vocabulary.LastNames);
            var role = i < admins ? "TdAdministrators" : i < admins + staff ? "TdHelpDeskUsers" : i >= n - pending ? "TdPendingUsers" : "TdInternalUsers";
            var email = $"{first}.{last}.{i + 1:D5}@{settings.EmailDomain}".ToLowerInvariant();
            list.Add(new SeedUser(rng.NextGuid().ToString(), email, email, $"{first} {last}", role));
        }
        return list;
    }

    private SeedUser[] Owners => _owners ??= Users.Where(u => u.Role != "TdPendingUsers").ToArray();
    private SeedUser[] Staff => _staff ??= Users.Where(u => u.Role is "TdHelpDeskUsers" or "TdAdministrators").ToArray();

    public DataTable ProjectsTable()
    {
        var t = TicketDeskSchema.Projects.NewDataTable();
        var rng = Prng.For(settings.Seed, "projects");
        for (int i = 1; i < settings.Scale.Projects; i++) // index 0 is the existing Default project
        {
            var name = Vocabulary.Projects[(i - 1) % Vocabulary.Projects.Length];
            if (i > Vocabulary.Projects.Length) name += $" {i / Vocabulary.Projects.Length + 1}";
            t.Rows.Add(settings.FirstProjectId + i, name, $"{name} requests ({rng.Pick(Vocabulary.Places)})");
        }
        return t;
    }

    public DataTable UsersTable()
    {
        var t = TicketDeskSchema.IdentityUsers.NewDataTable();
        var users = Users;
        var hashes = new string[users.Count];
        var stamps = new string[users.Count];
        var salts = new byte[users.Count][];
        var rng = Prng.For(settings.Seed, "user-secrets");
        for (int i = 0; i < users.Count; i++)
        {
            salts[i] = rng.NextGuid().ToByteArray();
            stamps[i] = rng.NextGuid().ToString();
        }
        Parallel.For(0, users.Count, i => hashes[i] = IdentityV2PasswordHasher.Hash(settings.UserPassword, salts[i]));
        for (int i = 0; i < users.Count; i++)
        {
            var u = users[i];
            t.Rows.Add(u.Id, u.Email, false, hashes[i], stamps[i], DBNull.Value, false, false, DBNull.Value, true, 0, u.UserName, u.DisplayName);
        }
        return t;
    }

    public DataTable UserRolesTable()
    {
        var t = TicketDeskSchema.IdentityUserRoles.NewDataTable();
        foreach (var u in Users)
        {
            if (!settings.RoleIds.TryGetValue(u.Role, out var roleId)) throw new InvalidOperationException($"role '{u.Role}' not found in the target database");
            t.Rows.Add(u.Id, roleId);
        }
        return t;
    }

    public IEnumerable<SeedChunk> TicketChunks()
    {
        int eventId = 0, tagId = 0;
        var total = settings.Scale.Tickets;
        for (int start = 1; start <= total; start += settings.ChunkSize)
        {
            var chunk = new SeedChunk
            {
                Tickets = TicketDeskSchema.Tickets.NewDataTable(),
                Tags = TicketDeskSchema.TicketTags.NewDataTable(),
                Subscribers = TicketDeskSchema.TicketSubscribers.NewDataTable(),
                Events = TicketDeskSchema.TicketEvents.NewDataTable(),
                Notifications = TicketDeskSchema.TicketEventNotifications.NewDataTable(),
            };
            var end = Math.Min(total, start + settings.ChunkSize - 1);
            for (int id = start; id <= end; id++) AddTicket(chunk, id, ref eventId, ref tagId);
            yield return chunk;
        }
    }

    private sealed record Ev(TicketActivity Activity, string By, DateTimeOffset At, string? Comment, string Description);

    private void AddTicket(SeedChunk c, int ticketId, ref int eventId, ref int tagId)
    {
        var rng = Prng.For(settings.Seed, "ticket", ticketId);
        var owners = Owners;
        var staff = Staff;
        var owner = owners[rng.Zipf(owners.Length)];
        var onBehalf = rng.Chance(0.2);
        var creator = onBehalf ? staff[rng.Next(staff.Length)] : owner;
        if (creator.Id == owner.Id) onBehalf = false;

        // creation time: skewed towards recent dates (more tickets lately)
        var ageDays = 1 + (settings.HistoryDays - 1) * Math.Pow(rng.NextDouble(), 1.6);
        var created = Trunc(settings.BaseDate.AddDays(-ageDays));
        var clock = created;
        DateTimeOffset Advance(double meanHours)
        {
            // strictly increasing and never past BaseDate: a step is at most half of the time that is left
            var remaining = (settings.BaseDate - clock).TotalMinutes;
            var step = Math.Min(1 + rng.Exponential(meanHours * 60), remaining / 2);
            var next = clock.AddMinutes(step);
            var truncated = Trunc(next);
            clock = truncated > clock ? truncated : next;
            return clock;
        }

        var projectIdx = rng.Zipf(settings.Scale.Projects);
        var projectId = settings.FirstProjectId + projectIdx;
        var type = rng.Pick(settings.TicketTypes);
        var category = rng.Pick(settings.Categories);
        var thing = rng.Pick(Vocabulary.Things);
        var title = Truncate(Capitalize($"{thing} {rng.Pick(Vocabulary.Problems)} {rng.Pick(Vocabulary.Places)}"), 500);
        var details = string.Join(" ", Enumerable.Range(0, rng.Next(1, 5)).Select(_ => rng.Pick(Vocabulary.Sentences)));

        var tagCount = rng.Next(0, 4);
        var tags = new List<string>();
        for (int i = 0; i < tagCount; i++)
        {
            var tag = Vocabulary.Tags[rng.Zipf(Vocabulary.Tags.Length)];
            if (!tags.Contains(tag) && string.Join(",", tags.Append(tag)).Length <= 100) tags.Add(tag);
        }

        var events = new List<Ev>
        {
            new(onBehalf ? TicketActivity.CreateOnBehalfOf : TicketActivity.Create, creator.Id, created, null,
                onBehalf ? $"created the ticket on behalf of {owner.DisplayName}" : "created the ticket"),
        };
        var status = TicketStatus.Active;
        SeedUser? assignee = null;
        string? priority = rng.Chance(0.3) ? rng.Pick(settings.Priorities) : null;
        var statusDate = created;
        var statusBy = creator.Id;
        var subscribers = new List<string> { owner.Id };
        void Subscribe(string id) { if (!subscribers.Contains(id)) subscribers.Add(id); }
        void Add(TicketActivity a, string by, string? comment, string description, bool statusChange)
        {
            var at = Advance(a == TicketActivity.AddComment ? 10 : 20);
            events.Add(new Ev(a, by, at, comment, description));
            if (statusChange) { statusDate = at; statusBy = by; }
        }
        string PrioritySuffix(string? p) => p == null ? "" : $" at a priority of {p}";

        // Target end state: 50% closed, 15% resolved, 5% waiting for info, 30% open.
        var roll = rng.NextDouble();
        var target = roll < 0.50 ? TicketStatus.Closed : roll < 0.65 ? TicketStatus.Resolved : roll < 0.70 ? TicketStatus.MoreInfo : TicketStatus.Active;
        var leaveUnassigned = target == TicketStatus.Active && rng.Chance(0.35);

        if (!leaveUnassigned)
        {
            var newPriority = rng.Chance(0.8) ? rng.Pick(settings.Priorities) : null;
            if (rng.Chance(0.3))
            {
                assignee = staff[rng.Next(staff.Length)];
                Add(TicketActivity.TakeOver, assignee.Id, null, "has taken over the ticket" + PrioritySuffix(newPriority), false);
            }
            else
            {
                var dispatcher = staff[rng.Next(staff.Length)];
                assignee = staff[rng.Next(staff.Length)];
                Add(TicketActivity.Assign, dispatcher.Id, rng.Chance(0.3) ? rng.Pick(Vocabulary.Replies) : null,
                    $"assigned the ticket to {assignee.DisplayName}" + PrioritySuffix(newPriority), false);
            }
            if (newPriority != null) priority = newPriority;
            Subscribe(assignee.Id);

            if (rng.Chance(0.15)) // passed on to a colleague
            {
                var previous = assignee;
                assignee = staff[rng.Next(staff.Length)];
                Add(TicketActivity.Pass, previous.Id, rng.Pick(Vocabulary.Replies), $"passed the ticket to {assignee.DisplayName}", false);
                Subscribe(assignee.Id);
            }
        }

        var comments = (int)Math.Min(8, rng.Exponential(1.6));
        for (int i = 0; i < comments; i++)
        {
            var by = assignee != null && rng.Chance(0.5) ? assignee.Id : owner.Id;
            Add(TicketActivity.AddComment, by, rng.Pick(by == owner.Id ? Vocabulary.Sentences : Vocabulary.Replies), "added comment", false);
        }

        if (assignee != null && (target == TicketStatus.MoreInfo || rng.Chance(0.1)))
        {
            Add(TicketActivity.RequestMoreInfo, assignee.Id, "Can you send more details?", "has requested more information", true);
            status = TicketStatus.MoreInfo;
            if (target != TicketStatus.MoreInfo)
            {
                Add(TicketActivity.SupplyMoreInfo, owner.Id, rng.Pick(Vocabulary.Sentences), "has provided more information and reactivated the ticket.", true);
                status = TicketStatus.Active;
            }
        }

        DateTimeOffset? resolutionDate = null;
        if (assignee != null && target is TicketStatus.Resolved or TicketStatus.Closed)
        {
            if (target == TicketStatus.Closed && rng.Chance(0.1))
            {
                Add(TicketActivity.ForceClose, owner.Id, "No longer needed.", "closed the ticket by force", true);
                status = TicketStatus.Closed;
            }
            else
            {
                Add(TicketActivity.Resolve, assignee.Id, rng.Pick(Vocabulary.Replies), "resolved the ticket", true);
                status = TicketStatus.Resolved;
                if (rng.Chance(0.7)) resolutionDate = clock;
                if (target == TicketStatus.Closed)
                {
                    if (rng.Chance(0.05)) // reopened once, then resolved again
                    {
                        Add(TicketActivity.ReOpen, owner.Id, "Still broken.", "re-opened the ticket", true);
                        Add(TicketActivity.Resolve, assignee.Id, rng.Pick(Vocabulary.Replies), "resolved the ticket", true);
                    }
                    Add(TicketActivity.Close, owner.Id, rng.Chance(0.4) ? "Thanks!" : null, "closed the ticket", true);
                    status = TicketStatus.Closed;
                }
            }
        }

        DateTimeOffset? due = rng.Chance(0.2) ? Trunc(created.AddDays(rng.Next(1, 30))) : null;
        DateTimeOffset? targetDate = rng.Chance(0.1) ? Trunc(created.AddDays(rng.Next(1, 60))) : null;
        decimal? estimate = rng.Chance(0.25) ? Math.Round((decimal)(0.5 + rng.Exponential(6)), 2) : null;
        decimal? actual = estimate != null && status is TicketStatus.Resolved or TicketStatus.Closed ? Math.Round(estimate.Value * (decimal)(0.5 + rng.NextDouble()), 2) : null;
        var last = events[^1];

        c.Tickets.Rows.Add(
            ticketId, projectId, type, category, title, details, false,
            tags.Count == 0 ? DBNull.Value : string.Join(",", tags), creator.Id, created,
            N(targetDate), N(resolutionDate), N(due), N(estimate), N(actual),
            owner.Id, assignee == null ? DBNull.Value : assignee.Id, (int)status, statusDate,
            statusBy, last.By, last.At, priority == null ? DBNull.Value : priority, rng.Chance(0.2));

        foreach (var tag in tags) c.Tags.Rows.Add(++tagId, tag, ticketId);
        foreach (var s in subscribers) c.Subscribers.Rows.Add(ticketId, s);

        var recentCutoff = settings.BaseDate.AddDays(-14);
        foreach (var e in events)
        {
            var id = ++eventId;
            c.Events.Rows.Add(ticketId, id, Truncate(e.Description, 500), e.Comment == null ? DBNull.Value : e.Comment, false, e.By, e.At, (int)e.Activity);
            foreach (var s in subscribers)
            {
                // same rule as TicketEvent.CreateSubscriberEventNotifications, plus "people read old notifications"
                var own = s == e.By;
                var read = own || e.At < recentCutoff ? own || rng.Chance(0.95) : rng.Chance(0.4);
                c.Notifications.Rows.Add(ticketId, id, s, !read, read);
            }
        }
    }

    private static object N<T>(T? v) where T : struct => v.HasValue ? v.Value : DBNull.Value;

    private static DateTimeOffset Trunc(DateTimeOffset d) => new(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, d.Offset);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>SHA-256 over every generated row in load order (used to prove determinism).</summary>
    public string Fingerprint()
    {
        // one hash per table, combined in a fixed order, so chunking does not matter
        var perTable = TicketDeskSchema.All.ToDictionary(t => t.Name, _ => IncrementalHash.CreateHash(HashAlgorithmName.SHA256));
        void Feed(DataTable t)
        {
            var sha = perTable[t.TableName];
            foreach (DataRow r in t.Rows)
            {
                var line = string.Join("\u001f", r.ItemArray.Select(v => v switch
                {
                    DateTimeOffset d => d.ToString("O"),
                    decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    DBNull => "∅",
                    _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
                })) + "\n";
                sha.AppendData(Encoding.UTF8.GetBytes(line));
            }
        }
        Feed(ProjectsTable());
        Feed(UsersTable());
        Feed(UserRolesTable());
        foreach (var chunk in TicketChunks()) foreach (var (_, t) in chunk.InLoadOrder()) Feed(t);
        using var all = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var def in TicketDeskSchema.All)
        {
            all.AppendData(perTable[def.Name].GetHashAndReset());
            perTable[def.Name].Dispose();
        }
        return Convert.ToHexStringLower(all.GetHashAndReset());
    }
}

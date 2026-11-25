using System.Data;
using Microsoft.AspNetCore.Identity;

namespace TicketDesk.Seed.Tests;

public class GeneratorTests
{
    internal static SeedSettings Settings(ScalePreset scale, long seed = 42) => new()
    {
        Scale = scale, Seed = seed, UserPassword = "Seed-Test-Password-1",
        RoleIds = TicketDeskSchema.StockRoles.ToDictionary(r => r, r => "role-" + r),
    };

    private sealed record Data(DataTable Projects, DataTable Users, DataTable Roles, DataTable Tickets, DataTable Tags, DataTable Subs, DataTable Events, DataTable Notes);

    private static readonly Lazy<Data> S = new(() => Materialize(new Generator(Settings(ScalePreset.S))));

    private static Data Materialize(Generator g)
    {
        var chunks = g.TicketChunks().ToList();
        DataTable Merge(Func<SeedChunk, DataTable> pick)
        {
            var t = pick(chunks[0]).Clone();
            foreach (var c in chunks) foreach (DataRow r in pick(c).Rows) t.ImportRow(r);
            return t;
        }
        return new Data(g.ProjectsTable(), g.UsersTable(), g.UserRolesTable(), Merge(c => c.Tickets), Merge(c => c.Tags),
            Merge(c => c.Subscribers), Merge(c => c.Events), Merge(c => c.Notifications));
    }

    [Fact]
    public void Same_seed_gives_identical_rows_and_another_seed_does_not()
    {
        var a = new Generator(Settings(ScalePreset.XS)).Fingerprint();
        var b = new Generator(Settings(ScalePreset.XS)).Fingerprint();
        var c = new Generator(Settings(ScalePreset.XS, seed: 43)).Fingerprint();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Chunk_size_does_not_change_the_data()
    {
        var baseline = Settings(ScalePreset.XS);
        var small = new Generator(new SeedSettings { Scale = baseline.Scale, Seed = baseline.Seed, UserPassword = baseline.UserPassword, RoleIds = baseline.RoleIds, ChunkSize = 7 });
        Assert.Equal(new Generator(Settings(ScalePreset.XS)).Fingerprint(), small.Fingerprint());
    }

    [Fact]
    public void Prng_is_splitmix64()
    {
        var p = new Prng(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, p.NextULong());
    }

    [Theory]
    [InlineData("XS", 200, 24, 3)]
    [InlineData("s", 10_000, 250, 5)]
    [InlineData("M", 100_000, 2_500, 10)]
    [InlineData("L", 1_000_000, 25_000, 25)]
    public void Scale_presets(string name, int tickets, int users, int projects)
    {
        var p = ScalePreset.Parse(name);
        Assert.Equal((tickets, users, projects), (p.Tickets, p.Users, p.Projects));
    }

    [Fact]
    public void Unknown_scale_is_rejected() => Assert.Throws<ArgumentException>(() => ScalePreset.Parse("XL"));

    [Fact]
    public void Scale_S_produces_proportional_row_counts()
    {
        var d = S.Value;
        Assert.Equal(10_000, d.Tickets.Rows.Count);
        Assert.Equal(250, d.Users.Rows.Count);
        Assert.Equal(250, d.Roles.Rows.Count);
        Assert.Equal(4, d.Projects.Rows.Count); // plus the existing Default project
        Assert.InRange(d.Events.Rows.Count / 10_000.0, 3.0, 8.0);
        Assert.InRange(d.Subs.Rows.Count / 10_000.0, 1.5, 3.5);
        Assert.InRange(d.Tags.Rows.Count / 10_000.0, 1.0, 2.0);
        Assert.InRange(d.Notes.Rows.Count / (double)d.Events.Rows.Count, 1.5, 3.5);
    }

    [Fact]
    public void Status_mix_matches_the_target_distribution()
    {
        var byStatus = S.Value.Tickets.Rows.Cast<DataRow>().GroupBy(r => (TicketStatus)(int)r["TicketStatus"]).ToDictionary(g => g.Key, g => g.Count() / 10_000.0);
        Assert.InRange(byStatus[TicketStatus.Closed], 0.45, 0.55);
        Assert.InRange(byStatus[TicketStatus.Resolved], 0.12, 0.18);
        Assert.InRange(byStatus[TicketStatus.MoreInfo], 0.03, 0.07);
        Assert.InRange(byStatus[TicketStatus.Active], 0.25, 0.35);
    }

    [Fact]
    public void Tickets_follow_the_domain_rules()
    {
        var d = S.Value;
        var events = d.Events.Rows.Cast<DataRow>().GroupBy(r => (int)r["TicketId"]).ToDictionary(g => g.Key, g => g.OrderBy(r => (int)r["EventId"]).ToList());
        var subs = d.Subs.Rows.Cast<DataRow>().GroupBy(r => (int)r["TicketId"]).ToDictionary(g => g.Key, g => g.Select(r => (string)r["SubscriberId"]).ToHashSet());
        var tags = d.Tags.Rows.Cast<DataRow>().GroupBy(r => (int)r["TicketId"]).ToDictionary(g => g.Key, g => g.Select(r => (string)r["TagName"]).ToList());
        var settings = Settings(ScalePreset.S);
        foreach (DataRow t in d.Tickets.Rows)
        {
            var id = (int)t["TicketId"];
            var ev = events[id];
            var first = (TicketActivity)(int)ev[0]["ForActivity"];
            var owner = (string)t["Owner"];
            var createdBy = (string)t["CreatedBy"];
            Assert.Equal(createdBy == owner ? TicketActivity.Create : TicketActivity.CreateOnBehalfOf, first);
            Assert.Equal((DateTimeOffset)t["CreatedDate"], (DateTimeOffset)ev[0]["EventDate"]);
            Assert.Equal((DateTimeOffset)t["LastUpdateDate"], (DateTimeOffset)ev[^1]["EventDate"]);
            Assert.Equal((string)t["LastUpdateBy"], (string)ev[^1]["EventBy"]);
            for (int i = 1; i < ev.Count; i++) Assert.True((DateTimeOffset)ev[i]["EventDate"] > (DateTimeOffset)ev[i - 1]["EventDate"], $"ticket {id}: events out of order");
            Assert.True((DateTimeOffset)ev[^1]["EventDate"] <= settings.BaseDate);
            Assert.True((DateTimeOffset)t["CreatedDate"] >= settings.BaseDate.AddDays(-settings.HistoryDays - 1));

            var status = (TicketStatus)(int)t["TicketStatus"];
            var statusEvents = ev.Select(e => (TicketActivity)(int)e["ForActivity"])
                .Where(a => a is TicketActivity.Resolve or TicketActivity.Close or TicketActivity.ForceClose or TicketActivity.ReOpen
                    or TicketActivity.RequestMoreInfo or TicketActivity.SupplyMoreInfo).ToList();
            var expectedLast = status switch
            {
                TicketStatus.Closed => new[] { TicketActivity.Close, TicketActivity.ForceClose },
                TicketStatus.Resolved => [TicketActivity.Resolve],
                TicketStatus.MoreInfo => [TicketActivity.RequestMoreInfo],
                _ => [TicketActivity.SupplyMoreInfo, TicketActivity.ReOpen],
            };
            if (status != TicketStatus.Active || statusEvents.Count > 0) Assert.Contains(statusEvents[^1], expectedLast);

            var assigned = t["AssignedTo"] as string;
            if (assigned == null) Assert.Equal(TicketStatus.Active, status);
            Assert.Contains(owner, subs[id]);
            if (assigned != null) Assert.Contains(assigned, subs[id]);

            var tagList = t["TagList"] as string;
            Assert.Equal(tagList?.Split(',') ?? [], tags.GetValueOrDefault(id) ?? []);
        }
    }

    [Fact]
    public void Notifications_reference_existing_events_and_subscribers()
    {
        var d = S.Value;
        var eventKeys = d.Events.Rows.Cast<DataRow>().Select(r => ((int)r["TicketId"], (int)r["EventId"])).ToHashSet();
        var subKeys = d.Subs.Rows.Cast<DataRow>().Select(r => ((int)r["TicketId"], (string)r["SubscriberId"])).ToHashSet();
        var eventBy = d.Events.Rows.Cast<DataRow>().ToDictionary(r => (int)r["EventId"], r => (string)r["EventBy"]);
        var seen = new HashSet<(int, int, string)>();
        foreach (DataRow n in d.Notes.Rows)
        {
            var (t, e, s) = ((int)n["TicketId"], (int)n["EventId"], (string)n["SubscriberId"]);
            Assert.Contains((t, e), eventKeys);
            Assert.Contains((t, s), subKeys);
            Assert.True(seen.Add((t, e, s)), "duplicate notification key");
            Assert.NotEqual((bool)n["IsNew"], (bool)n["IsRead"]);
            if (eventBy[e] == s) Assert.True((bool)n["IsRead"], "own events are read");
        }
    }

    [Fact]
    public void Keys_are_unique_and_values_fit_the_columns()
    {
        var d = S.Value;
        Assert.Equal(d.Events.Rows.Count, d.Events.Rows.Cast<DataRow>().Select(r => (int)r["EventId"]).Distinct().Count());
        Assert.Equal(d.Tags.Rows.Count, d.Tags.Rows.Cast<DataRow>().Select(r => (int)r["TicketTagId"]).Distinct().Count());
        Assert.Equal(d.Users.Rows.Count, d.Users.Rows.Cast<DataRow>().Select(r => (string)r["UserName"]).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var (def, table) in new[]
                 {
                     (TicketDeskSchema.Projects, d.Projects), (TicketDeskSchema.IdentityUsers, d.Users), (TicketDeskSchema.Tickets, d.Tickets),
                     (TicketDeskSchema.TicketTags, d.Tags), (TicketDeskSchema.TicketEvents, d.Events), (TicketDeskSchema.TicketSubscribers, d.Subs),
                 })
        {
            foreach (var col in def.Columns.Where(c => c.MaxLength != null))
            {
                var max = table.Rows.Cast<DataRow>().Select(r => r[col.Name] as string).Max(v => v?.Length ?? 0);
                Assert.True(max <= col.MaxLength, $"{def.Name}.{col.Name}: {max} > {col.MaxLength}");
            }
            foreach (var col in def.Columns.Where(c => !c.Nullable))
                Assert.DoesNotContain(table.Rows.Cast<DataRow>(), r => r[col.Name] is DBNull);
        }
        Assert.All(d.Users.Rows.Cast<DataRow>(), r => Assert.EndsWith("@seed.ticketdesk.test", (string)r["Email"]));
    }

    [Fact]
    public void Every_role_gets_users_and_the_default_project_is_used()
    {
        var d = S.Value;
        var roles = d.Roles.Rows.Cast<DataRow>().GroupBy(r => (string)r["RoleId"]).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(4, roles.Count);
        Assert.True(roles["role-TdInternalUsers"] > roles["role-TdHelpDeskUsers"]);
        var projects = d.Tickets.Rows.Cast<DataRow>().GroupBy(r => (int)r["ProjectId"]).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();
        Assert.Equal([1, 2, 3, 4, 5], projects.Select(p => p.Key));
        Assert.Equal(projects.Max(p => p.Item2), projects[0].Item2); // Zipf: the Default project is the busiest
    }
}

public class PasswordHashTests
{
    private sealed class User;

    [Fact]
    public void Identity_v2_hash_is_accepted_by_aspnetcore_identity_and_flagged_for_rehash()
    {
        var hash = IdentityV2PasswordHasher.Hash("Correct-Horse-1", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff").ToByteArray());
        Assert.Equal(68, hash.Length);
        Assert.Equal(0, Convert.FromBase64String(hash)[0]);

        var v3Default = new PasswordHasher<User>();
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, v3Default.VerifyHashedPassword(new User(), hash, "Correct-Horse-1"));
        Assert.Equal(PasswordVerificationResult.Failed, v3Default.VerifyHashedPassword(new User(), hash, "wrong"));

        var v2Mode = new PasswordHasher<User>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        Assert.Equal(PasswordVerificationResult.Success, v2Mode.VerifyHashedPassword(new User(), hash, "Correct-Horse-1"));
    }

    [Fact]
    public void Aspnetcore_v2_mode_hashes_verify_with_the_legacy_algorithm()
    {
        var v2Mode = new PasswordHasher<User>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        var produced = v2Mode.HashPassword(new User(), "Shared-Coexistence-2");
        Assert.True(IdentityV2PasswordHasher.Verify(produced, "Shared-Coexistence-2"));

        var v3 = new PasswordHasher<User>().HashPassword(new User(), "Shared-Coexistence-2");
        Assert.False(IdentityV2PasswordHasher.Verify(v3, "Shared-Coexistence-2")); // why the new app must write v2 until the legacy app is gone
    }
}

using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Database;
using TicketDesk.Identity;
using TicketDesk.Search;
using TicketDesk.Testing;
using TicketDesk.Tickets;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Writes;

namespace TicketDesk.Web.Tests;

/// <summary>
/// A copy of the baseline data (TicketDesk, the VM's baseline-s) with td-db V0011 (full-text) applied, plus four tickets with
/// made-up words written the way the legacy app writes them (plain INSERTs, no hook): the index must pick them up by itself.
/// </summary>
public sealed class FullTextBaselineDatabase : BaselineDatabase
{
    public string InternalUser { get; private set; } = "";
    public string OtherUser { get; private set; } = "";
    /// <summary>T1 owned by the internal user, T2 other project, T3 comment-only match (internal user subscribed), T4 not theirs.</summary>
    public int[] Tickets { get; } = new int[4];

    protected override async Task PrepareAsync(string connectionString)
    {
        await new SchemaMigrator(connectionString, TextWriter.Null).MigrateAsync();
        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        async Task<object> Scalar(string sql)
        {
            await using var cmd = new SqlCommand(sql, c);
            return (await cmd.ExecuteScalarAsync())!;
        }
        const string internalOnly = """
            SELECT TOP 1 ur.UserId FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = 'TdInternalUsers'
            WHERE NOT EXISTS (SELECT 1 FROM dbo.IdentityUserRoles u2 JOIN dbo.IdentityRoles r2 ON r2.Id = u2.RoleId
                              WHERE u2.UserId = ur.UserId AND r2.Name IN ('TdHelpDeskUsers', 'TdAdministrators'))
            ORDER BY ur.UserId
            """;
        InternalUser = (string)await Scalar(internalOnly);
        OtherUser = (string)await Scalar(internalOnly.Replace("ORDER BY ur.UserId", "AND ur.UserId > @me ORDER BY ur.UserId").Replace("@me", $"N'{InternalUser.Replace("'", "''")}'"));
        var rows = new (string Title, string Details, string? Tags, int Project, string Owner, string? Comment)[]
        {
            ("Zebracorn calibration failed", "flumox drift on the bench", "quokka", 1, InternalUser, null),
            ("Monitor flicker", "the zebracorn is mentioned here once", "zebracorn", 2, OtherUser, null),
            ("Dock issue", "nothing to see", null, 1, OtherUser, "zebracorn appears in a comment only"),
            ("Zebracorn with flumox", "together", null, 1, OtherUser, null),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            await using var cmd = new SqlCommand("""
                INSERT dbo.Tickets (TicketType, Category, Title, Details, IsHtml, TagList, CreatedBy, CreatedDate, Owner, AssignedTo, TicketStatus,
                    CurrentStatusDate, CurrentStatusSetBy, LastUpdateBy, LastUpdateDate, Priority, AffectsCustomer, ProjectId)
                VALUES (N'Problem', N'Hardware', @t, @d, 0, @tags, @o, SYSDATETIMEOFFSET(), @o, NULL, 0, SYSDATETIMEOFFSET(), @o, @o, SYSDATETIMEOFFSET(), N'Low', 0, @p);
                DECLARE @id int = SCOPE_IDENTITY();
                INSERT dbo.TicketEvents (TicketId, EventDescription, Comment, IsHtml, EventBy, EventDate, ForActivity)
                VALUES (@id, N'created the ticket', @comment, 0, @o, SYSDATETIMEOFFSET(), 1);
                INSERT dbo.TicketSubscribers (TicketId, SubscriberId) VALUES (@id, @o);
                SELECT @id;
                """, c);
            cmd.Parameters.AddWithValue("@t", r.Title);
            cmd.Parameters.AddWithValue("@d", r.Details);
            cmd.Parameters.AddWithValue("@tags", (object?)r.Tags ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@o", r.Owner);
            cmd.Parameters.AddWithValue("@p", r.Project);
            cmd.Parameters.AddWithValue("@comment", (object?)r.Comment ?? DBNull.Value);
            Tickets[i] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        await Exec(c, $"INSERT dbo.TicketSubscribers (TicketId, SubscriberId) VALUES ({Tickets[2]}, N'{InternalUser.Replace("'", "''")}')");
        // the real condition, not the indexes' population state (see A_ticket_written_by_the_new_app_is_found_once_the_index_caught_up):
        // the four rows are found by their words (T3 only through its comment, in TicketEvents)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!await FixtureRowsIndexedAsync(c))
        {
            if (sw.Elapsed > TimeSpan.FromMinutes(3))
                throw new TimeoutException("the fixture's rows were not indexed within 3 min:\n" + FullTextStatus.Format(await FullTextStatus.ReadAsync(connectionString)));
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
    }

    private async Task<bool> FixtureRowsIndexedAsync(SqlConnection c)
    {
        async Task<HashSet<int>> Keys(string table, string word)
        {
            await using var cmd = new SqlCommand($"SELECT CAST([KEY] AS int) FROM CONTAINSTABLE({table}, *, N'{word}')", c);
            var keys = new HashSet<int>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) keys.Add(r.GetInt32(0));
            return keys;
        }
        var zebracorn = await Keys("dbo.Tickets", "zebracorn");
        var flumox = await Keys("dbo.Tickets", "flumox");
        var quokka = await Keys("dbo.Tickets", "quokka");
        await using var events = new SqlCommand($"SELECT COUNT(*) FROM CONTAINSTABLE(dbo.TicketEvents, *, N'zebracorn') ft JOIN dbo.TicketEvents e ON e.EventId = ft.[KEY] WHERE e.TicketId = {Tickets[2]}", c);
        return zebracorn.IsSupersetOf([Tickets[0], Tickets[1], Tickets[3]]) && flumox.IsSupersetOf([Tickets[0], Tickets[3]]) && quokka.Contains(Tickets[0])
               && (int)(await events.ExecuteScalarAsync())! > 0;
    }

    /// <summary>Searches through the app's ITicketSearch until <paramref name="term"/> finds something or the time is up.</summary>
    public static async Task<(List<int> Found, TimeSpan Waited)> UntilFoundAsync(IServiceProvider services, string term, TimeSpan timeout, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            using var scope = services.CreateScope();
            var hits = await scope.ServiceProvider.GetRequiredService<ITicketSearch>().SearchAsync(new SearchRequest(term, 0, new SearchViewer("help-desk", true)), ct);
            if (hits.Count > 0 || sw.Elapsed > timeout) return (hits.Select(h => h.TicketId).ToList(), sw.Elapsed);
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }
    }
}

/// <summary>
/// Search on SQL Server full-text: the legacy page (golden parity with the legacy result list fed in), the query semantics
/// on real full-text indexes, the per-ticket rule (S12), legacy and new-app writes being searchable (C1), the retired
/// search manager (L4).
/// </summary>
public sealed class SearchTests(FullTextBaselineDatabase db) : IClassFixture<FullTextBaselineDatabase>
{
    private const string AdminName = "{secret:TD_ADMIN_NAME}";
    private static readonly string[] AdminRoles = ["TdAdministrators", "TdHelpDeskUsers", "TdInternalUsers"];
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static readonly string[] SearchRoutes = ["search.query", "search.path", "admin.search-manager", "admin.search-manager.remove", "admin.search-manager.populate"];

    private string Cs => db.ConnectionString ?? throw new InvalidOperationException();

    private void NeedDb() => Assert.SkipWhen(db.ConnectionString == null, "needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");

    private static async Task<string> AdminIdAsync(TicketDeskApp app)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        return await ctx.UserRoles.Where(ur => ctx.Roles.Any(r => r.Id == ur.RoleId && r.Name == "TdAdministrators")).Select(ur => ur.UserId).FirstAsync();
    }

    private static HttpRequestMessage Get(string path, string name, string userId, params string[] roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.PrincipalHeader, TestAuthHandler.Principal(name, userId, roles));
        return request;
    }

    private static List<int> ResultIds(string html) =>
        Regex.Matches(html, "<div id=\"ticketDetailPanel\"[\\s\\S]*?href=\"/ticket/(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();

    /// <summary>The legacy result list for the golden queries fed into the new page: the page itself must equal the recording.</summary>
    [Theory]
    [InlineData("search#002.0", "/search")]
    [InlineData("search#003.0", "/search?term=printer")]
    [InlineData("search#004.0", "/search/network")]
    [InlineData("search#005.0", "/search?term=zzqqxxnohits")]
    public async Task With_the_legacy_ranking_the_page_matches_the_golden_recording(string key, string path)
    {
        NeedDb();
        var golden = Golden.Get(key);
        Assert.Equal(path, golden.Request.Path);
        var legacyIds = ResultIds(golden.Response!.Body ?? "");
        await using var app = new TicketDeskApp(WaveParityTests.Migrated(Cs, [.. WaveParityTests.Wave01Routes, .. SearchRoutes]), testAuth: true);
        await using var fed = app.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<ITicketSearch>(_ => new FixedSearch(legacyIds))));
        using var client = fed.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        using var request = Get(path, AdminName, await AdminIdAsync(app), AdminRoles);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("modern; route=" + (path.StartsWith("/search/") ? "search.path" : "search.query"), response.Headers.GetValues("X-TicketDesk-Backend").Single());
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct));
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    private sealed class FixedSearch(IReadOnlyList<int> ids) : ITicketSearch
    {
        public Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SearchHit>>(ids.Select((id, i) => new SearchHit(id, 1000 - i)).ToList());
    }

    /// <summary>The golden queries on the real full-text index: 20 results (the legacy count), each really containing the word.</summary>
    [Theory]
    [InlineData("/search?term=printer", "printer")]
    [InlineData("/search/network", "network")]
    public async Task Golden_queries_find_twenty_tickets_that_contain_the_word(string path, string word)
    {
        NeedDb();
        await using var app = new TicketDeskApp(WaveParityTests.Migrated(Cs, [.. WaveParityTests.Wave01Routes, .. SearchRoutes]), testAuth: true);
        using var client = app.RawClient();
        using var request = Get(path, AdminName, await AdminIdAsync(app), AdminRoles);
        using var response = await client.SendAsync(request, Ct);
        var ids = ResultIds(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(20, ids.Count);
        await using var c = new SqlConnection(Cs);
        await c.OpenAsync(Ct);
        foreach (var id in ids)
        {
            await using var cmd = new SqlCommand("""
                SELECT COUNT(*) FROM dbo.Tickets t WHERE t.TicketId = @id AND (' ' + t.Title + ' ' + t.Details + ' ' + ISNULL(t.TagList, '') + ' ' LIKE @w
                    OR EXISTS (SELECT 1 FROM dbo.TicketEvents e WHERE e.TicketId = t.TicketId AND e.Comment LIKE @w))
                """, c);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@w", $"%{word}%");
            Assert.True((int)(await cmd.ExecuteScalarAsync(Ct))! == 1, $"ticket {id} does not contain '{word}'");
        }
    }

    private async Task<List<int>> SearchAsync(string term, int projectId = 0, string? viewer = null)
    {
        await using var app = new TicketDeskApp(TicketDeskApp.Settings(Cs));
        using var scope = app.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<ITicketSearch>();
        var hits = await search.SearchAsync(new SearchRequest(term, projectId, new SearchViewer(viewer ?? "help-desk", viewer == null)), Ct);
        return hits.Select(h => h.TicketId).ToList();
    }

    [Fact]
    public async Task Terms_phrases_prefixes_fields_and_ids_follow_the_legacy_syntax()
    {
        NeedDb();
        var t = db.Tickets;
        Assert.Equal(t.Order(), (await SearchAsync("zebracorn")).Order());
        Assert.Equal(t[1], (await SearchAsync("zebracorn"))[0]); // tag (x2.5) + details beats title alone
        Assert.Equal([t[0], t[3]], (await SearchAsync("+zebracorn +flumox")).Order());
        Assert.Equal([t[0], t[3]], (await SearchAsync("zebracorn AND flumox")).Order());
        Assert.Equal([t[1], t[2]], (await SearchAsync("zebracorn -flumox")).Order());
        Assert.Equal([t[1]], await SearchAsync("tags:zebracorn"));
        Assert.Equal([t[2]], await SearchAsync("events:zebracorn"));
        Assert.Equal([t[0]], await SearchAsync("\"calibration failed\""));
        Assert.Equal(t.Order(), (await SearchAsync("zebraco*")).Order());
        Assert.Equal(t[2], (await SearchAsync($"{t[2]} zebracorn"))[0]); // a ticket number ranks first
        Assert.Equal([t[1]], await SearchAsync("zebracorn", projectId: 2));
        Assert.Empty(await SearchAsync("-zebracorn"));
        Assert.Empty(await SearchAsync("\" OR 1=1 --"));
        // CONTAINS syntax in the box is just words (here "zebracorn" among them), never an error
        Assert.Equal(t.Order(), (await SearchAsync("NEAR((zebracorn, x), 5) ISABOUT( FORMSOF(")).Order());
    }

    /// <summary>S12: other internal users find only tickets they own, created, are assigned to or subscribe to (the S8 rule).</summary>
    [Fact]
    public async Task Internal_users_find_only_their_own_tickets_like_the_attachment_rule()
    {
        NeedDb();
        var t = db.Tickets;
        var found = await SearchAsync("zebracorn", viewer: db.InternalUser);
        Assert.Equal([t[0], t[2]], found.Order());
        await using var app = new TicketDeskApp(TicketDeskApp.Settings(Cs));
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, db.InternalUser), new Claim(ClaimTypes.Role, TicketDeskRoles.InternalUsers)], "test"));
        foreach (var id in t)
        {
            var row = await ctx.Tickets.AsNoTracking().Include(x => x.TicketSubscribers).SingleAsync(x => x.TicketId == id, Ct);
            var allowed = (await auth.AuthorizeAsync(user, new TicketAccessResource(row.Owner, row.AssignedTo, row.CreatedBy, row.TicketSubscribers.Select(s => s.SubscriberId).ToArray()), TicketAttachmentsRequirement.Instance)).Succeeded;
            Assert.Equal(allowed, found.Contains(id));
        }
    }

    /// <summary>
    /// The page keeps the legacy rule by default (the legacy SearchController checks the role and the selected project only):
    /// an internal user finds other people's tickets; with Search:RestrictToOwnTickets the S12 rule applies.
    /// </summary>
    [Theory]
    [InlineData(null, 4)]
    [InlineData("true", 2)]
    public async Task The_search_page_keeps_the_legacy_rule_unless_the_restriction_is_on(string? restrict, int expected)
    {
        NeedDb();
        var settings = WaveParityTests.Migrated(Cs, [.. WaveParityTests.Wave01Routes, .. SearchRoutes]);
        if (restrict != null) settings["Search:RestrictToOwnTickets"] = restrict;
        await using var app = new TicketDeskApp(settings, testAuth: true);
        using var client = app.RawClient();
        using var request = Get("/search?term=zebracorn", "Internal User", db.InternalUser, "TdInternalUsers");
        using var response = await client.SendAsync(request, Ct);
        var ids = ResultIds(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(expected, ids.Count);
        Assert.Equal(expected == 4 ? db.Tickets.Order() : new[] { db.Tickets[0], db.Tickets[2] }.Order(), ids.Order());
    }

    /// <summary>
    /// C1 resolved: a ticket the new app writes is searchable without any hook (full-text change tracking, asynchronous). The
    /// test waits for that itself, the search finding the ticket (bounded: 60 s), not for the indexes' population state: after a
    /// completed auto population (crawl log: completed, 0 documents failed) SQL Server 2022 can leave an index at "crawl not
    /// completed", populate status 6 and its population "Starting" for good, so a wait for every index to be idle timed out
    /// while the ticket was indexed (1 of 33 full runs, Round 5: docs/evidence/followups-20261113/flakes/).
    /// </summary>
    [Fact]
    public async Task A_ticket_written_by_the_new_app_is_found_once_the_index_caught_up()
    {
        NeedDb();
        await using var app = new TicketDeskApp(TicketDeskApp.Settings(Cs));
        var ticket = new TicketDesk.Data.Entities.Ticket { ProjectId = 1, Title = "Wombatrix printer jam", Details = "new app write", TicketType = "Problem", Category = "Hardware", Owner = db.OtherUser, TagList = "wombatrix" };
        using (var scope = app.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<TicketWriter>().CreateAsync(ticket, new TicketActor(db.OtherUser, true, true, true), Ct));
        var (found, waited) = await FullTextBaselineDatabase.UntilFoundAsync(app.Services, "wombatrix", TimeSpan.FromMinutes(1), Ct);
        Assert.True(found.Count > 0, $"not found after {waited.TotalSeconds:F0} s; full-text state:\n{FullTextStatus.Format(await FullTextStatus.ReadAsync(Cs, Ct))}");
        Assert.Equal([ticket.TicketId], found);
    }

    [Fact]
    public async Task Search_management_shows_the_full_text_indexes_and_the_lucene_actions_are_gone()
    {
        NeedDb();
        await using var app = new TicketDeskApp(WaveParityTests.Migrated(Cs, [.. WaveParityTests.Wave01Routes, .. SearchRoutes]), testAuth: true);
        using var client = app.RawClient();
        var adminId = await AdminIdAsync(app);
        using (var r = await client.SendAsync(Get("/admin/search-manager", AdminName, adminId, AdminRoles), Ct))
        {
            var html = await r.Content.ReadAsStringAsync(Ct);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Contains("<td>dbo.Tickets</td>", html);
            Assert.Contains("<td>dbo.TicketEvents</td>", html);
            Assert.DoesNotContain("populate-index", html);
        }
        foreach (var (path, id) in new[] { ("/admin/search-manager/remove-index", "admin.search-manager.remove"), ("/admin/search-manager/populate-index", "admin.search-manager.populate") })
        {
            using var r = await client.SendAsync(Get(path, AdminName, adminId, AdminRoles), Ct);
            Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
            Assert.Equal("modern; route=" + id, r.Headers.GetValues("X-TicketDesk-Backend").Single());
        }
        using (var r = await client.SendAsync(Get("/admin/search-manager/populate-index", "Ali Chen", "x", "TdInternalUsers"), Ct))
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); // administrators only, as before (to /access-denied)
    }
}

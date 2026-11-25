using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Testing;

[assembly: AssemblyFixture(typeof(ScratchDatabase))]

namespace TicketDesk.Web.Tests;

/// <summary>The web host over the scratch database (XS seed), with header-based test auth, plus a pending user and attachment files.</summary>
public sealed class ApiFixture(ScratchDatabase db) : IAsyncLifetime
{
    private static readonly SemaphoreSlim PendingUserLock = new(1, 1);

    public ScratchDatabase Db => db;
    public string AttachmentsRoot { get; } = Directory.CreateTempSubdirectory("td-api-attach-").FullName;
    public TicketDeskApp App { get; private set; } = null!;
    public TicketDeskApp CookieApp { get; private set; } = null!;
    public string PendingUserId { get; private set; } = "";
    public string PendingUserName { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        if (!db.Available) return;
        var settings = TicketDeskApp.Settings(db.ConnectionString);
        settings["Attachments:RootPath"] = AttachmentsRoot;
        App = new TicketDeskApp(settings, testAuth: true);
        CookieApp = new TicketDeskApp(settings);
        using var scope = App.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<TicketDeskUser>>();
        // one scratch database per test assembly, one ApiFixture per test class (classes run in parallel): create the pending user once
        await PendingUserLock.WaitAsync();
        try
        {
            var pending = await um.FindByNameAsync("pending.user@seed.ticketdesk.test");
            if (pending == null)
            {
                pending = new TicketDeskUser { UserName = "pending.user@seed.ticketdesk.test", Email = "pending.user@seed.ticketdesk.test", DisplayName = "Pending User" };
                var created = await um.CreateAsync(pending, ScratchDatabase.UserPassword);
                if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
                await um.AddToRoleAsync(pending, TicketDeskRoles.PendingUsers);
            }
            (PendingUserId, PendingUserName) = (pending.Id, pending.UserName!);
        }
        finally
        {
            PendingUserLock.Release();
        }
    }

    public HttpClient As(string? userId)
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var client = App.RawClient();
        if (userId != null) client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        if (App != null) await App.DisposeAsync();
        if (CookieApp != null) await CookieApp.DisposeAsync();
        Directory.Delete(AttachmentsRoot, true);
    }
}

public sealed class ApiTests(ApiFixture f) : IClassFixture<ApiFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private ScratchDatabase Db => f.Db;
    private string Staff => Db.UserInRole(TicketDeskRoles.HelpDeskUsers).Id;
    private string Internal => Db.UserInRole(TicketDeskRoles.InternalUsers).Id;

    private async Task<JsonElement> GetJson(string url, string? user)
    {
        using var response = await f.As(user).GetAsync(url, Ct);
        Assert.True(response.IsSuccessStatusCode, $"{url}: HTTP {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static int[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("ticketId").GetInt32()).ToArray();

    private async Task<int[]> SqlIds(string sql, params (string, object?)[] p) => (await Db.RowsAsync(sql, p)).Select(r => (int)r[0]!).ToArray();

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/tickets/1")]
    [InlineData("/api/projects")]
    public async Task Unauthenticated_calls_get_401_not_a_login_redirect(string url)
    {
        Assert.SkipUnless(Db.Available, SqlTestEnvironment.SkipReason);
        using var response = await f.CookieApp.RawClient().GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Identity_cookie_from_a_v2_password_sign_in_authenticates_the_api()
    {
        Assert.SkipUnless(Db.Available, SqlTestEnvironment.SkipReason);
        var client = f.CookieApp.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var signIn = await client.PostAsync($"/test/sign-in?u={Uri.EscapeDataString(Db.UserInRole(TicketDeskRoles.InternalUsers).UserName.ToUpperInvariant())}", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        using var projects = await client.GetAsync("/api/projects", Ct);
        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);

        var pendingClient = f.CookieApp.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var pendingSignIn = await pendingClient.PostAsync($"/test/sign-in?u={Uri.EscapeDataString(f.PendingUserName)}", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, pendingSignIn.StatusCode);
        using var forbidden = await pendingClient.GetAsync("/api/projects", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/tickets/1")]
    [InlineData("/api/projects")]
    public async Task Pending_users_are_forbidden(string url)
    {
        using var response = await f.As(f.PendingUserId).GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Projects_are_listed_by_name()
    {
        var projects = await GetJson("/api/projects", Internal);
        var ids = projects.EnumerateArray().Select(p => p.GetProperty("projectId").GetInt32()).ToArray();
        Assert.Equal(await SqlIds("SELECT ProjectId FROM dbo.Projects ORDER BY ProjectName, ProjectId"), ids);
        Assert.All(projects.EnumerateArray(), p => Assert.False(string.IsNullOrEmpty(p.GetProperty("projectName").GetString())));
    }

    [Fact]
    public async Task Default_page_matches_SQL()
    {
        var page = await GetJson("/api/tickets", Internal);
        Assert.Equal(await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets"), page.GetProperty("totalCount").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal("-lastUpdateDate,ticketId", page.GetProperty("sort").GetString());
        Assert.Equal(await SqlIds("SELECT TOP 20 TicketId FROM dbo.Tickets ORDER BY LastUpdateDate DESC, TicketId"), Ids(page));

        var first = page.GetProperty("items")[0];
        var owner = first.GetProperty("owner");
        Assert.Equal(await Db.ScalarAsync<string>("SELECT DisplayName FROM dbo.IdentityUsers WHERE Id = @id", ("@id", owner.GetProperty("id").GetString())),
            owner.GetProperty("displayName").GetString());
        Assert.Contains(first.GetProperty("status").GetString(), Enum.GetNames<TicketStatus>());
    }

    [Fact]
    public async Task Filters_match_SQL()
    {
        var project = Db.Generator!.ProjectIds[1];
        var owner = await Db.ScalarAsync<string>("SELECT TOP 1 Owner FROM dbo.Tickets GROUP BY Owner ORDER BY COUNT(*) DESC, Owner");
        var assignee = await Db.ScalarAsync<string>("SELECT TOP 1 AssignedTo FROM dbo.Tickets WHERE AssignedTo IS NOT NULL GROUP BY AssignedTo ORDER BY COUNT(*) DESC, AssignedTo");
        var tag = await Db.ScalarAsync<string>("SELECT TOP 1 TagName FROM dbo.TicketTags GROUP BY TagName ORDER BY COUNT(*) DESC, TagName");
        var cases = new (string Query, string Where, object? Param)[]
        {
            ("status=Active&status=MoreInfo", "TicketStatus IN (0, 1)", null),
            ($"projectId={project}", "ProjectId = @p", project),
            ($"owner={owner}", "Owner = @p", owner),
            ($"assignedTo={assignee}", "AssignedTo = @p", assignee),
            ($"tag={Uri.EscapeDataString(tag.ToUpperInvariant())}", "EXISTS (SELECT 1 FROM dbo.TicketTags g WHERE g.TicketId = t.TicketId AND g.TagName = @p)", tag),
            ($"status=Closed&projectId={project}", "TicketStatus = 3 AND ProjectId = @p", project),
        };
        foreach (var (query, where, param) in cases)
        {
            var page = await GetJson($"/api/tickets?{query}&pageSize=100&sort=ticketId", Staff);
            var expected = await SqlIds($"SELECT TicketId FROM dbo.Tickets t WHERE {where} ORDER BY TicketId", ("@p", param));
            Assert.True(expected.Length > 0, $"no rows for {query}");
            Assert.Equal(expected.Length, page.GetProperty("totalCount").GetInt32());
            Assert.Equal(expected.Take(100), Ids(page));
        }
    }

    [Fact]
    public async Task Legacy_lists_match_SQL()
    {
        var me = await Db.ScalarAsync<string>("""
            SELECT TOP 1 t.AssignedTo FROM dbo.Tickets t JOIN dbo.IdentityUserRoles ur ON ur.UserId = t.AssignedTo
            JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = 'TdHelpDeskUsers'
            WHERE t.TicketStatus <> 3 GROUP BY t.AssignedTo ORDER BY COUNT(*) DESC, t.AssignedTo
            """);
        var lists = new (string List, string Where)[]
        {
            ("unassigned", "TicketStatus <> 3 AND AssignedTo IS NULL"),
            ("assignedToMe", "TicketStatus <> 3 AND AssignedTo = @me"),
            ("mytickets", "TicketStatus <> 3 AND Owner = @me"),
            ("opentickets", "TicketStatus <> 3"),
            ("historytickets", "TicketStatus = 3"),
        };
        foreach (var (list, where) in lists)
        {
            var page = await GetJson($"/api/tickets?list={list}&pageSize=100&sort=ticketId", me);
            var expected = await SqlIds($"SELECT TicketId FROM dbo.Tickets WHERE {where} ORDER BY TicketId", ("@me", me));
            Assert.Equal(expected.Length, page.GetProperty("totalCount").GetInt32());
            Assert.Equal(expected.Take(100), Ids(page));
        }
        Assert.True(await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE TicketStatus <> 3 AND AssignedTo = @me", ("@me", me)) > 0);
    }

    [Theory]
    [InlineData("priority,-createdDate", "Priority, CreatedDate DESC, TicketId")]
    [InlineData("-status,title", "TicketStatus DESC, Title, TicketId")]
    [InlineData("dueDate", "DueDate, TicketId")]
    [InlineData("-ticketId", "TicketId DESC")]
    [InlineData("TicketStatus,-LastUpdateDate", "TicketStatus, LastUpdateDate DESC, TicketId")]
    public async Task Whitelisted_sorts_match_SQL_order(string sort, string orderBy)
    {
        var page = await GetJson($"/api/tickets?sort={Uri.EscapeDataString(sort)}&pageSize=50", Internal);
        Assert.Equal(await SqlIds($"SELECT TOP 50 TicketId FROM dbo.Tickets ORDER BY {orderBy}"), Ids(page));
    }

    [Theory]
    [InlineData("Details")]
    [InlineData("it.Title DESC")]
    [InlineData("Title; DROP TABLE dbo.Tickets--")]
    [InlineData("TicketId DESC")]
    [InlineData("(SELECT 1)")]
    public async Task Sort_fields_outside_the_whitelist_are_rejected(string sort)
    {
        using var response = await f.As(Internal).GetAsync($"/api/tickets?sort={Uri.EscapeDataString(sort)}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("sort", out _));
        Assert.Equal(200, await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets"));
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("status=Open", "status")]
    [InlineData("list=favourites", "list")]
    public async Task Invalid_parameters_are_400_with_the_parameter_named(string query, string field)
    {
        using var response = await f.As(Internal).GetAsync($"/api/tickets?{query}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Paging_visits_every_ticket_exactly_once_even_with_ties()
    {
        var seen = new List<int>();
        int pages;
        var pageNo = 1;
        do
        {
            var page = await GetJson($"/api/tickets?sort=priority&pageSize=37&page={pageNo}", Internal);
            pages = page.GetProperty("totalPages").GetInt32();
            seen.AddRange(Ids(page));
        } while (++pageNo <= pages);
        Assert.Equal(await SqlIds("SELECT TicketId FROM dbo.Tickets ORDER BY TicketId"), seen.Order());
    }

    [Fact]
    public async Task Ticket_detail_matches_the_database()
    {
        var id = await Db.ScalarAsync<int>("SELECT TOP 1 TicketId FROM dbo.TicketEvents GROUP BY TicketId ORDER BY COUNT(*) DESC, TicketId");
        var t = await GetJson($"/api/tickets/{id}", Staff);
        Assert.Equal(id, t.GetProperty("ticketId").GetInt32());
        Assert.Equal(await Db.ScalarAsync<string>("SELECT Title FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)), t.GetProperty("title").GetString());
        Assert.Equal(await Db.ScalarAsync<string>("SELECT p.ProjectName FROM dbo.Tickets t JOIN dbo.Projects p ON p.ProjectId = t.ProjectId WHERE t.TicketId = @id", ("@id", id)),
            t.GetProperty("projectName").GetString());
        var events = t.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("eventId").GetInt32()).ToArray();
        Assert.Equal(await SqlIds("SELECT EventId FROM dbo.TicketEvents WHERE TicketId = @id ORDER BY EventDate DESC, EventId DESC", ("@id", id)), events);
        var tags = t.GetProperty("tags").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal((await Db.RowsAsync("SELECT TagName FROM dbo.TicketTags WHERE TicketId = @id ORDER BY TicketTagId", ("@id", id))).Select(r => (string?)r[0]), tags);
        var firstEvent = t.GetProperty("events")[0];
        Assert.False(string.IsNullOrEmpty(firstEvent.GetProperty("eventBy").GetProperty("displayName").GetString()));
        Assert.Contains(firstEvent.GetProperty("forActivity").GetString()!.Split(", ")[0], Enum.GetNames<TicketActivity>());
    }

    [Theory]
    [InlineData("/api/tickets/999999")]
    [InlineData("/api/tickets/abc")]
    public async Task Unknown_ticket_is_404(string url)
    {
        using var response = await f.As(Internal).GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Attachment_metadata_is_shown_only_to_staff_and_people_on_the_ticket()
    {
        var internalIds = Db.Users.Where(u => u.Role == TicketDeskRoles.InternalUsers).Select(u => u.Id).ToArray();
        var rows = await Db.RowsAsync("SELECT TicketId, Owner, AssignedTo, CreatedBy FROM dbo.Tickets ORDER BY TicketId");
        var subscribers = (await Db.RowsAsync("SELECT TicketId, SubscriberId FROM dbo.TicketSubscribers"))
            .GroupBy(r => (int)r[0]!).ToDictionary(g => g.Key, g => g.Select(r => (string)r[1]!).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var (ticketId, owner, outsider) = rows
            .Where(r => internalIds.Contains((string)r[1]!))
            .Select(r =>
            {
                var id = (int)r[0]!;
                var involved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { (string)r[1]!, (string)r[3]! };
                if (r[2] is string a) involved.Add(a);
                if (subscribers.TryGetValue(id, out var s)) involved.UnionWith(s);
                return (id, (string)r[1]!, internalIds.FirstOrDefault(u => !involved.Contains(u)));
            })
            .First(x => x.Item3 != null);

        var folder = Directory.CreateDirectory(Path.Combine(f.AttachmentsRoot, ticketId.ToString()));
        await File.WriteAllBytesAsync(Path.Combine(folder.FullName, "screenshot.png"), new byte[2048], Ct);
        await File.WriteAllTextAsync(Path.Combine(folder.FullName, "log.txt"), "log", Ct);

        foreach (var viewer in new[] { Staff, owner })
        {
            var t = await GetJson($"/api/tickets/{ticketId}", viewer);
            Assert.True(t.GetProperty("attachmentsVisible").GetBoolean());
            var files = t.GetProperty("attachments").EnumerateArray().Select(a => (a.GetProperty("name").GetString(), a.GetProperty("size").GetInt64())).ToArray();
            Assert.Equal([("log.txt", 3L), ("screenshot.png", 2048L)], files);
        }

        var hidden = await GetJson($"/api/tickets/{ticketId}", outsider!);
        Assert.False(hidden.GetProperty("attachmentsVisible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("attachments").ValueKind);
        Assert.Equal(ticketId, hidden.GetProperty("ticketId").GetInt32()); // the ticket itself stays visible, as in the legacy app
    }
}

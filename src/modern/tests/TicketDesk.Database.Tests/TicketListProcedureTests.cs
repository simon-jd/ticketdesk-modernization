using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Testing;
using TicketDesk.Tickets;

namespace TicketDesk.Database.Tests;

/// <summary>
/// usp_TicketList returns exactly what the Round 1 LINQ list returns (the executable specification): same rows, order,
/// totals, display names, for the five legacy lists and every filter and whitelisted sort; and it refuses anything
/// outside its sort whitelist on its own, whatever the caller validated.
/// </summary>
public sealed class TicketListProcedureTests(ScratchDatabase db) : IClassFixture<ScratchDatabase>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private AppServices? _app;

    public ValueTask InitializeAsync()
    {
        if (db.Available) _app = new AppServices(db.ConnectionString!);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null) await _app.DisposeAsync();
    }

    private async Task AssertSameAsLinq(string query, string user)
    {
        var p = System.Web.HttpUtility.ParseQueryString(query);
        int? Int(string k) => p[k] is { } v ? int.Parse(v) : null;
        var request = TicketListRequest.TryCreate(new TicketListQuery
        {
            Page = Int("page"), PageSize = Int("pageSize"), List = p["list"], Sort = p["sort"], Status = p.GetValues("status"),
            ProjectId = Int("projectId"), Owner = p["owner"], AssignedTo = p["assignedTo"], Tag = p["tag"],
        }, out var errors) ?? throw new InvalidOperationException($"{query}: {string.Join(",", errors.Keys)}");
        var (linq, proc) = await _app!.Run(async sp =>
        {
            var q = sp.GetRequiredService<TicketQueries>();
            return (await q.ListWithLinqAsync(request, user, Ct), await q.ListAsync(request, user, Ct));
        });
        Assert.Equal(JsonSerializer.Serialize(linq), JsonSerializer.Serialize(proc));
    }

    public static TheoryData<string> Queries()
    {
        var data = new TheoryData<string>
        {
            "", "pageSize=100", "page=2&pageSize=7", "page=999", "pageSize=1",
            "list=unassigned", "list=assignedToMe", "list=mytickets", "list=opentickets", "list=historytickets", "list=OpenTickets&page=3",
            "status=Active", "status=Active&status=MoreInfo", "status=Resolved,Closed", "status=Active,MoreInfo,Resolved,Closed",
            "list=historytickets&status=Active", "list=unassigned&status=MoreInfo",
            "sort=-priority,createdDate&pageSize=50", "sort=title&page=2", "sort=-dueDate,-targetDate", "sort=ticketStatus,-LastUpdateDate",
            "sort=-ticketId", "sort=owner,assignedTo,createdBy", "sort=category,ticketType,-resolutionDate,currentStatusDate,projectId",
        };
        foreach (var field in TicketSort.AllowedFields)
        {
            data.Add($"sort={field}&pageSize=30");
            data.Add($"sort=-{field}&page=2&pageSize=30");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Procedure_page_equals_the_LINQ_page(string query)
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        await AssertSameAsLinq(query, db.UserInRole("TdHelpDeskUsers").Id);
    }

    /// <summary>
    /// V0013 (AKS round): the list reads committed row versions. Under READ COMMITTED it waited for a ticket write's X lock, and
    /// with the write's own index change (LastUpdateDate) the two deadlocked (1205, the reader the victim: a 500 on kind).
    /// Here a write holds its locks uncommitted; the list must answer within 5 s and show the committed value.
    /// </summary>
    [Fact]
    public async Task The_list_reads_the_committed_version_while_a_ticket_write_is_open()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        Assert.Equal(1, await db.ScalarAsync<byte>("SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()"));
        await using var writer = new SqlConnection(db.ConnectionString);
        await writer.OpenAsync(Ct);
        await using var tx = (SqlTransaction)await writer.BeginTransactionAsync(Ct);
        int ticketId;
        await using (var top = new SqlCommand("SELECT TOP (1) TicketId FROM dbo.Tickets ORDER BY LastUpdateDate DESC", writer, tx))
            ticketId = (int)(await top.ExecuteScalarAsync(Ct))!;
        await using (var update = new SqlCommand("UPDATE dbo.Tickets SET LastUpdateDate = SYSDATETIMEOFFSET(), Title = Title + N' (open write)' WHERE TicketId = @id", writer, tx))
        {
            update.Parameters.AddWithValue("@id", ticketId);
            Assert.Equal(1, await update.ExecuteNonQueryAsync(Ct));
        }

        await using var reader = new SqlConnection(db.ConnectionString);
        await reader.OpenAsync(Ct);
        await using var list = new SqlCommand("EXEC dbo.usp_TicketList @PageSize = 100", reader) { CommandTimeout = 5 };
        var titles = new List<string>();
        await using (var r = await list.ExecuteReaderAsync(Ct))
            while (await r.ReadAsync(Ct))
                titles.Add(r.GetString(r.GetOrdinal("Title")));
        Assert.NotEmpty(titles);
        Assert.DoesNotContain(titles, t => t.EndsWith(" (open write)", StringComparison.Ordinal));
        await tx.RollbackAsync(Ct);
    }

    [Fact]
    public async Task Filters_and_per_user_lists_equal_the_LINQ_results()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var owner = await db.ScalarAsync<string>("SELECT TOP 1 Owner FROM dbo.Tickets WHERE TicketStatus <> 3 GROUP BY Owner ORDER BY COUNT(*) DESC, Owner");
        var assignee = await db.ScalarAsync<string>("SELECT TOP 1 AssignedTo FROM dbo.Tickets WHERE TicketStatus <> 3 AND AssignedTo IS NOT NULL GROUP BY AssignedTo ORDER BY COUNT(*) DESC, AssignedTo");
        var tag = await db.ScalarAsync<string>("SELECT TOP 1 TagName FROM dbo.TicketTags GROUP BY TagName ORDER BY COUNT(*) DESC, TagName");
        var project = db.Generator!.ProjectIds[1];
        foreach (var (query, user) in new[]
        {
            ("list=mytickets", owner), ("list=assignedToMe", assignee), ("list=assignedToMe&sort=title", assignee),
            ($"owner={owner}", assignee), ($"assignedTo={assignee}&status=Resolved", owner), ($"projectId={project}&sort=-createdDate", owner),
            ($"tag={tag.ToUpperInvariant()}", owner), ($"tag={tag}&projectId={project}&status=Active,MoreInfo&sort=dueDate", owner),
            ($"list=mytickets&owner={assignee}", owner), ($"list=unassigned&assignedTo={assignee}", owner), ("tag=no-such-tag", owner),
            ($"owner={new string('x', 300)}", owner),
        })
            await AssertSameAsLinq(query, user);
    }

    [Theory]
    [InlineData("Title; DROP TABLE dbo.Tickets--", 50013)]
    [InlineData("(SELECT 1)", 50013)]
    [InlineData("Details", 50013)]
    [InlineData("title,-Title", 50014)]
    [InlineData("a,b,c,d,e,f", 50013)]
    [InlineData("title,priority,owner,category,dueDate,createdDate", 50014)]
    public async Task Procedure_refuses_sorts_outside_its_whitelist(string sort, int error)
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var e = await Assert.ThrowsAsync<SqlException>(() => SqlHelpers.ExecAsync(db.ConnectionString!,
            "DECLARE @c int; EXEC dbo.usp_TicketList @Sort = @s, @TotalCount = @c OUTPUT;", ("@s", sort)));
        Assert.Equal(error, e.Number);
        Assert.Equal(200, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets"));
    }

    [Theory]
    [InlineData("EXEC dbo.usp_TicketList @List = 'favourites'", 50012)]
    [InlineData("EXEC dbo.usp_TicketList @PageSize = 101", 50010)]
    [InlineData("EXEC dbo.usp_TicketList @Page = 0", 50010)]
    [InlineData("EXEC dbo.usp_TicketList @StatusMask = 16", 50011)]
    public async Task Procedure_validates_its_other_parameters(string sql, int error)
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var e = await Assert.ThrowsAsync<SqlException>(() => SqlHelpers.ExecAsync(db.ConnectionString!, sql));
        Assert.Equal(error, e.Number);
    }
}

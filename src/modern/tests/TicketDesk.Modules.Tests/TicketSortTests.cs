using TicketDesk.Data.Entities;
using TicketDesk.Tickets;

namespace TicketDesk.Modules.Tests;

/// <summary>S5: only whitelisted sort fields; the legacy app pasted the column name into Entity SQL.</summary>
public sealed class TicketSortTests
{
    [Fact]
    public void Parses_fields_directions_and_the_legacy_column_name()
    {
        Assert.True(TicketSort.TryParse("-lastUpdateDate, Title ,TicketStatus", out var keys, out var error));
        Assert.Null(error);
        Assert.Equal([new SortKey("lastUpdateDate", true), new SortKey("title", false), new SortKey("status", false)], keys);
    }

    [Theory]
    [InlineData("Details")]                          // a real column, but not sortable
    [InlineData("it.Title DESC")]                    // Entity SQL fragment
    [InlineData("title desc")]
    [InlineData("TicketId; DROP TABLE Tickets--")]
    [InlineData("Title,")]
    [InlineData("title,-title")]
    [InlineData("ticketId,title,status,priority,owner,assignedTo")]
    public void Rejects_anything_outside_the_whitelist(string sort)
    {
        Assert.False(TicketSort.TryParse(sort, out var keys, out var error));
        Assert.Empty(keys);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Empty_sort_is_allowed_and_means_the_default()
    {
        Assert.True(TicketSort.TryParse("  ", out var keys, out _));
        Assert.Empty(keys);
    }

    [Fact]
    public void Apply_orders_and_appends_ticketId_as_tie_breaker()
    {
        var day = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tickets = new[]
        {
            new Ticket { TicketId = 3, Priority = "High", LastUpdateDate = day },
            new Ticket { TicketId = 1, Priority = "Low", LastUpdateDate = day },
            new Ticket { TicketId = 2, Priority = "High", LastUpdateDate = day.AddDays(1) },
            new Ticket { TicketId = 4, Priority = "High", LastUpdateDate = day },
        }.AsQueryable();
        Assert.True(TicketSort.TryParse("priority,-lastUpdateDate", out var keys, out _));
        var ordered = TicketSort.Apply(tickets, keys, out var applied).Select(t => t.TicketId).ToArray();
        Assert.Equal([2, 3, 4, 1], ordered);
        Assert.Equal("priority,-lastUpdateDate,ticketId", string.Join(",", applied));

        Assert.True(TicketSort.TryParse("-ticketId", out var byId, out _));
        TicketSort.Apply(tickets, byId, out var appliedById);
        Assert.Equal("-ticketId", string.Join(",", appliedById));
    }
}

public sealed class TicketListRequestTests
{
    [Fact]
    public void Defaults_follow_the_legacy_lists()
    {
        var r = TicketListRequest.TryCreate(new TicketListQuery(), out var errors)!;
        Assert.Empty(errors);
        Assert.Equal((1, 20), (r.Page, r.PageSize));
        Assert.Equal("-lastUpdateDate", string.Join(",", r.Sort));

        var open = TicketListRequest.TryCreate(new TicketListQuery { List = "OpenTickets" }, out _)!;
        Assert.Equal("opentickets", open.List!.Name);
        Assert.Equal("status,targetDate,dueDate,-lastUpdateDate", string.Join(",", open.Sort));
    }

    [Theory]
    [InlineData(0, 20, null, null, "page")]
    [InlineData(1, 0, null, null, "pageSize")]
    [InlineData(1, 101, null, null, "pageSize")]
    [InlineData(1, 20, "favourites", null, "list")]
    [InlineData(1, 20, null, "Open", "status")]
    [InlineData(1, 20, null, "1", "status")]
    public void Invalid_parameters_are_reported_by_name(int page, int size, string? list, string? status, string field)
    {
        var r = TicketListRequest.TryCreate(new TicketListQuery { Page = page, PageSize = size, List = list, Status = status == null ? null : [status] }, out var errors);
        Assert.Null(r);
        Assert.Contains(field, errors.Keys);
    }

    [Fact]
    public void Statuses_parse_case_insensitively_and_comma_separated()
    {
        var r = TicketListRequest.TryCreate(new TicketListQuery { Status = ["active,moreinfo", "Closed", "ACTIVE"] }, out _)!;
        Assert.Equal([TicketStatus.Active, TicketStatus.MoreInfo, TicketStatus.Closed], r.Statuses);
    }
}

public sealed class TicketListsTests
{
    private static readonly Ticket[] Tickets =
    [
        new() { TicketId = 1, TicketStatus = TicketStatus.Active, Owner = "me", AssignedTo = null },
        new() { TicketId = 2, TicketStatus = TicketStatus.Active, Owner = "you", AssignedTo = "me" },
        new() { TicketId = 3, TicketStatus = TicketStatus.Closed, Owner = "me", AssignedTo = "me" },
        new() { TicketId = 4, TicketStatus = TicketStatus.Resolved, Owner = "you", AssignedTo = null },
        new() { TicketId = 5, TicketStatus = TicketStatus.Closed, Owner = "you", AssignedTo = null },
    ];

    [Theory]
    [InlineData("unassigned", new[] { 1, 4 })]
    [InlineData("assignedToMe", new[] { 2 })]
    [InlineData("mytickets", new[] { 1 })]
    [InlineData("opentickets", new[] { 1, 2, 4 })]
    [InlineData("historytickets", new[] { 3, 5 })]
    public void Lists_apply_the_legacy_filters(string list, int[] expected)
    {
        var ids = TicketLists.Find(list)!.Filter(Tickets.AsQueryable(), "me").Select(t => t.TicketId).Order();
        Assert.Equal(expected, ids);
    }
}

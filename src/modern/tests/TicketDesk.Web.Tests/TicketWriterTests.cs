using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Writes;

namespace TicketDesk.Web.Tests;

/// <summary>
/// The EF Core write path against a copy of the baseline data: the side effects the legacy TdDomainContext.SaveChanges added
/// (status and audit fields, the activity event, subscribers, one notification per subscriber) and Tickets.AssignedDate.
/// </summary>
public sealed class TicketWriterTests(BaselineDatabase db) : IClassFixture<BaselineDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(TicketDeskApp App, string AdminId, string HelpDeskId)> StartAsync()
    {
        if (db.ConnectionString == null) Assert.Skip("needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");
        var app = new TicketDeskApp(TicketDeskApp.Settings(db.ConnectionString));
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        var admin = await ctx.UserRoles.Where(ur => ctx.Roles.Any(r => r.Id == ur.RoleId && r.Name == "TdAdministrators")).Select(ur => ur.UserId).FirstAsync(Ct);
        var helpDesk = await ctx.UserRoles.Where(ur => ur.UserId != admin && ctx.Roles.Any(r => r.Id == ur.RoleId && r.Name == "TdHelpDeskUsers")).Select(ur => ur.UserId).FirstAsync(Ct);
        return (app, admin, helpDesk);
    }

    [Fact]
    public async Task Create_and_activities_reproduce_the_legacy_side_effects()
    {
        var (app, adminId, helpDeskId) = await StartAsync();
        await using var _ = app;
        var admin = new TicketActor(adminId, true, true, true);
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<TicketWriter>();
            var ticket = new Ticket { ProjectId = 1, Title = "Writer test", Details = "d", TicketType = "Problem", Category = "Hardware", Owner = helpDeskId, TagList = "a, b" };
            Assert.True(await writer.CreateAsync(ticket, admin, Ct));
            id = ticket.TicketId;
        }
        using (var scope = app.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
            var t = await ctx.Tickets.AsNoTracking().Include(x => x.TicketEvents).ThenInclude(e => e.TicketEventNotifications).Include(x => x.TicketSubscribers)
                .Include(x => x.TicketTags).AsSplitQuery().SingleAsync(x => x.TicketId == id, Ct);
            Assert.Equal((TicketStatus.Active, adminId, adminId, adminId), (t.TicketStatus, t.CreatedBy, t.CurrentStatusSetBy, t.LastUpdateBy));
            Assert.Null(t.AssignedDate);
            Assert.Equal(["a", "b"], t.TicketTags.Select(x => x.TagName).Order());
            var evt = Assert.Single(t.TicketEvents);
            Assert.Equal(TicketActivity.CreateOnBehalfOf, evt.ForActivity); // owner is someone else
            Assert.StartsWith("created the ticket on behalf of ", evt.EventDescription);
            Assert.Equal([helpDeskId], t.TicketSubscribers.Select(s => s.SubscriberId)); // the owner; the creator is not subscribed
            var note = Assert.Single(evt.TicketEventNotifications);
            Assert.True(note.IsNew && !note.IsRead); // someone else's event: new for the owner
        }
        using (var scope = app.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<TicketWriter>();
            var t = (await writer.FindAsync(id, Ct))!;
            Assert.Equal(TicketWriteResult.Saved, await writer.PerformAsync(t, admin, new TicketActivityInput(TicketActivity.Assign) { AssignedTo = helpDeskId, Priority = "Low", Comment = "c" }, Ct));
            Assert.Equal(TicketWriteResult.Refused, await writer.PerformAsync(t, admin, new TicketActivityInput(TicketActivity.Close), Ct)); // not resolved
        }
        using (var scope = app.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
            var t = await ctx.Tickets.AsNoTracking().Include(x => x.TicketEvents).ThenInclude(e => e.TicketEventNotifications).AsSplitQuery().SingleAsync(x => x.TicketId == id, Ct);
            Assert.Equal((helpDeskId, "Low"), (t.AssignedTo, t.Priority));
            Assert.Equal(t.LastUpdateDate, t.AssignedDate); // set by the new write path, same instant as the update (trigger rule)
            var assign = t.TicketEvents.Single(e => e.ForActivity == TicketActivity.Assign);
            Assert.EndsWith(" at a priority of Low", assign.EventDescription);
            Assert.Equal("c", assign.Comment);
            Assert.True(Assert.Single(assign.TicketEventNotifications).IsNew);
        }
        using (var scope = app.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<TicketWriter>();
            var helpDesk = new TicketActor(helpDeskId, false, true, true);
            var t = (await writer.FindAsync(id, Ct))!;
            Assert.Equal(TicketWriteResult.Saved, await writer.PerformAsync(t, helpDesk, new TicketActivityInput(TicketActivity.GiveUp) { Comment = "bye" }, Ct));
            Assert.Equal(false, await writer.ToggleSubscriptionAsync(id, helpDeskId, Ct)); // unsubscribe removes the user's notifications too
            Assert.Equal(true, await writer.ToggleSubscriptionAsync(id, helpDeskId, Ct));
            Assert.Null(await writer.ToggleSubscriptionAsync(999_999_999, helpDeskId, Ct));
        }
        using (var scope = app.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
            var t = await ctx.Tickets.AsNoTracking().SingleAsync(x => x.TicketId == id, Ct);
            Assert.Null(t.AssignedTo);
            Assert.Null(t.AssignedDate);
            Assert.Equal(0, await ctx.TicketEventNotifications.CountAsync(n => n.TicketId == id && n.SubscriberId == helpDeskId, Ct));
        }
    }

    [Fact]
    public async Task Edit_records_the_changes_in_the_comment_and_follows_tags()
    {
        var (app, adminId, _) = await StartAsync();
        await using var _ = app;
        var admin = new TicketActor(adminId, true, true, true);
        using var scope = app.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<TicketWriter>();
        var ticket = new Ticket { ProjectId = 1, Title = "Edit test", Details = "d", TicketType = "Problem", Category = "Hardware", Priority = "High", TagList = "replay,laptop" };
        Assert.True(await writer.CreateAsync(ticket, admin, Ct));
        var t = (await writer.FindAsync(ticket.TicketId, Ct))!;
        var edit = new TicketEditInput(1, "Default", "Edit test (edited)", "d", "High", "", "Problem", "Hardware", adminId, "replay,laptop,dock", null, null, "", "");
        Assert.Equal(TicketWriteResult.Saved, await writer.PerformAsync(t, admin, new TicketActivityInput(TicketActivity.EditTicketInfo) { Comment = "Edited.", Edit = edit }, Ct));
        var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        var evt = await ctx.TicketEvents.AsNoTracking().SingleAsync(e => e.TicketId == ticket.TicketId && e.ForActivity == TicketActivity.EditTicketInfo, Ct);
        // the legacy text (golden ticket-edit#007.0), CRLF as the Windows server wrote it
        Assert.Equal("Edited.\r\n<dl><dt>\r\nChanges:\r\n</dt>\r\n<dd>    Title</dd>\r\n<dd>    Tags</dd>\r\n</dl>\r\n", evt.Comment);
        Assert.Equal(["dock", "laptop", "replay"], (await ctx.TicketTags.AsNoTracking().Where(x => x.TicketId == ticket.TicketId).Select(x => x.TagName).ToListAsync(Ct)).Order());
    }
}

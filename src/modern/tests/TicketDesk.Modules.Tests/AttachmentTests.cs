using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TicketDesk.Attachments;
using TicketDesk.Identity;
using TicketDesk.Tickets;

namespace TicketDesk.Modules.Tests;

/// <summary>S8: attachment listing and resolution stay inside the ticket's folder; access is per ticket.</summary>
public sealed class AttachmentStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("td-attach-").FullName;
    private readonly string _outside = Directory.CreateTempSubdirectory("td-outside-").FullName;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public AttachmentStoreTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "7"));
        File.WriteAllBytes(Path.Combine(_root, "7", "report.pdf"), new byte[1234]);
        File.WriteAllText(Path.Combine(_root, "7", "Notes (1).txt"), "hello");
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "secret");
        File.CreateSymbolicLink(Path.Combine(_root, "7", "link.txt"), Path.Combine(_outside, "secret.txt"));
        Directory.CreateSymbolicLink(Path.Combine(_root, "8"), _outside);
        Directory.CreateDirectory(Path.Combine(_root, "pending"));
        File.WriteAllText(Path.Combine(_root, "pending", "upload.txt.0b7c"), "pending");
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    [Fact]
    public async Task Lists_regular_files_of_the_ticket_folder_only()
    {
        var store = new LocalAttachmentStore(_root);
        var files = await store.ListAsync(7, Ct);
        Assert.Equal(["Notes (1).txt", "report.pdf"], files.Select(f => f.Name));
        Assert.Equal(1234, files.Single(f => f.Name == "report.pdf").Size);
        Assert.Empty(await store.ListAsync(8, Ct));   // symlinked ticket folder
        Assert.Empty(await store.ListAsync(99, Ct));  // no folder
        Assert.Empty(await store.ListAsync(0, Ct));
    }

    [Theory]
    [InlineData("../8/secret.txt")]
    [InlineData("..")]
    [InlineData("sub/report.pdf")]
    [InlineData("..\\report.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("link.txt")]
    [InlineData("missing.pdf")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("report.pdf.")]
    [InlineData(" report.pdf")]
    [InlineData("a:b")]
    [InlineData("tab\there")]
    [InlineData("")]
    public void Refuses_names_that_leave_the_folder_or_are_unsafe(string name)
    {
        Assert.False(new LocalAttachmentStore(_root).TryResolve(7, name, out var path));
        Assert.Equal("", path);
    }

    [Fact]
    public void Resolves_a_file_inside_the_ticket_folder()
    {
        Assert.True(new LocalAttachmentStore(_root).TryResolve(7, "Notes (1).txt", out var path));
        Assert.Equal(Path.Combine(Path.GetFullPath(_root), "7", "Notes (1).txt"), path);
        Assert.False(new LocalAttachmentStore(_root).TryResolve(-7, "Notes (1).txt", out _));
        Assert.False(new LocalAttachmentStore(_root).TryResolve(8, "secret.txt", out _)); // symlinked folder
    }
}

public sealed class AttachmentAccessTests
{
    private static readonly TicketAccessResource Ticket = new("owner-id", "assignee-id", "creator-id", ["subscriber-id"]);

    private static async Task<bool> Allowed(string userId, params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.NameIdentifier, userId));
        var context = new AuthorizationHandlerContext([TicketAttachmentsRequirement.Instance], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), Ticket);
        await new TicketAttachmentsHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    [Theory]
    [InlineData("someone", TicketDeskRoles.HelpDeskUsers, true)]
    [InlineData("someone", TicketDeskRoles.Administrators, true)]
    [InlineData("owner-id", TicketDeskRoles.InternalUsers, true)]
    [InlineData("OWNER-ID", TicketDeskRoles.InternalUsers, true)]
    [InlineData("assignee-id", TicketDeskRoles.InternalUsers, true)]
    [InlineData("creator-id", TicketDeskRoles.InternalUsers, true)]
    [InlineData("subscriber-id", TicketDeskRoles.InternalUsers, true)]
    [InlineData("someone", TicketDeskRoles.InternalUsers, false)]
    [InlineData("owner-id", TicketDeskRoles.PendingUsers, false)]
    public async Task Only_staff_and_people_on_the_ticket_see_its_files(string userId, string role, bool expected) =>
        Assert.Equal(expected, await Allowed(userId, role));
}

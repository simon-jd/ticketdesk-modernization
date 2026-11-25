using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Settings;
using A = TicketDesk.Data.Entities.TicketActivity;

namespace TicketDesk.Modules.Tests;

/// <summary>The legacy activity state machine and role permissions (Ticket.GetAvailableActivites, GetValidTicketActivities).</summary>
public sealed class TicketActivityRulesTests
{
    private static readonly TicketActor Admin = new("admin", true, true, true);
    private static readonly TicketActor HelpDesk = new("hd", false, true, true);
    private static readonly TicketActor Internal = new("int", false, false, true);

    private static Ticket T(TicketStatus status, string owner, string? assigned) =>
        new() { TicketId = 1, TicketStatus = status, Owner = owner, AssignedTo = assigned };

    [Fact]
    public void New_unassigned_ticket_of_the_admin_offers_what_the_golden_ticket_page_showed()
    {
        // ticket-create-view#008.0: Comment, Take Over, Assign, Force Close, Edit Attachments, Edit
        var valid = TicketActivityRules.Valid(T(TicketStatus.Active, "admin", null), Admin);
        Assert.Equal([A.AddComment, A.TakeOver, A.Assign, A.ForceClose, A.ModifyAttachments, A.EditTicketInfo], TicketActivityRules.Flags(valid));
    }

    [Fact]
    public void Internal_user_on_own_ticket_gets_no_help_desk_activities()
    {
        // internal-user#009.0: Comment, Force Close, Edit Attachments, Edit
        var valid = TicketActivityRules.Valid(T(TicketStatus.Active, "int", null), Internal);
        Assert.Equal([A.AddComment, A.ForceClose, A.ModifyAttachments, A.EditTicketInfo], TicketActivityRules.Flags(valid));
    }

    [Fact]
    public void Closed_ticket_can_only_be_reopened()
    {
        Assert.Equal([A.ReOpen], TicketActivityRules.Flags(TicketActivityRules.Valid(T(TicketStatus.Closed, "admin", "admin"), Admin)));
    }

    [Fact]
    public void More_info_assigned_to_me_offers_supply_and_cancel()
    {
        var valid = TicketActivityRules.Valid(T(TicketStatus.MoreInfo, "other", "hd"), HelpDesk);
        Assert.True(valid.HasFlag(A.SupplyMoreInfo) && valid.HasFlag(A.CancelMoreInfo) && valid.HasFlag(A.Pass) && valid.HasFlag(A.GiveUp));
        Assert.False(valid.HasFlag(A.AddComment) || valid.HasFlag(A.Resolve) || valid.HasFlag(A.TakeOver));
    }

    [Fact]
    public void Admins_also_get_what_the_owner_could_do()
    {
        // resolved ticket owned by someone else: only the owner may close; an admin acts as the owner too
        var t = T(TicketStatus.Resolved, "owner", "hd");
        Assert.False(TicketActivityRules.IsValid(t, HelpDesk with { UserId = "x" }, A.Close));
        Assert.True(TicketActivityRules.IsValid(t, Admin, A.Close));
    }

    [Fact]
    public void Users_without_a_ticketdesk_role_get_nothing() =>
        Assert.Equal(A.None, TicketActivityRules.Valid(T(TicketStatus.Active, "p", null), new TicketActor("p", false, false, false)));

    [Fact]
    public void Settings_fall_back_to_the_legacy_defaults_and_read_the_json()
    {
        var defaults = TicketDeskSettings.Parse(null);
        Assert.True(defaults.IsEditorDefaultHtml);
        Assert.True(defaults.AllowInternalUsersToEditTags);
        Assert.False(defaults.AllowInternalUsersToEditPriority);
        var s = TicketDeskSettings.Parse(new ApplicationSetting
        {
            SelectListSettingsJson = """{"CategoryList":["A","B"],"PriorityList":["P1"],"TicketTypesList":["T"]}""",
            PermissionsSettingsJson = """{"AllowInternalUsersToEditPriority":true,"AllowInternalUsersToEditTags":false}""",
            ClientSettingsJson = """{"Settings":{"DefaultTextEditorType":"markdown"}}""",
        });
        Assert.Equal(["A", "B"], s.CategoryList);
        Assert.Equal(["P1"], s.PriorityList);
        Assert.True(s.AllowInternalUsersToEditPriority);
        Assert.False(s.AllowInternalUsersToEditTags);
        Assert.False(s.IsEditorDefaultHtml);
    }
}

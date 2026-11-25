// Port of TdDomainContext.SaveChanges side effects and TicketActionManager (TicketDesk 2.5, Ms-PL; attribution: Stephen Redd,
// https://github.com/stephenredd), as explicit domain code over EF Core (ef6-to-efcore.md #15).
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Notifications;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Pages;
using DomainStrings = TicketDesk.Localization.Domain.Strings;

namespace TicketDesk.Tickets.Writes;

public enum TicketWriteResult
{
    /// <summary>Saved (legacy: <c>SaveChanges() &gt; 0</c>, an empty 200 for the activity posts).</summary>
    Saved,
    /// <summary>The activity is not valid for this user and ticket (legacy SecurityException): nothing saved.</summary>
    Refused,
    NotFound,
}

/// <summary>The fields an activity form posts (raw strings as the legacy binder saw them: an empty field is "", not null).</summary>
public sealed record TicketActivityInput(TicketActivity Activity)
{
    public string? Comment { get; init; }
    public string? AssignedTo { get; init; }
    public string? Priority { get; init; }
    /// <summary>ReOpen: assign the reopened ticket to the current user (L3 fixed).</summary>
    public bool AssignToMe { get; init; }
    /// <summary>SupplyMoreInfo: set the ticket back to Active.</summary>
    public bool Reactivate { get; init; }
    public TicketEditInput? Edit { get; init; }
}

/// <summary>EditTicketInfo fields.</summary>
public sealed record TicketEditInput(int ProjectId, string? ProjectName, string? Title, string? Details, string? Priority, string? DueDateAsString,
    string? TicketType, string? Category, string? Owner, string? TagList, decimal? EstimatedDuration, decimal? ActualDuration,
    string? TargetDateAsString, string? ResolutionDateAsString);

/// <summary>
/// Ticket writes with the side effects the legacy <c>TdDomainContext.SaveChanges</c> added (audit and status fields, tags,
/// the activity event, subscribers, one <c>TicketEventNotification</c> per subscriber and new event) plus
/// <c>Tickets.AssignedDate</c>, which the new app sets itself (the expand trigger only covers writers that leave it out).
/// Timestamps are UTC (<see cref="TimeProvider"/>); <c>CreatedDate</c>/<c>EventDate</c> stay database defaults.
/// </summary>
public sealed class TicketWriter(TicketDeskDbContext db, TicketUsers users, ITicketChangeNotifier notifier, TimeProvider clock)
{
    private const string CrLf = "\r\n"; // the legacy app ran on Windows: StringBuilder.AppendLine wrote CRLF into comments

    /// <summary>Loads a ticket for an activity (tracked, with subscribers and project).</summary>
    public Task<Ticket?> FindAsync(int ticketId, CancellationToken ct) =>
        db.Tickets.Include(t => t.TicketSubscribers).Include(t => t.Project).AsSplitQuery().FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);

    // ---------------------------------------------------------------- new ticket

    /// <summary>
    /// Legacy <c>Tickets.Add + SaveChanges</c> (PrePopulateNewTicket, PrePopulateModifiedTicket, CreateEventNotifications) and
    /// <c>ValidateEntity</c>'s Create permission. Returns false when the user may not create tickets.
    /// </summary>
    public async Task<bool> CreateAsync(Ticket ticket, TicketActor actor, CancellationToken ct)
    {
        if (!TicketActivityRules.IsValid(ticket, actor, TicketActivity.Create)) return false;
        var now = clock.GetUtcNow();
        ticket.Owner = string.IsNullOrEmpty(ticket.Owner) ? actor.UserId : ticket.Owner;
        ticket.CreatedBy = actor.UserId;
        ticket.TicketStatus = TicketStatus.Active;
        ticket.CurrentStatusDate = now;
        ticket.CurrentStatusSetBy = actor.UserId;
        ticket.LastUpdateBy = actor.UserId;
        ticket.LastUpdateDate = now;
        ticket.AssignedDate = ticket.AssignedTo != null ? now : null;
        if (!string.IsNullOrEmpty(ticket.TagList))
            foreach (var tag in ticket.TagList.Split(','))
                ticket.TicketTags.Add(new TicketTag { TagName = tag.Trim() });

        var activity = ticket.Owner != actor.UserId ? TicketActivity.CreateOnBehalfOf : TicketActivity.Create;
        var evt = NewEvent(actor, activity, null, null, await users.NameAsync(ticket.Owner, ct));
        ticket.TicketEvents.Add(evt);
        EnsureSubscribers(ticket, ticket.Owner, ticket.AssignedTo);
        AddNotifications(ticket, evt);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        await notifier.TicketsChangedAsync([new TicketChange(ticket.TicketId, true, [evt.EventId])], ct);
        return true;
    }

    // ---------------------------------------------------------------- activities

    /// <summary>
    /// One activity on a tracked ticket (legacy <c>TicketActivityController.PerformTicketAction</c>). The comment of
    /// ModifyAttachments must already list the file changes (<paramref name="input"/>.Comment).
    /// </summary>
    public async Task<TicketWriteResult> PerformAsync(Ticket ticket, TicketActor actor, TicketActivityInput input, CancellationToken ct)
    {
        var activity = input.Activity;
        // legacy ChangeAssignment: assigning to oneself is a TakeOver
        if (activity is TicketActivity.Assign or TicketActivity.ReAssign or TicketActivity.Pass && input.AssignedTo == actor.UserId)
            activity = TicketActivity.TakeOver;
        if (!TicketActivityRules.IsValid(ticket, actor, activity)) return TicketWriteResult.Refused;

        var original = (Owner: ticket.Owner, AssignedTo: ticket.AssignedTo, Status: ticket.TicketStatus, TagList: ticket.TagList);
        bool ownerSet = false, assignedSet = false;
        var comment = input.Comment;
        string? newPriority = null, userName = null;
        switch (activity)
        {
            case TicketActivity.AddComment:
            case TicketActivity.ModifyAttachments:
                break;
            case TicketActivity.CancelMoreInfo:
                ticket.TicketStatus = TicketStatus.Active;
                break;
            case TicketActivity.Close:
            case TicketActivity.ForceClose:
                ticket.TicketStatus = TicketStatus.Closed;
                break;
            case TicketActivity.RequestMoreInfo:
                ticket.TicketStatus = TicketStatus.MoreInfo;
                break;
            case TicketActivity.Resolve:
                ticket.TicketStatus = TicketStatus.Resolved;
                break;
            case TicketActivity.GiveUp:
                ticket.AssignedTo = null;
                assignedSet = true;
                break;
            case TicketActivity.ReOpen:
                ticket.AssignedTo = input.AssignToMe ? actor.UserId : null;
                assignedSet = true;
                ticket.TicketStatus = TicketStatus.Active;
                break;
            case TicketActivity.SupplyMoreInfo:
                if (input.Reactivate) ticket.TicketStatus = TicketStatus.Active;
                break;
            case TicketActivity.TakeOver:
                ticket.AssignedTo = actor.UserId;
                assignedSet = true;
                newPriority = ChangePriority(ticket, input.Priority);
                break;
            case TicketActivity.Assign:
            case TicketActivity.ReAssign:
            case TicketActivity.Pass:
                ticket.AssignedTo = input.AssignedTo;
                assignedSet = true;
                newPriority = ChangePriority(ticket, input.Priority);
                userName = await users.NameAsync(input.AssignedTo, ct);
                break;
            case TicketActivity.EditTicketInfo:
                (comment, ownerSet) = await EditAsync(ticket, actor, input.Edit!, comment, ct);
                // legacy: EF6 validation of the required columns failed the save (an unhandled 500); here the form comes back
                if (string.IsNullOrEmpty(ticket.Title) || string.IsNullOrEmpty(ticket.TicketType) || string.IsNullOrEmpty(ticket.Category) ||
                    string.IsNullOrEmpty(ticket.Details) || string.IsNullOrEmpty(ticket.Owner))
                    return TicketWriteResult.Refused;
                break;
            default:
                return TicketWriteResult.Refused;
        }

        var now = clock.GetUtcNow();
        var evt = NewEvent(actor, activity, comment, newPriority, userName);
        ticket.TicketEvents.Add(evt);

        // PrePopulateModifiedTicket
        ticket.LastUpdateBy = actor.UserId;
        ticket.LastUpdateDate = now;
        if (ticket.TicketStatus != original.Status)
        {
            ticket.CurrentStatusDate = now;
            ticket.CurrentStatusSetBy = actor.UserId;
        }
        if (ticket.TagList != original.TagList) await ApplyTagChangesAsync(ticket, ct);
        if (ticket.AssignedTo != original.AssignedTo) ticket.AssignedDate = ticket.AssignedTo == null ? null : now;
        EnsureSubscribers(ticket, ticket.Owner, ticket.AssignedTo, ownerSet ? original.Owner : null, assignedSet ? original.AssignedTo : null);
        AddNotifications(ticket, evt);
        await db.SaveChangesAsync(ct);
        await notifier.TicketsChangedAsync([new TicketChange(ticket.TicketId, false, [evt.EventId])], ct);
        return TicketWriteResult.Saved;
    }

    /// <summary>Legacy priority rule of TakeOver/ChangeAssignment: a new priority is applied and named in the event.</summary>
    private static string? ChangePriority(Ticket ticket, string? priority)
    {
        if (string.IsNullOrEmpty(priority) || ticket.Priority == priority) return null;
        ticket.Priority = priority;
        return priority;
    }

    /// <summary>Legacy <c>TicketActionManager.EditTicketInfo</c>: applies the changes and appends the change list to the comment.</summary>
    private async Task<(string Comment, bool OwnerSet)> EditAsync(Ticket ticket, TicketActor actor, TicketEditInput e, string? comment, CancellationToken ct)
    {
        var sb = new StringBuilder(comment);
        sb.Append(CrLf);
        sb.Append("<dl><dt>").Append(CrLf);
        sb.Append(DomainStrings.Changes_Title).Append(CrLf);
        sb.Append("</dt>").Append(CrLf);
        void Line(string text) => sb.Append("<dd>    ").Append(text).Append("</dd>").Append(CrLf);
        string FromTo(string property, object? from, object? to) =>
            string.Format(CultureInfo.CurrentCulture, DomainStrings.Changes_From_To, Display(property), from, to);
        var ownerSet = false;
        var settings = await Settings.TicketDeskSettings.LoadAsync(db, ct);

        if (ticket.Title != e.Title) { Line(Display("TicketTitle")); ticket.Title = e.Title ?? ""; }
        if (ticket.ProjectId != e.ProjectId)
        {
            Line(FromTo("TicketProject", ticket.Project?.ProjectName, e.ProjectName));
            ticket.ProjectId = e.ProjectId;
        }
        if (ticket.Details != e.Details) { Line(Display("TicketDetails")); ticket.Details = e.Details ?? ""; }
        if ((actor.IsHelpDesk || settings.AllowInternalUsersToEditTags) && ticket.TagList != e.TagList)
        {
            Line(Display("TicketTagList"));
            ticket.TagList = e.TagList;
        }
        if ((actor.IsHelpDesk || settings.AllowInternalUsersToEditPriority) && ticket.Priority != e.Priority)
        {
            Line(FromTo("TicketPriority", ticket.Priority, e.Priority));
            ticket.Priority = e.Priority;
        }
        if (LegacyDates.ToShortDate(ticket.DueDate) != e.DueDateAsString)
        {
            Line(FromTo("TicketDueDate", LegacyDates.ToShortDate(ticket.DueDate), e.DueDateAsString));
            ticket.DueDate = LegacyDates.Parse(e.DueDateAsString, ticket.DueDate);
        }
        if (LegacyDates.ToShortDate(ticket.TargetDate) != e.TargetDateAsString)
        {
            Line(FromTo("TicketTargetDate", LegacyDates.ToShortDate(ticket.TargetDate), e.TargetDateAsString));
            ticket.TargetDate = LegacyDates.Parse(e.TargetDateAsString, ticket.TargetDate);
        }
        if (LegacyDates.ToShortDate(ticket.ResolutionDate) != e.ResolutionDateAsString)
        {
            Line(FromTo("TicketResolutionDate", LegacyDates.ToShortDate(ticket.ResolutionDate), e.ResolutionDateAsString));
            ticket.ResolutionDate = LegacyDates.Parse(e.ResolutionDateAsString, ticket.ResolutionDate);
        }
        if (ticket.TicketType != e.TicketType) { Line(FromTo("TicketTicketType", ticket.TicketType, e.TicketType)); ticket.TicketType = e.TicketType ?? ""; }
        if (ticket.Category != e.Category) { Line(FromTo("TicketCategory", ticket.Category, e.Category)); ticket.Category = e.Category ?? ""; }
        if (ticket.EstimatedDuration != e.EstimatedDuration)
        {
            Line(FromTo("TicketEstimatedDuration", ticket.EstimatedDuration, e.EstimatedDuration));
            ticket.EstimatedDuration = e.EstimatedDuration;
        }
        if (ticket.ActualDuration != e.ActualDuration)
        {
            Line(FromTo("TicketActualDuration", ticket.ActualDuration, e.ActualDuration));
            ticket.ActualDuration = e.ActualDuration;
        }
        if (actor.IsHelpDesk && ticket.Owner != e.Owner)
        {
            var names = await users.NamesAsync([ticket.Owner, e.Owner], ct);
            Line(FromTo("TicketOwner", names.GetValueOrDefault(ticket.Owner), e.Owner == null ? null : names.GetValueOrDefault(e.Owner)));
            ticket.Owner = e.Owner ?? "";
            ownerSet = true;
        }
        sb.Append("</dl>").Append(CrLf);
        return (sb.ToString(), ownerSet);
    }

    /// <summary>Display name of a ticket property (legacy <c>PropertyUtility.GetPropertyDisplayString</c>, the Domain resources).</summary>
    private static string Display(string resourceName) => DomainStrings.ResourceManager.GetString(resourceName, CultureInfo.CurrentUICulture) ?? resourceName;

    /// <summary>
    /// Tags follow TagList: rows for names no longer listed are removed, missing names are added (trimmed, as on create).
    /// L7: the legacy diff had an inverted condition (it re-added the kept tags and never added new ones).
    /// </summary>
    private async Task ApplyTagChangesAsync(Ticket ticket, CancellationToken ct)
    {
        await db.Entry(ticket).Collection(t => t.TicketTags).LoadAsync(ct);
        var names = string.IsNullOrEmpty(ticket.TagList) ? [] : ticket.TagList.Split(',').Select(n => n.Trim()).ToList();
        var kept = ticket.TicketTags.Where(t => names.Contains(t.TagName)).ToList();
        foreach (var tag in ticket.TicketTags.Except(kept).ToList()) db.TicketTags.Remove(tag);
        foreach (var name in names.Distinct().Where(n => kept.All(t => t.TagName != n)))
            ticket.TicketTags.Add(new TicketTag { TagName = name });
    }

    // ---------------------------------------------------------------- subscription

    /// <summary>
    /// Legacy <c>ChangeTicketSubscription</c>: toggles the user's subscriber row. Removing it also removes the user's event
    /// notifications of the ticket (the notification key includes the subscriber, so EF6 deleted the tracked rows with it).
    /// Returns the new state, or null when the ticket does not exist.
    /// </summary>
    public async Task<bool?> ToggleSubscriptionAsync(int ticketId, string userId, CancellationToken ct)
    {
        var ticket = await db.Tickets.Include(t => t.TicketSubscribers).FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
        if (ticket == null) return null;
        var subscriber = ticket.TicketSubscribers.FirstOrDefault(s => s.SubscriberId == userId);
        if (subscriber == null)
        {
            ticket.TicketSubscribers.Add(new TicketSubscriber { SubscriberId = userId });
            await db.SaveChangesAsync(ct);
            return true;
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.TicketEventNotifications.Where(n => n.TicketId == ticketId && n.SubscriberId == userId).ExecuteDeleteAsync(ct);
        // rows this context still tracks from an earlier write are gone now: forget them, or EF would try to re-key them
        foreach (var tracked in db.ChangeTracker.Entries<TicketEventNotification>()
                     .Where(e => e.Entity.TicketId == ticketId && e.Entity.SubscriberId == userId).ToList())
            tracked.State = EntityState.Detached;
        db.TicketSubscribers.Remove(subscriber);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return false;
    }

    // ---------------------------------------------------------------- shared rules

    private static TicketEvent NewEvent(TicketActor actor, TicketActivity activity, string? comment, string? newPriority, string? userName) => new()
    {
        ForActivity = activity,
        Comment = comment,
        EventBy = actor.UserId,
        EventDescription = EventDescription(activity, newPriority, userName),
        IsHtml = false,
    };

    /// <summary>Legacy <c>TicketTextUtility.GetTicketEventDescription</c> (Domain resources, current UI culture).</summary>
    public static string EventDescription(TicketActivity activity, string? newPriority, string? userName)
    {
        var val = DomainStrings.ResourceManager.GetString("TicketActivity" + activity, CultureInfo.CurrentUICulture)
                  ?? throw new InvalidOperationException($"no resource for TicketActivity{activity}");
        var pval = DomainStrings.ResourceManager.GetString("TicketActivityPriority", CultureInfo.CurrentUICulture)!;
        if (!string.IsNullOrEmpty(userName)) val = string.Format(CultureInfo.CurrentCulture, val, userName);
        if (!string.IsNullOrEmpty(newPriority)) val += string.Format(CultureInfo.CurrentCulture, pval, newPriority);
        return val;
    }

    /// <summary>Legacy <c>Ticket.EnsureSubscribers</c>: owner, assignee and the previous owner/assignee of this change.</summary>
    private static void EnsureSubscribers(Ticket ticket, params string?[] userIds)
    {
        foreach (var user in userIds)
            if (user != null && ticket.TicketSubscribers.All(s => s.SubscriberId != user))
                ticket.TicketSubscribers.Add(new TicketSubscriber { SubscriberId = user });
    }

    /// <summary>Legacy <c>TicketEvent.CreateSubscriberEventNotifications</c>: the author's own row is read, everyone else's new.</summary>
    private static void AddNotifications(Ticket ticket, TicketEvent evt)
    {
        foreach (var subscriber in ticket.TicketSubscribers)
        {
            var own = evt.EventBy == subscriber.SubscriberId;
            evt.TicketEventNotifications.Add(new TicketEventNotification
            {
                SubscriberId = subscriber.SubscriberId,
                TicketSubscriber = subscriber,
                IsNew = !own,
                IsRead = own,
            });
        }
    }
}

/// <summary>The legacy <c>*AsString</c> date properties of <c>Ticket</c> (short date of the current culture; parsed with it).</summary>
public static class LegacyDates
{
    /// <summary>Getter: <c>Date.Value.Date.ToShortDateString()</c>, or "" without a date.</summary>
    public static string ToShortDate(DateTimeOffset? date) => date.HasValue ? date.Value.Date.ToString("d", CultureInfo.CurrentCulture) : "";

    /// <summary>
    /// Setter: blank clears the date; a value that <c>DateTime.TryParse</c> accepts (current culture) becomes a date in the
    /// server's offset; anything else leaves the date unchanged.
    /// </summary>
    public static DateTimeOffset? Parse(string? value, DateTimeOffset? current)
    {
        if (string.IsNullOrEmpty((value ?? "").Trim())) return null;
        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dt) ? new DateTimeOffset(dt) : current;
    }
}

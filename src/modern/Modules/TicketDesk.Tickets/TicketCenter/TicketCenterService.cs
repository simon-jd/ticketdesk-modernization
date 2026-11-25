using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Users;

namespace TicketDesk.Tickets.TicketCenter;

/// <summary>One row of the ticket-center table (a projection: the query does not read columns the legacy schema lacks).</summary>
public sealed record TicketRow(int TicketId, string Title, int ProjectId, string? ProjectName, TicketStatus Status, string Owner, string? OwnerName,
    string? AssignedTo, string? AssignedToName, string TicketType, string Category, string? Priority, DateTimeOffset? TargetDate,
    DateTimeOffset? DueDate, DateTimeOffset CreatedDate, DateTimeOffset LastUpdateDate)
{
    public bool IsOpen => Status != TicketStatus.Closed;

    /// <summary>Legacy <c>Ticket.IsDue</c>: the due date (its own clock) is <paramref name="today"/> (the server's date).</summary>
    public bool IsDue(DateTime today) => IsOpen && DueDate.HasValue && DueDate.Value.DateTime.Date == today;

    /// <summary>Legacy <c>Ticket.IsOverDue</c>: the due date is before <paramref name="today"/>.</summary>
    public bool IsOverDue(DateTime today) => IsOpen && DueDate.HasValue && DueDate.Value.DateTime.Date < today;
}

/// <summary>A page of tickets with the totals the X.PagedList pager needs.</summary>
public sealed record TicketListPage(IReadOnlyList<TicketRow> Items, int PageNumber, int PageSize, int TotalItemCount)
{
    public int PageCount => TotalItemCount > 0 ? (int)Math.Ceiling(TotalItemCount / (double)PageSize) : 0;
}

/// <summary>
/// Ticket center (legacy <c>TicketCenterController</c> + <c>TicketCenterListViewModel</c>): per-user list settings shared
/// with the legacy app, the filtered/sorted/paged list, and the user lists of the filter bar.
/// </summary>
public sealed class TicketCenterService(TicketDeskDbContext db, IUserDirectory users)
{
    /// <summary>Columns a list may be sorted by (S5: the legacy app pasted any name into Entity SQL).</summary>
    private static readonly Dictionary<string, LambdaExpression> SortKeys = new(StringComparer.Ordinal)
    {
        ["TicketId"] = Key(t => t.TicketId),
        ["Title"] = Key(t => t.Title),
        ["TicketStatus"] = Key(t => t.TicketStatus),
        ["Owner"] = Key(t => t.Owner),
        ["AssignedTo"] = Key(t => t.AssignedTo),
        ["TicketType"] = Key(t => t.TicketType),
        ["Category"] = Key(t => t.Category),
        ["Priority"] = Key(t => t.Priority),
        ["TargetDate"] = Key(t => t.TargetDate),
        ["DueDate"] = Key(t => t.DueDate),
        ["CreatedDate"] = Key(t => t.CreatedDate),
        ["LastUpdateDate"] = Key(t => t.LastUpdateDate),
        ["ProjectId"] = Key(t => t.ProjectId),
        ["CreatedBy"] = Key(t => t.CreatedBy),
        ["CurrentStatusDate"] = Key(t => t.CurrentStatusDate),
        ["ResolutionDate"] = Key(t => t.ResolutionDate),
    };

    private static LambdaExpression Key<T>(Expression<Func<Ticket, T>> e) => e;

    public static bool IsSortable(string? column) => column != null && SortKeys.ContainsKey(column);

    /// <summary>
    /// The user's lists: the stored row when it has the lists the role needs, otherwise the defaults. Never writes: the
    /// legacy GET created or reset the row (expected difference S3, part); the new app writes only when the user changes a
    /// filter, a sort or resets the lists.
    /// </summary>
    public async Task<List<ListSetting>> GetListsAsync(string userId, bool isHelpDeskOrAdmin, Func<string, string> displayName, CancellationToken ct)
    {
        var json = await db.UserSettings.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.ListSettingsJson).FirstOrDefaultAsync(ct);
        var lists = ListSettings.Parse(json);
        if (lists == null || !ListSettings.HasRequiredLists(lists, isHelpDeskOrAdmin)) lists = ListSettings.Defaults(userId, isHelpDeskOrAdmin, displayName);
        return lists;
    }

    /// <summary>Upserts the user's row (SelectedProjectId is kept).</summary>
    public async Task SaveListsAsync(string userId, IEnumerable<ListSetting> lists, CancellationToken ct)
    {
        var row = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (row == null)
        {
            row = new UserSetting { UserId = userId };
            db.UserSettings.Add(row);
        }
        row.ListSettingsJson = ListSettings.Serialize(lists);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Legacy <c>AutoCompleteController.TagList</c>: up to 10 distinct tag names starting with the term. The legacy query had
    /// no ORDER BY (the order was whatever the plan produced); here it is by name, the order the legacy app also returns on the shared schema (expected difference D1).
    /// </summary>
    public async Task<IReadOnlyList<string>> TagSuggestionsAsync(string? term, CancellationToken ct) =>
        await db.TicketTags.AsNoTracking().Where(t => t.TagName.StartsWith(term ?? ""))
            .Select(t => t.TagName).Distinct().OrderBy(n => n).Take(10).ToListAsync(ct);

    /// <summary>Who is on a ticket (for the per-ticket attachment rule, S8); null when there is no such ticket.</summary>
    public async Task<TicketAccessResource?> GetAccessAsync(int ticketId, CancellationToken ct)
    {
        var t = await db.Tickets.AsNoTracking().Where(x => x.TicketId == ticketId)
            .Select(x => new { x.Owner, x.AssignedTo, x.CreatedBy }).FirstOrDefaultAsync(ct);
        if (t == null) return null;
        var subscribers = await db.TicketSubscribers.AsNoTracking().Where(s => s.TicketId == ticketId).Select(s => s.SubscriberId).ToListAsync(ct);
        return new TicketAccessResource(t.Owner, t.AssignedTo, t.CreatedBy, subscribers);
    }

    /// <summary>Legacy <c>ChangeTicketSubscription</c>: toggles the user's TicketSubscribers row; null when there is no such ticket.</summary>
    public async Task<bool?> ToggleSubscriptionAsync(int ticketId, string userId, CancellationToken ct)
    {
        if (!await db.Tickets.AnyAsync(t => t.TicketId == ticketId, ct)) return null;
        var row = await db.TicketSubscribers.FirstOrDefaultAsync(s => s.TicketId == ticketId && s.SubscriberId == userId, ct);
        if (row == null) db.TicketSubscribers.Add(new TicketSubscriber { TicketId = ticketId, SubscriberId = userId });
        else db.TicketSubscribers.Remove(row);
        await db.SaveChangesAsync(ct);
        return row == null;
    }

    public async Task<TicketListPage> GetPageAsync(ListSetting list, int selectedProjectId, int page, CancellationToken ct)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "page numbers start at 1");
        var q = db.Tickets.AsNoTracking();
        foreach (var f in list.FilterColumns) q = ApplyFilter(q, f);
        if (selectedProjectId != 0) q = q.Where(t => t.ProjectId == selectedProjectId);

        var total = await q.CountAsync(ct);
        var size = list.ItemsPerPage > 0 ? list.ItemsPerPage : 20;
        var ordered = ApplySort(q, list.SortColumns);
        var rows = await ordered.Skip((page - 1) * size).Take(size)
            .Select(t => new
            {
                t.TicketId, t.Title, t.ProjectId, t.TicketStatus, t.Owner, t.AssignedTo, t.TicketType,
                t.Category, t.Priority, t.TargetDate, t.DueDate, t.CreatedDate, t.LastUpdateDate,
            })
            .ToListAsync(ct);
        // no join in the paged query: same plan shape as the legacy query, so rows with equal sort keys keep the legacy order
        var projectNames = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.ProjectId, p => p.ProjectName, ct);
        var names = await users.GetDisplayNamesAsync(rows.SelectMany(r => new[] { r.Owner, r.AssignedTo ?? "" }), ct);
        var items = rows.Select(r => new TicketRow(r.TicketId, r.Title, r.ProjectId, projectNames.GetValueOrDefault(r.ProjectId), r.TicketStatus, r.Owner, names.GetValueOrDefault(r.Owner),
            r.AssignedTo, r.AssignedTo == null ? null : names.GetValueOrDefault(r.AssignedTo), r.TicketType, r.Category, r.Priority, r.TargetDate, r.DueDate,
            r.CreatedDate, r.LastUpdateDate)).ToList();
        return new TicketListPage(items, page, size, total);
    }

    /// <summary>
    /// Legacy Entity SQL filter <c>it.Column = @p</c> (or <c>!=</c> when UseEqualityComparison is false). A null value
    /// means "is null" (EF6 sent it that way; verified as E1). Only the columns the filter bar can produce are honoured.
    /// </summary>
    internal static IQueryable<Ticket> ApplyFilter(IQueryable<Ticket> q, FilterColumn f)
    {
        var notEqual = f.UseEqualityComparison == false;
        switch (f.ColumnName)
        {
            case "TicketStatus":
                var status = f.StatusValue();
                if (status == null) return q.Where(_ => false);
                var s = status.Value;
                return notEqual ? q.Where(t => t.TicketStatus != s) : q.Where(t => t.TicketStatus == s);
            case "Owner":
                var owner = f.StringValue();
                if (owner == null) return notEqual ? q : q.Where(_ => false); // Owner is NOT NULL
                return notEqual ? q.Where(t => t.Owner != owner) : q.Where(t => t.Owner == owner);
            case "AssignedTo":
                var assigned = f.ColumnValue == null ? null : f.StringValue();
                if (assigned == null) return notEqual ? q.Where(t => t.AssignedTo != null) : q.Where(t => t.AssignedTo == null);
                return notEqual ? q.Where(t => t.AssignedTo != assigned) : q.Where(t => t.AssignedTo == assigned);
            default:
                return q;
        }
    }

    /// <summary>
    /// Legacy <c>ORDER BY it.Col [DESC], ...</c> plus <c>TicketId</c> last, so rows with equal sort keys (e.g. a sort by
    /// title) come back in a stable order. The legacy query had no tie-breaker: its order within ties depended on the plan,
    /// so it cannot be reproduced (expected difference S5). Unknown columns (only possible in a tampered row) are skipped.
    /// </summary>
    internal static IQueryable<Ticket> ApplySort(IQueryable<Ticket> q, IReadOnlyList<SortColumn> sorts)
    {
        IOrderedQueryable<Ticket>? ordered = null;
        foreach (var s in sorts)
        {
            if (!SortKeys.TryGetValue(s.ColumnName, out var key)) continue;
            var desc = s.SortDirection == SortDirection.Descending;
            var method = ordered == null ? (desc ? "OrderByDescending" : "OrderBy") : (desc ? "ThenByDescending" : "ThenBy");
            var call = Expression.Call(typeof(Queryable), method, [typeof(Ticket), key.ReturnType], (ordered ?? q).Expression, Expression.Quote(key));
            ordered = (IOrderedQueryable<Ticket>)q.Provider.CreateQuery<Ticket>(call);
        }
        if (ordered == null) return q.OrderBy(t => t.TicketId);
        return sorts.Any(s => s.ColumnName == "TicketId") ? ordered : ordered.ThenBy(t => t.TicketId);
    }
}

using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Attachments;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Users;

namespace TicketDesk.Tickets;

public sealed record TicketListRequest(
    int Page,
    int PageSize,
    TicketLists.ListDefinition? List,
    IReadOnlyList<SortKey> Sort,
    IReadOnlyList<TicketStatus> Statuses,
    int? ProjectId,
    string? Owner,
    string? AssignedTo,
    string? Tag)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Validates the raw query; returns errors keyed by parameter name (ValidationProblem shape).</summary>
    public static TicketListRequest? TryCreate(TicketListQuery q, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var page = q.Page ?? 1;
        var size = q.PageSize ?? DefaultPageSize;
        if (page < 1) errors["page"] = ["Must be 1 or greater."];
        if (size is < 1 or > MaxPageSize) errors["pageSize"] = [$"Must be between 1 and {MaxPageSize}."];

        TicketLists.ListDefinition? list = null;
        if (!string.IsNullOrWhiteSpace(q.List))
        {
            list = TicketLists.Find(q.List);
            if (list == null) errors["list"] = [$"Unknown list. Allowed: {string.Join(", ", TicketLists.All.Select(l => l.Name))}."];
        }

        if (!TicketSort.TryParse(q.Sort, out var sort, out var sortError)) errors["sort"] = [sortError!];

        var statuses = new List<TicketStatus>();
        foreach (var s in (q.Status ?? []).SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (Enum.TryParse<TicketStatus>(s, ignoreCase: true, out var st) && Enum.IsDefined(st) && !int.TryParse(s, out _)) statuses.Add(st);
            else errors["status"] = [$"Unknown status '{s}'. Allowed: {string.Join(", ", Enum.GetNames<TicketStatus>())}."];
        }

        if (errors.Count > 0) return null;
        return new TicketListRequest(page, size, list, sort.Count > 0 ? sort : list?.DefaultSort ?? TicketLists.DefaultSort,
            statuses.Distinct().ToArray(), q.ProjectId, Blank(q.Owner), Blank(q.AssignedTo), Blank(q.Tag));
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Row shape of <c>dbo.usp_TicketList</c>'s result set.</summary>
internal sealed class TicketListRow
{
    public int TicketId { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string TicketType { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Priority { get; set; }
    public int TicketStatus { get; set; }
    public string Owner { get; set; } = "";
    public string? OwnerDisplayName { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset LastUpdateDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public string? TagList { get; set; }
}

/// <summary>Read side of the Tickets module (no tracking, no writes; the legacy GET also created UserSettings rows).</summary>
public sealed class TicketQueries(TicketDeskDbContext db, IUserDirectory users, IAttachmentStore attachments, IAuthorizationService authorization,
    Rendering.LegacyTextRenderer renderer)
{
    public IQueryable<Ticket> Filter(TicketListRequest r, string currentUserId)
    {
        IQueryable<Ticket> q = db.Tickets.AsNoTracking();
        if (r.List != null) q = r.List.Filter(q, currentUserId);
        if (r.Statuses.Count > 0)
        {
            var statuses = r.Statuses.ToArray();
            q = q.Where(t => statuses.Contains(t.TicketStatus));
        }
        if (r.ProjectId is { } projectId) q = q.Where(t => t.ProjectId == projectId);
        if (r.Owner is { } owner) q = q.Where(t => t.Owner == owner);
        if (r.AssignedTo is { } assignee) q = q.Where(t => t.AssignedTo == assignee);
        if (r.Tag is { } tag) q = q.Where(t => t.TicketTags.Any(x => x.TagName == tag));
        return q;
    }

    /// <summary>
    /// One page through <c>dbo.usp_TicketList</c> (count, page and display names in one round trip; tuned indexes and the
    /// parameter-sniffing design are described in the procedure and in docs/sql/tuning.md).
    /// </summary>
    public async Task<TicketPage> ListAsync(TicketListRequest r, string currentUserId, CancellationToken ct)
    {
        var applied = TicketSort.WithTieBreaker(r.Sort);
        // a filter value longer than its column can never match (the LINQ reference returns an empty page too);
        // answer without sending a value that a procedure parameter would silently truncate
        if (r.Owner?.Length > 256 || r.AssignedTo?.Length > 256 || r.Tag?.Length > 100)
            return new TicketPage(r.Page, r.PageSize, 0, 0, string.Join(",", applied), []);

        var total = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
        SqlParameter[] parameters =
        [
            new("@List", SqlDbType.VarChar, 20) { Value = (object?)r.List?.Name ?? DBNull.Value },
            new("@UserId", SqlDbType.NVarChar, 256) { Value = currentUserId },
            new("@StatusMask", SqlDbType.TinyInt) { Value = r.Statuses.Count == 0 ? DBNull.Value : (byte)r.Statuses.Aggregate(0, (m, s) => m | (1 << (int)s)) },
            new("@ProjectId", SqlDbType.Int) { Value = (object?)r.ProjectId ?? DBNull.Value },
            new("@Owner", SqlDbType.NVarChar, 256) { Value = (object?)r.Owner ?? DBNull.Value },
            new("@AssignedTo", SqlDbType.NVarChar, 256) { Value = (object?)r.AssignedTo ?? DBNull.Value },
            new("@Tag", SqlDbType.NVarChar, 100) { Value = (object?)r.Tag ?? DBNull.Value },
            new("@Sort", SqlDbType.VarChar, 200) { Value = string.Join(",", r.Sort) },
            new("@Page", SqlDbType.Int) { Value = r.Page },
            new("@PageSize", SqlDbType.Int) { Value = r.PageSize },
            total,
        ];
        var rows = await db.Database.SqlQueryRaw<TicketListRow>(
                "EXEC dbo.usp_TicketList @List = @List, @UserId = @UserId, @StatusMask = @StatusMask, @ProjectId = @ProjectId, " +
                "@Owner = @Owner, @AssignedTo = @AssignedTo, @Tag = @Tag, @Sort = @Sort, @Page = @Page, @PageSize = @PageSize, " +
                "@TotalCount = @TotalCount OUTPUT", parameters)
            .ToListAsync(ct);
        var count = total.Value is int n ? n : 0;
        var items = rows.Select(x => new TicketListItem(x.TicketId, x.ProjectId, x.Title, x.TicketType, x.Category, x.Priority,
            (TicketStatus)x.TicketStatus, new UserRef(x.Owner, x.OwnerDisplayName), x.AssignedTo == null ? null : new UserRef(x.AssignedTo, x.AssignedToDisplayName),
            x.CreatedDate, x.LastUpdateDate, x.DueDate, x.TargetDate, x.TagList)).ToList();
        var pages = count == 0 ? 0 : (int)Math.Ceiling(count / (double)r.PageSize);
        return new TicketPage(r.Page, r.PageSize, count, pages, string.Join(",", applied), items);
    }

    /// <summary>
    /// The Round 1 EF Core LINQ implementation of the same list: the executable specification the procedure is compared
    /// with in the tests, and the "naive port" of docs/sql/tuning.md. Not used by the API.
    /// </summary>
    internal async Task<TicketPage> ListWithLinqAsync(TicketListRequest r, string currentUserId, CancellationToken ct)
    {
        var filtered = Filter(r, currentUserId);
        var total = await filtered.CountAsync(ct);
        var rows = await TicketSort.Apply(filtered, r.Sort, out var applied)
            .Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(t => new
            {
                t.TicketId, t.ProjectId, t.Title, t.TicketType, t.Category, t.Priority, t.TicketStatus, t.Owner, t.AssignedTo,
                t.CreatedDate, t.LastUpdateDate, t.DueDate, t.TargetDate, t.TagList,
            })
            .ToListAsync(ct);
        var names = await users.GetDisplayNamesAsync(rows.SelectMany(x => new[] { x.Owner, x.AssignedTo ?? "" }), ct);
        var items = rows.Select(x => new TicketListItem(x.TicketId, x.ProjectId, x.Title, x.TicketType, x.Category, x.Priority,
            x.TicketStatus, names.ToUserRef(x.Owner), x.AssignedTo == null ? null : names.ToUserRef(x.AssignedTo),
            x.CreatedDate, x.LastUpdateDate, x.DueDate, x.TargetDate, x.TagList)).ToList();
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)r.PageSize);
        return new TicketPage(r.Page, r.PageSize, total, pages, string.Join(",", applied), items);
    }

    public Task<bool> ExistsAsync(int ticketId, CancellationToken ct) => db.Tickets.AnyAsync(t => t.TicketId == ticketId, ct);

    public async Task<TicketDetail?> GetAsync(int ticketId, ClaimsPrincipal user, CancellationToken ct)
    {
        var t = await db.Tickets.AsNoTracking()
            .Where(x => x.TicketId == ticketId)
            .Include(x => x.Project)
            .Include(x => x.TicketEvents)
            .Include(x => x.TicketTags)
            .Include(x => x.TicketSubscribers)
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
        if (t == null) return null;

        var events = t.TicketEvents.OrderByDescending(e => e.EventDate).ThenByDescending(e => e.EventId).ToList();
        var names = await users.GetDisplayNamesAsync(
            new[] { t.Owner, t.AssignedTo ?? "", t.CreatedBy, t.CurrentStatusSetBy, t.LastUpdateBy }.Concat(events.Select(e => e.EventBy)), ct);

        var resource = new TicketAccessResource(t.Owner, t.AssignedTo, t.CreatedBy, t.TicketSubscribers.Select(s => s.SubscriberId).ToArray());
        var canSeeFiles = (await authorization.AuthorizeAsync(user, resource, TicketAttachmentsRequirement.Instance)).Succeeded;
        var files = canSeeFiles ? await attachments.ListAsync(t.TicketId, ct) : null;

        return new TicketDetail(
            t.TicketId, t.ProjectId, t.Project?.ProjectName, t.Title, t.Details, t.IsHtml, t.TicketType, t.Category, t.Priority,
            t.TicketStatus, t.AffectsCustomer, names.ToUserRef(t.Owner), t.AssignedTo == null ? null : names.ToUserRef(t.AssignedTo),
            names.ToUserRef(t.CreatedBy), t.CreatedDate, names.ToUserRef(t.CurrentStatusSetBy), t.CurrentStatusDate,
            names.ToUserRef(t.LastUpdateBy), t.LastUpdateDate, t.DueDate, t.TargetDate, t.ResolutionDate, t.EstimatedDuration,
            t.ActualDuration, t.TagList, t.TicketTags.OrderBy(x => x.TicketTagId).Select(x => x.TagName).ToList(),
            events.Select(e => new TicketEventDto(e.EventId, names.ToUserRef(e.EventBy), e.EventDate, e.EventDescription, e.Comment,
                e.IsHtml, e.ForActivity) { CommentHtml = renderer.Render(e.Comment, e.IsHtml) }).ToList(),
            canSeeFiles, files)
        {
            DetailsHtml = renderer.Render(t.Details, t.IsHtml),
            IsSubscribed = t.TicketSubscribers.Any(s => string.Equals(s.SubscriberId, user.GetUserId(), StringComparison.OrdinalIgnoreCase)),
            Activities = TicketActivitiesApi.For(t, Activities.TicketActor.From(user)),
        };
    }
}

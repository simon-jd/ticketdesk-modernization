using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;

namespace TicketDesk.Projects;

/// <summary>A row of the admin project list.</summary>
public sealed record ProjectWithTicketCount(int ProjectId, string ProjectName, string? ProjectDescription, int NumberOfTickets);

/// <summary>The project menu of the layout: all projects by name and the user's selected one (0 = all projects).</summary>
public sealed record ProjectMenu(IReadOnlyList<ProjectDto> Projects, int SelectedProjectId);

/// <summary>
/// The user's selected project (<c>UserSettings.SelectedProjectId</c>, shared with the legacy app). Read-only: the legacy
/// <c>GetUserSelectedProjectIdAsync</c> also created missing settings rows and reset a selection that pointed at a deleted
/// project; here a missing row or a deleted project reads as 0 and nothing is written (expected difference S3, part).
/// </summary>
public interface IProjectSelection
{
    Task<int> GetSelectedProjectIdAsync(string? userId, CancellationToken ct);

    /// <summary>Null when there are fewer than two projects (the legacy menu is not rendered then).</summary>
    Task<ProjectMenu?> GetMenuAsync(string? userId, CancellationToken ct);

    /// <summary>More than one project: lists then show each ticket's project.</summary>
    Task<bool> HasSeveralProjectsAsync(CancellationToken ct);

    /// <summary>Legacy <c>ProjectSettingsController.Index</c>: every project with its ticket count (table order = id).</summary>
    Task<IReadOnlyList<ProjectWithTicketCount>> ListWithTicketCountsAsync(CancellationToken ct);

    /// <summary>
    /// Legacy <c>ChangeProject</c>: stores the selection when the project exists or is 0 (all). A user without a settings
    /// row gets one with <paramref name="defaultListSettingsJson"/>, as the legacy app created it. False when nothing changed.
    /// </summary>
    Task<bool> SelectProjectAsync(string userId, int projectId, string defaultListSettingsJson, CancellationToken ct);
}

internal sealed class ProjectSelection(TicketDeskDbContext db) : IProjectSelection
{
    public async Task<int> GetSelectedProjectIdAsync(string? userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return 0;
        var selected = await db.UserSettings.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.SelectedProjectId).FirstOrDefaultAsync(ct) ?? 0;
        if (selected != 0 && !await db.Projects.AnyAsync(p => p.ProjectId == selected, ct)) return 0;
        return selected;
    }

    public async Task<bool> HasSeveralProjectsAsync(CancellationToken ct) => await db.Projects.CountAsync(ct) > 1;

    public async Task<IReadOnlyList<ProjectWithTicketCount>> ListWithTicketCountsAsync(CancellationToken ct) =>
        await db.Projects.AsNoTracking().OrderBy(p => p.ProjectId)
            .Select(p => new ProjectWithTicketCount(p.ProjectId, p.ProjectName, p.ProjectDescription, db.Tickets.Count(t => t.ProjectId == p.ProjectId)))
            .ToListAsync(ct);

    public async Task<bool> SelectProjectAsync(string userId, int projectId, string defaultListSettingsJson, CancellationToken ct)
    {
        if (projectId != 0 && !await db.Projects.AnyAsync(p => p.ProjectId == projectId, ct)) return false;
        var row = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (row == null)
        {
            row = new Data.Entities.UserSetting { UserId = userId, ListSettingsJson = defaultListSettingsJson };
            db.UserSettings.Add(row);
        }
        row.SelectedProjectId = projectId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProjectMenu?> GetMenuAsync(string? userId, CancellationToken ct)
    {
        // legacy: Context.Projects.OrderBy(p => p.ProjectName), ordered by SQL Server (collation order, not ordinal)
        var projects = await db.Projects.AsNoTracking().OrderBy(p => p.ProjectName)
            .Select(p => new ProjectDto(p.ProjectId, p.ProjectName, p.ProjectDescription)).ToListAsync(ct);
        if (projects.Count < 2) return null;
        var selected = await GetSelectedProjectIdAsync(userId, ct);
        return new ProjectMenu(projects, selected);
    }
}

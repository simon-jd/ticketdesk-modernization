using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;

namespace TicketDesk.Projects;

/// <summary>Wave 5: legacy <c>ProjectSettingsController</c> writes (create, rename/describe, delete with ticket reassignment).</summary>
public interface IProjectAdmin
{
    Task<ProjectDto?> FindAsync(int projectId, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    /// <summary>The other projects by id (the delete dialog's "reassign tickets to" list).</summary>
    Task<IReadOnlyList<ProjectDto>> OthersAsync(int projectId, CancellationToken ct);
    Task<int> CreateAsync(string name, string? description, CancellationToken ct);
    /// <summary>Only name and description (the legacy TryUpdateModel took any posted entity field).</summary>
    Task<bool> UpdateAsync(int projectId, string name, string? description, CancellationToken ct);
    /// <summary>Moves the project's tickets to <paramref name="reassignTo"/> and deletes it, in one transaction. False when not allowed.</summary>
    Task<bool> DeleteAsync(int projectId, int reassignTo, CancellationToken ct);
}

internal sealed class ProjectAdmin(TicketDeskDbContext db) : IProjectAdmin
{
    public Task<ProjectDto?> FindAsync(int projectId, CancellationToken ct) =>
        db.Projects.AsNoTracking().Where(p => p.ProjectId == projectId).Select(p => new ProjectDto(p.ProjectId, p.ProjectName, p.ProjectDescription)).FirstOrDefaultAsync(ct);

    public Task<int> CountAsync(CancellationToken ct) => db.Projects.CountAsync(ct);

    public async Task<IReadOnlyList<ProjectDto>> OthersAsync(int projectId, CancellationToken ct) =>
        await db.Projects.AsNoTracking().Where(p => p.ProjectId != projectId).OrderBy(p => p.ProjectId)
            .Select(p => new ProjectDto(p.ProjectId, p.ProjectName, p.ProjectDescription)).ToListAsync(ct);

    public async Task<int> CreateAsync(string name, string? description, CancellationToken ct)
    {
        var project = new Project { ProjectName = name, ProjectDescription = description };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return project.ProjectId;
    }

    public async Task<bool> UpdateAsync(int projectId, string name, string? description, CancellationToken ct) =>
        await db.Projects.Where(p => p.ProjectId == projectId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProjectName, name).SetProperty(p => p.ProjectDescription, description), ct) > 0;

    public async Task<bool> DeleteAsync(int projectId, int reassignTo, CancellationToken ct)
    {
        if (projectId == reassignTo || await db.Projects.CountAsync(ct) < 2) return false;
        if (!await db.Projects.AnyAsync(p => p.ProjectId == reassignTo, ct)) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Tickets.Where(t => t.ProjectId == projectId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ProjectId, reassignTo), ct);
        var deleted = await db.Projects.Where(p => p.ProjectId == projectId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        return deleted > 0;
    }
}

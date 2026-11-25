using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;
using DomainStrings = TicketDesk.Localization.Domain.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>Wave 5: legacy <c>ProjectSettingsController</c> new / edit / delete (the list moved in wave 2). Posts validate the antiforgery token (S3).</summary>
[Authorize(Policy = TicketDeskPolicies.Administrator)]
[Route("admin/project")]
public sealed class ProjectSettingsController(IProjectAdmin projects) : LegacyPageController
{
    public static LegacyField ProjectIdField => new("ProjectId", "ProjectId") { Required = true, Number = true };
    public static LegacyField NameField => new("ProjectName", DomainStrings.Project_Name) { Required = true, MaxLength = 100 };
    public static LegacyField DescriptionField => new("ProjectDescription", DomainStrings.Project_Description) { MaxLength = 500 };

    public sealed record EditModel(int ProjectId, string? ProjectName, string? ProjectDescription, bool EnableDelete, IReadOnlyList<ProjectDto> ReassignTo);

    [HttpGet("new")]
    [MigratedRoute("admin.project-new.get", LegacyTokenPage = true)]
    public IActionResult New() => View();

    [HttpPost("new")]
    [MigratedRoute("admin.project-new.post", LegacyTokenPage = true)]
    public async Task<IActionResult> NewPost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        if (LegacyForm.Validate(ModelState, form, NameField, DescriptionField))
        {
            await projects.CreateAsync(form["ProjectName"].ToString(), form["ProjectDescription"].FirstOrDefault(), HttpContext.RequestAborted);
            return new LegacyRedirectResult("/admin/projects");
        }
        ModelState.AddModelError("", ControllerStrings.UnableToCreateProject);
        return View("New");
    }

    private async Task<EditModel> ModelFor(int projectId, string? name, string? description) =>
        new(projectId, name, description, await projects.CountAsync(HttpContext.RequestAborted) > 1, await projects.OthersAsync(projectId, HttpContext.RequestAborted));

    [HttpGet("{projectId:int}")]
    [MigratedRoute("admin.project-edit.get", LegacyTokenPage = true)]
    public async Task<IActionResult> Edit(int projectId)
    {
        var project = await projects.FindAsync(projectId, HttpContext.RequestAborted);
        if (project == null) return new LegacyRedirectResult("/admin/projects");
        return View("Edit", await ModelFor(projectId, project.ProjectName, project.ProjectDescription));
    }

    [HttpPost("{projectId:int}")]
    [MigratedRoute("admin.project-edit.post", LegacyTokenPage = true)]
    public async Task<IActionResult> EditPost(int projectId)
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var name = form["ProjectName"].FirstOrDefault();
        var description = form["ProjectDescription"].FirstOrDefault();
        if (LegacyForm.Validate(ModelState, form, NameField, DescriptionField)
            && await projects.UpdateAsync(projectId, name!, description, HttpContext.RequestAborted))
            return new LegacyRedirectResult("/admin/projects");
        return View("Edit", await ModelFor(projectId, name, description));
    }

    /// <summary>
    /// Like the legacy action, every outcome redirects to the edit page (its success path forgot the return), which sends a
    /// deleted project on to the list.
    /// </summary>
    [HttpPost("delete/{projectId:int}")]
    [MigratedRoute("admin.project-delete")]
    public async Task<IActionResult> Delete(int projectId)
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        if (await projects.FindAsync(projectId, HttpContext.RequestAborted) == null) return new LegacyRedirectResult("/admin/projects");
        if (int.TryParse(form["ReassignTo"].FirstOrDefault(), out var reassignTo))
            await projects.DeleteAsync(projectId, reassignTo, HttpContext.RequestAborted);
        return new LegacyRedirectResult($"/admin/project/{projectId}");
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Settings;
using TicketDesk.Users;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Controllers;

/// <summary>
/// Wave 5: legacy <c>ApplicationSettingsController</c> (GET form of wave 2 and its POST). Only the fields of the form are
/// taken (the legacy TryUpdateModel bound the whole settings object); the POST validates the antiforgery token (S3).
/// </summary>
[Authorize(Policy = TicketDeskPolicies.Administrator)]
[Route("admin/application-settings")]
public sealed class ApplicationSettingsController(IApplicationSettingsStore store, IUserAccounts accounts) : LegacyPageController
{
    public sealed record Page(TicketDeskSettings Settings, IReadOnlyList<RoleInfo> Roles);

    [HttpGet("")]
    [HttpGet("index")]
    [MigratedRoute("admin.application-settings.get", LegacyTokenPage = true)]
    public async Task<IActionResult> Index() =>
        View("Index", new Page(await store.GetAsync(HttpContext.RequestAborted), await accounts.GetRolesAsync(HttpContext.RequestAborted)));

    [HttpPost("")]
    [HttpPost("index")]
    [MigratedRoute("admin.application-settings.post", LegacyTokenPage = true)]
    public async Task<IActionResult> IndexPost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var s = await store.GetAsync(HttpContext.RequestAborted);
        static bool Flag(IFormCollection f, string name) => f[name].Contains("true");
        static List<string> Commas(IFormCollection f, string name) =>
            (f[name].FirstOrDefault() ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        s.Permissions.AllowInternalUsersToEditPriority = Flag(form, "Permissions.AllowInternalUsersToEditPriority");
        s.Permissions.AllowInternalUsersToEditTags = Flag(form, "Permissions.AllowInternalUsersToEditTags");
        s.Permissions.AllowInternalUsersToSetOwner = Flag(form, "Permissions.AllowInternalUsersToSetOwner");
        s.Permissions.AllowInternalUsersToSetAssigned = Flag(form, "Permissions.AllowInternalUsersToSetAssigned");
        s.SelectLists.CategoryList = Commas(form, "categories");
        s.SelectLists.PriorityList = Commas(form, "priorities");
        s.SelectLists.TicketTypesList = Commas(form, "tickettypes");
        var roles = await accounts.GetRolesAsync(HttpContext.RequestAborted);
        s.SecuritySettings.DefaultNewUserRoles = form["defaultroles"].Where(r => roles.Any(x => x.Name == r)).Select(r => r!).ToList();
        s.SecuritySettings.DefaultLogonDomain = form["SecuritySettings.DefaultLogonDomain"].FirstOrDefault() is { Length: > 0 } d ? d : null;
        if (form["DefaultTextEditorType"].FirstOrDefault() is "summernote" or "pagedown")
            s.ClientSettings.Settings["DefaultTextEditorType"] = form["DefaultTextEditorType"].ToString();
        await store.SaveAsync(s, HttpContext.RequestAborted);
        return View("Index", new Page(s, roles));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Settings;
using TicketDesk.Users;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;
using ModelStrings = TicketDesk.Localization.Models.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>
/// Wave 5: legacy <c>UserAdministrationController</c> new / edit / delete (the list pages moved in wave 2, AdminController).
/// All posts validate the antiforgery token (S3; the legacy edit post did not and the delete form had none).
/// </summary>
[Authorize(Policy = TicketDeskPolicies.Administrator)]
[Route("admin")]
public sealed class UserAdministrationController(
    UserManager<TicketDeskUser> users,
    IUserAccounts accounts,
    IApplicationSettingsStore settings,
    IOptionsSnapshot<SiteOptions> site) : LegacyPageController
{
    public static LegacyField UserNameField => new("User.UserName", "UserName");
    public static LegacyField EmailField => new("User.Email", ModelStrings.Email);
    public static LegacyField DisplayNameField => new("User.DisplayName", ModelStrings.DisplayName) { Required = true, MaxLength = 100 };
    public static LegacyField IsLockedField => new("IsLocked", ModelStrings.Locked) { Required = true };
    public static LegacyField IsDisabledField => new("IsDisabled", ModelStrings.Disabled) { Required = true };

    public sealed record EditModel(string Id, string? UserName, string? Email, string? DisplayName, bool IsLocked, bool IsDisabled,
        IReadOnlyList<string> RoleIds, IReadOnlyList<RoleInfo> AllRoles, string PendingRoleId);

    private async Task<EditModel> ModelFor(TicketDeskUser user)
    {
        var roles = await accounts.GetRolesAsync(HttpContext.RequestAborted);
        var roleIds = roles.Where(r => user.Roles.Any(ur => ur.RoleId == r.Id)).Select(r => r.Id).ToList();
        if (roleIds.Count == 0)
        {
            var names = await users.GetRolesAsync(user);
            roleIds = roles.Where(r => names.Contains(r.Name, StringComparer.OrdinalIgnoreCase)).Select(r => r.Id).ToList();
        }
        var end = user.LockoutEndDateUtc;
        var locked = end != null && end > DateTime.UtcNow && user.LockoutEnabled;
        var disabled = locked && end >= SqlMaxDateTime;
        return new EditModel(user.Id, user.UserName, user.Email, user.DisplayName, locked, disabled, roleIds, roles,
            roles.FirstOrDefault(r => r.Name == TicketDeskRoles.PendingUsers)?.Id ?? "");
    }

    /// <summary>DateTime.MaxValue (how the legacy page disabled a user) as a SQL datetime stores it.</summary>
    private static readonly DateTime SqlMaxDateTime = new(9999, 12, 31, 23, 59, 59, 997, DateTimeKind.Utc);

    [HttpGet("user/{id?}")]
    [MigratedRoute("admin.user-edit.get", LegacyTokenPage = true)]
    public async Task<IActionResult> Edit(string? id)
    {
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null) return new LegacyRedirectResult("/admin/users");
        return View(await ModelFor(user));
    }

    [HttpPost("user/{id?}")]
    [MigratedRoute("admin.user-edit.post", LegacyTokenPage = true)]
    public async Task<IActionResult> EditPost(string? id)
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var userId = form["User.Id"].FirstOrDefault() ?? id;
        var user = userId == null ? null : await users.FindByIdAsync(userId);
        if (user == null) return new LegacyRedirectResult("/admin/users");
        if (!LegacyForm.Validate(ModelState, form, DisplayNameField)) return View("Edit", Posted(await ModelFor(user), form));
        var email = form["User.Email"].FirstOrDefault();
        if (site.Value.DemoModeEnabled && (email ?? "").EndsWith("@example.com", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Profile", ControllerStrings.UnableToChangeDemoUser);
            return View("Edit", await ModelFor(user));
        }
        var isLocked = form["IsLocked"].Contains("true");
        var isDisabled = form["IsDisabled"].Contains("true");
        var roleIds = form["Roles"].Where(r => !string.IsNullOrEmpty(r)).Select(r => r!).ToList();
        if (await SetLockout(user, isLocked, isDisabled) && await UpdateUserInfo(user, form) && await SetRoles(user, roleIds))
            return new LegacyRedirectResult("/admin/users");
        return View("Edit", await ModelFor((await users.FindByIdAsync(user.Id))!));
    }

    private static EditModel Posted(EditModel m, IFormCollection form) => m with
    {
        UserName = form["User.UserName"].FirstOrDefault(),
        Email = form["User.Email"].FirstOrDefault(),
        DisplayName = form["User.DisplayName"].FirstOrDefault(),
    };

    private async Task<bool> SetLockout(TicketDeskUser user, bool isLocked, bool isDisabled)
    {
        var currentlyLocked = await users.IsLockedOutAsync(user);
        var currentlyDisabled = currentlyLocked && user.LockoutEndDateUtc >= SqlMaxDateTime;
        if (currentlyDisabled == isDisabled && currentlyLocked == isLocked) return true;
        var end = isDisabled ? DateTimeOffset.MaxValue
            : isLocked ? DateTimeOffset.UtcNow.Add(users.Options.Lockout.DefaultLockoutTimeSpan)
            : DateTimeOffset.MinValue;
        // legacy: SetLockoutEndDateAsync on a user whose lockout is enabled (true for every account the app created)
        var result = await users.SetLockoutEndDateAsync(user, end == DateTimeOffset.MinValue ? null : end);
        return AddErrors(result);
    }

    private async Task<bool> UpdateUserInfo(TicketDeskUser user, IFormCollection form)
    {
        user.UserName = form["User.UserName"].FirstOrDefault() ?? "";
        user.Email = form["User.Email"].FirstOrDefault();
        user.DisplayName = form["User.DisplayName"].FirstOrDefault() ?? "";
        return AddErrors(await users.UpdateAsync(user));
    }

    private async Task<bool> SetRoles(TicketDeskUser user, List<string> roleIds)
    {
        var roles = await accounts.GetRolesAsync(HttpContext.RequestAborted);
        var pending = roles.FirstOrDefault(r => r.Name == TicketDeskRoles.PendingUsers);
        // legacy: a user with any other role is no longer pending
        if (pending != null && roleIds.Any(r => r != pending.Id)) roleIds.Remove(pending.Id);
        var current = await users.GetRolesAsync(user);
        var wanted = roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToList();
        var remove = current.Where(n => !wanted.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        // legacy self-demotion guard (it compared "tdAdministrators", which matched only under a case-insensitive collation)
        if (users.GetUserId(User) == user.Id) remove.RemoveAll(n => n.Equals(TicketDeskRoles.Administrators, StringComparison.OrdinalIgnoreCase));
        var add = wanted.Where(n => !current.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        if (remove.Count > 0 && !AddErrors(await users.RemoveFromRolesAsync(user, remove))) return false;
        if (add.Count > 0 && !AddErrors(await users.AddToRolesAsync(user, add))) return false;
        return true;
    }

    private bool AddErrors(IdentityResult result)
    {
        foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
        return result.Succeeded;
    }

    [HttpPost("user/delete/{id?}")]
    [MigratedRoute("admin.user-delete")]
    public async Task<IActionResult> Delete(string? id)
    {
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null) return new LegacyRedirectResult("/admin/users");
        // legacy: the current user cannot be deleted (the error was lost in the redirect, as here)
        if (users.GetUserId(User) != user.Id && (await users.DeleteAsync(user)).Succeeded) return new LegacyRedirectResult("/admin/users");
        return new LegacyRedirectResult("/admin/user/" + Uri.EscapeDataString(id!));
    }

    [HttpGet("new")]
    [MigratedRoute("admin.user-new.get", LegacyTokenPage = true)]
    public IActionResult New() => View();

    [HttpPost("new")]
    [MigratedRoute("admin.user-new.post", LegacyTokenPage = true)]
    public async Task<IActionResult> NewPost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        if (!LegacyForm.Validate(ModelState, form, UserController.RegisterFields)) return View("New");
        var (user, errors) = await UserController.CreateUserAsync(users, settings, accounts, form, HttpContext.RequestAborted);
        if (user == null)
        {
            foreach (var e in errors) ModelState.AddModelError("", e);
            return View("New");
        }
        return new LegacyRedirectResult("/admin/users");
    }
}

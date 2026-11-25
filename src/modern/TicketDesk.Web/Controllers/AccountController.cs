using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Users;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;
using ModelStrings = TicketDesk.Localization.Models.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>
/// Wave 5 (and the two GET forms of wave 2): legacy <c>AccountController</c> change-password and edit-profile, any signed-in
/// user. The posts validate the new app's antiforgery token (S3: the legacy actions did not validate theirs). Passwords
/// are hashed in the Identity V2 format the legacy app verifies.
/// </summary>
[Authorize]
[Route("account")]
public sealed class AccountController(
    UserManager<TicketDeskUser> users,
    SignInManager<TicketDeskUser> signIn,
    IUserAccounts accounts,
    IOptionsSnapshot<SiteOptions> site) : LegacyPageController
{
    public static LegacyField[] PasswordFields =>
    [
        new("OldPassword", ModelStrings.CurrentPassword) { Required = true },
        new("NewPassword", ModelStrings.NewPassword) { Required = true, MaxLength = 100, MinLength = 6 },
        new("ConfirmPassword", ModelStrings.ConfirmNewPassword) { EqualTo = ("NewPassword", ModelStrings.NewConfirmationDoNotMatch) },
    ];

    /// <summary>AccountProfileViewModel without UserName (legacy L5: it was [Required] but not on the form, so no save ever succeeded).</summary>
    public static LegacyField[] ProfileFields =>
    [
        new("Email", ModelStrings.Email) { Required = true, Email = true },
        new("DisplayName", ModelStrings.DisplayName) { Required = true, MaxLength = 100 },
    ];

    public sealed record Profile(string? Email, string? DisplayName);

    [HttpGet("change-password")]
    [MigratedRoute("account.change-password.get", LegacyTokenPage = true)]
    public IActionResult ChangePassword() => View();

    [HttpPost("change-password")]
    [MigratedRoute("account.change-password.post", LegacyTokenPage = true)]
    public async Task<IActionResult> ChangePasswordPost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        if (!LegacyForm.Validate(ModelState, form, PasswordFields)) return View("ChangePassword");
        if (site.Value.DemoModeEnabled)
        {
            ModelState.AddModelError("Password", ControllerStrings.UnableToChangeDemoUser);
            return View("ChangePassword");
        }
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var result = await users.ChangePasswordAsync(user, form["OldPassword"].ToString(), form["NewPassword"].ToString());
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View("ChangePassword");
        }
        await signIn.RefreshSignInAsync(user); // new security stamp in the cookie (legacy: SignIn with a fresh identity)
        return new LegacyRedirectResult("/account/manage?Message=ChangePasswordSuccess");
    }

    [HttpGet("edit-profile")]
    [MigratedRoute("account.edit-profile.get", LegacyTokenPage = true)]
    public async Task<IActionResult> EditProfile()
    {
        var user = await users.GetUserAsync(User);
        return View(new Profile(user?.Email, user?.DisplayName));
    }

    [HttpPost("edit-profile")]
    [MigratedRoute("account.edit-profile.post", LegacyTokenPage = true)]
    public async Task<IActionResult> EditProfilePost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var posted = new Profile(form["Email"].FirstOrDefault(), form["DisplayName"].FirstOrDefault());
        if (!LegacyForm.Validate(ModelState, form, ProfileFields)) return View("EditProfile", posted);
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var oldEmail = user.Email;
        if (site.Value.DemoModeEnabled && (oldEmail ?? "").EndsWith("@example.com", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Email", ControllerStrings.UnableToChangeDemoUser);
            return View("EditProfile", posted);
        }
        user.Email = posted.Email;
        user.DisplayName = posted.DisplayName ?? "";
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View("EditProfile", posted);
        }
        await accounts.ResetEmailDestinationAsync(user.Id, oldEmail, user.Email, user.DisplayName, HttpContext.RequestAborted);
        await signIn.RefreshSignInAsync(user); // the cookie carries the display name (GivenName)
        return new LegacyRedirectResult("/account/manage?Message=ProfileSaveSuccess");
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Controllers;

/// <summary>Model of the page a retired legacy route answers with (410 Gone).</summary>
public sealed record RetiredPage(string Title, string Text, string LinkText, string LinkUrl);

/// <summary>
/// The 9 routes.csv rows of wave <c>retire</c>, dropped with the legacy app instead of migrated (expected differences X1,
/// X2, S2; replacements in <see cref="RetiredRoutes"/>). The new app answers their URLs itself, so a bookmark gets a clear
/// answer instead of an anonymous 404: <b>410 Gone</b> with a pointer in the legacy layout for first-run setup and the demo-data
/// actions, a plain <b>404</b> for the e-mail test page (S2: it disclosed any ticket to anonymous users). Nothing runs; the
/// actions take no antiforgery token because they change nothing, so the setup POST gets the same 410 as the GETs.
/// </summary>
public sealed class RetiredController : LegacyPageController
{
    private static readonly RetiredPage Setup = new("First-run setup",
        "First-run setup was part of the old TicketDesk application and is retired with it. This installation is set up: the " +
        "database schema is applied by the deployment (td-db migrate), the first administrator of a new, empty database is " +
        "created by the operator with td-db create-admin, and administrators manage accounts under Admin, Users.",
        "Back to TicketDesk", "/");

    private static readonly RetiredPage DemoData = new("Demo data",
        "Creating and removing demo data is no longer done from the web application. Demo data for a lab or test installation " +
        "is loaded and removed by the operator with the td-seed tool (td-seed run, td-seed reset).",
        "Demo data page", "/admin/data-management/demo");

    // ---- X1: first-run setup (setup.child-actions had no URL of their own: nothing to answer)

    [Route("first-run-setup")]
    [Route("first-run-setup/index")]
    [MigratedRoute("setup.index")]
    [IgnoreAntiforgeryToken]
    public IActionResult SetupIndex() => Gone(Setup);

    [Route("first-run-setup/legacy-migration")]
    [MigratedRoute("setup.legacy-migration")]
    [IgnoreAntiforgeryToken]
    public IActionResult SetupLegacyMigration() => Gone(Setup);

    [Route("first-run-setup/upgrade-database")]
    [MigratedRoute("setup.upgrade-database")]
    [IgnoreAntiforgeryToken]
    public IActionResult SetupUpgradeDatabase() => Gone(Setup);

    [Route("first-run-setup/check-upgrade-progress")]
    [MigratedRoute("setup.check-upgrade-progress")]
    [IgnoreAntiforgeryToken]
    public IActionResult SetupCheckUpgradeProgress() => Gone(Setup);

    [HttpPost("first-run-setup/create-database")]
    [MigratedRoute("setup.create-database")]
    [IgnoreAntiforgeryToken]
    public IActionResult SetupCreateDatabase() => Gone(Setup);

    // ---- X2: demo data (administrators, as the legacy actions; the demo page itself is admin.demo in AdminController)

    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/data-management/create-demo-data")]
    [MigratedRoute("admin.demo.create")]
    [IgnoreAntiforgeryToken]
    public IActionResult CreateDemoData() => Gone(DemoData);

    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/data-management/remove-demo-data")]
    [MigratedRoute("admin.demo.remove")]
    [IgnoreAntiforgeryToken]
    public IActionResult RemoveDemoData() => Gone(DemoData);

    // ---- S2: the anonymous e-mail test page

    [Route("emailtest/index/{id}")]
    [MigratedRoute("emailtest.index")]
    [IgnoreAntiforgeryToken]
    public IActionResult EmailTest() => NotFound();

    private ViewResult Gone(RetiredPage page)
    {
        Response.StatusCode = StatusCodes.Status410Gone;
        return View("~/Views/Retired/Gone.cshtml", page);
    }
}

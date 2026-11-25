namespace TicketDesk.Replay;

/// <summary>Builders for steps (keeps journey definitions short).</summary>
public static class Steps
{
    public static StepSpec Get(string name, string path, bool ajax = false, int? expect = null, params ExtractSpec[] extract) =>
        new() { Name = name, Method = "GET", Path = path, Ajax = ajax, ExpectStatus = expect, Extract = [.. extract] };

    public static StepSpec Post(string name, string path, IEnumerable<FieldSpec> form, bool ajax = false, int? expect = null, FileSpec? file = null, params ExtractSpec[] extract) =>
        new() { Name = name, Method = "POST", Path = path, Form = [.. form], Ajax = ajax, ExpectStatus = expect, File = file, Extract = [.. extract] };

    public static StepSpec Submit(string name, string formSelector, IEnumerable<FieldSpec> overrides, string? fromStep = null, bool ajax = false,
        int? expect = null, bool follow = true, params ExtractSpec[] extract) =>
        new()
        {
            Name = name, Kind = StepKinds.Form, Method = "POST", FormSelector = formSelector, FromStep = fromStep, Form = [.. overrides],
            Ajax = ajax, ExpectStatus = expect, FollowRedirects = follow, Extract = [.. extract],
        };

    public static FieldSpec F(string name, string value) => new(name, value);
}

/// <summary>
/// Journeys for legacy TicketDesk 2.5 (routes from docs/migration/routes.csv). They sign in with
/// {secret:TD_ADMIN_USER} / {secret:TD_ADMIN_PASSWORD} (and one seeded internal user with {secret:TD_SEED_USER_PASSWORD});
/// titles carry no run-specific text so two runs on the same baseline database produce comparable responses.
/// They are written for the baseline data set (legacy DB seeded at scale S with seed 42, ops/onprem/baseline/baseline-s.sh)
/// and run in the order of <see cref="All"/>: read-only journeys (search, lists) come before the ones that create tickets,
/// because the legacy app indexes new tickets in a background work item and lists show the newest tickets first.
/// </summary>
public static class TicketDeskJourneys
{
    private const string Admin = "{secret:TD_ADMIN_USER}";
    private const string AdminPassword = "{secret:TD_ADMIN_PASSWORD}";

    /// <summary>A seeded user that is only in TdInternalUsers (td-seed scale S, seed 42).</summary>
    public const string InternalUser = "ali.chen.00238@seed.ticketdesk.test";
    private const string SeedPassword = "{secret:TD_SEED_USER_PASSWORD}";

    public static IReadOnlyList<JourneySpec> All { get; } =
    [
        Anonymous(), SignIn(), Search(), TicketList(), TicketCreateView(), TicketEdit(), TicketComment(), TicketAssign(), TicketClose(),
        TicketWorkflow(), Attachments(), AdminSettings(), AdminChanges(), InternalUserJourney(), RegisterWhileDisabled(),
    ];

    public static IEnumerable<JourneySpec> Select(string? names)
    {
        if (string.IsNullOrWhiteSpace(names) || names == "all") return All;
        var wanted = names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = wanted.Except(All.Select(j => j.Name), StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0) throw new ArgumentException($"unknown journey(s): {string.Join(", ", unknown)}; known: {string.Join(", ", All.Select(j => j.Name))}");
        return All.Where(j => wanted.Contains(j.Name));
    }

    private static List<StepSpec> SignInSteps(string user = Admin, string password = AdminPassword) =>
    [
        Steps.Get("sign-in page", "/user/sign-in", expect: 200),
        Steps.Submit("sign-in submit", "form[action*='sign-in']", [Steps.F("UserNameOrEmail", user), Steps.F("Password", password)], expect: 302),
    ];

    private static List<StepSpec> CreateTicketSteps(string title, string tags = "replay,printer") =>
    [
        Steps.Get("new ticket page", "/ticket/new", expect: 200),
        Steps.Submit("create ticket", "form[action$='/ticket/new']",
        [
            Steps.F("ProjectId", FormReader.FirstOption), // 5 projects in the baseline data: the form makes the user pick one
            Steps.F("Title", title),
            Steps.F("Details", "The shared printer on the third floor shows *offline* since this morning.\n\nSteps tried: power cycle."),
            Steps.F("TicketType", FormReader.FirstOption),
            Steps.F("Category", FormReader.FirstOption),
            Steps.F("Priority", FormReader.FirstOption),
            Steps.F("TagList", tags),
        ], fromStep: "new ticket page", expect: 302, extract: new ExtractSpec("ticketId", "location", @"/ticket/(\d+)")),
    ];

    private static List<StepSpec> TicketPanels() =>
    [
        Steps.Get("ticket details panel", "/ticket/ticket-details?ticketId={ticketId}", ajax: true),
        Steps.Get("ticket events panel", "/ticket/ticket-events?ticketId={ticketId}", ajax: true),
        Steps.Get("ticket files panel", "/ticket/ticket-files?ticketId={ticketId}", ajax: true),
        Steps.Get("activity buttons", "/ticket-activity/activity-buttons?ticketId={ticketId}", ajax: true),
    ];

    /// <summary>Loads an activity form (AJAX partial) and submits it. <paramref name="n"/> keeps step names unique when an activity repeats.</summary>
    private static List<StepSpec> Activity(string activity, string route, int n, params FieldSpec[] overrides)
    {
        var suffix = n > 1 ? $" ({n})" : "";
        return
        [
            Steps.Get($"load {activity} form{suffix}", $"/ticket-activity/load-activity?ticketId={{ticketId}}&activity={activity}", ajax: true, expect: 200),
            Steps.Submit($"submit {activity}{suffix}", $"form[action*='/ticket-activity/{route}']", overrides, ajax: true, expect: 200),
        ];
    }

    private static List<StepSpec> Activity(string activity, string route, params FieldSpec[] overrides) => Activity(activity, route, 1, overrides);

    public static JourneySpec Anonymous() => new()
    {
        Name = "anonymous",
        Description = "Public pages, auth redirects, the disabled first-run wizard, the e-mail test page (S2), bundles and the culture switch.",
        Steps =
        [
            Steps.Get("home", "/"),
            Steps.Get("home by name", "/index"),
            Steps.Get("about", "/about"),
            Steps.Get("ticket center requires sign-in", "/tickets"),
            Steps.Get("ticket requires sign-in", "/ticket/1"),
            Steps.Get("admin requires sign-in", "/admin/users"),
            Steps.Get("sign-in page", "/user/sign-in"),
            Steps.Get("register page (disabled by default)", "/user/register"),
            Steps.Get("access denied page", "/access-denied"),
            Steps.Get("first-run setup (disabled)", "/first-run-setup"),
            Steps.Get("upgrade database (disabled)", "/first-run-setup/upgrade-database"),
            Steps.Get("upgrade progress (disabled)", "/first-run-setup/check-upgrade-progress"),
            Steps.Get("legacy migration (disabled)", "/first-run-setup/legacy-migration"),
            Steps.Get("e-mail test page renders a ticket anonymously (S2)", "/emailtest/index/1"),
            Steps.Get("style bundle", "/content/styles"),
            Steps.Get("common script bundle", "/bundles/common"),
            Steps.Get("switch culture to pt-BR", "/language?name=pt-BR"),
            Steps.Get("home in pt-BR", "/"),
            Steps.Get("switch culture back to en-US", "/language?name=en-US"),
        ],
    };

    public static JourneySpec SignIn() => new()
    {
        Name = "sign-in",
        Description = "Failed and successful sign-in with return URL, account pages and their forms, sign-out.",
        Steps =
        [
            Steps.Get("sign-in page with return url", "/user/sign-in?ReturnUrl=%2Faccount%2Fmanage", expect: 200),
            Steps.Submit("sign-in with wrong password", "form[action*='sign-in']",
                [Steps.F("UserNameOrEmail", Admin), Steps.F("Password", "not-the-password")], expect: 200),
            Steps.Submit("sign-in", "form[action*='sign-in']", [Steps.F("UserNameOrEmail", Admin), Steps.F("Password", AdminPassword)], expect: 302),
            Steps.Get("account manage", "/account/manage", expect: 200),
            Steps.Get("change password page", "/account/change-password", expect: 200),
            // Only the validation path: a real change would alter the admin credential every other journey uses.
            Steps.Submit("change password with wrong current password", "form[action$='/account/change-password']",
                [Steps.F("OldPassword", "not-the-password"), Steps.F("NewPassword", "Replay-Unused-1!"), Steps.F("ConfirmPassword", "Replay-Unused-1!")]),
            Steps.Get("edit profile page", "/account/edit-profile", expect: 200),
            Steps.Submit("save profile unchanged", "form[action$='/account/edit-profile']", []),
            Steps.Get("sign out", "/user/sign-out"),
            Steps.Get("ticket center after sign-out", "/tickets"),
        ],
    };

    public static JourneySpec Search() => new()
    {
        Name = "search",
        Description = "Quick search (query string and path forms) and an empty search, on the index built for the baseline.",
        Steps =
        [
            .. SignInSteps(),
            Steps.Get("search page empty", "/search"),
            Steps.Get("search by query", "/search?term=printer"),
            Steps.Get("search by path", "/search/network"),
            Steps.Get("search no hits", "/search?term=zzqqxxnohits"),
        ],
    };

    public static JourneySpec TicketList() => new()
    {
        Name = "ticket-list",
        Description = "Ticket center lists, paging, filter and sort (AJAX partials), tag autocomplete, project switch; resets the list settings at the end.",
        Steps =
        [
            .. SignInSteps(),
            Steps.Get("ticket center default list", "/tickets", expect: 200),
            Steps.Get("ticket index redirects to the list", "/ticket"),
            Steps.Get("my tickets", "/tickets/mytickets"),
            Steps.Get("unassigned", "/tickets/unassigned"),
            Steps.Get("assigned to me", "/tickets/assignedToMe"),
            Steps.Get("history", "/tickets/historytickets"),
            Steps.Get("open tickets", "/tickets/opentickets", expect: 200),
            Steps.Get("open tickets page 2", "/tickets/pageList/opentickets/2", ajax: true),
            Steps.Submit("filter open tickets", "#filterForm", [Steps.F("PageSize", "50")], fromStep: "open tickets", ajax: true),
            Steps.Get("sort by title", "/tickets/sortList/opentickets?columnName=Title", ajax: true),
            Steps.Get("sort by title again (flip)", "/tickets/sortList/opentickets?columnName=Title", ajax: true),
            Steps.Get("add priority to sort", "/tickets/sortList/opentickets?columnName=Priority&isMultiSort=true", ajax: true),
            Steps.Get("reset list settings", "/tickets/reset-user-lists"),
            Steps.Get("tag autocomplete", "/auto-complete/tag-list?term=net", ajax: true),
            Steps.Get("switch to project 2", "/navigation/change-project/2"),
            Steps.Get("open tickets in project 2", "/tickets/opentickets"),
            Steps.Get("switch to all projects", "/navigation/change-project/0"),
        ],
    };

    public static JourneySpec TicketCreateView() => new()
    {
        Name = "ticket-create-view",
        Description = "Create a ticket, view it with all AJAX panels, toggle the subscription twice.",
        Steps =
        [
            .. SignInSteps(),
            .. CreateTicketSteps("Replay: third-floor printer offline"),
            Steps.Get("view ticket", "/ticket/{ticketId}", expect: 200),
            .. TicketPanels(),
            Steps.Post("unsubscribe", "/ticket/change-ticket-subscription?ticketId={ticketId}", [], ajax: true),
            Steps.Post("subscribe again", "/ticket/change-ticket-subscription?ticketId={ticketId}", [], ajax: true),
            Steps.Get("unknown ticket redirects to list", "/ticket/999999999"),
        ],
    };

    public static JourneySpec TicketEdit() => new()
    {
        Name = "ticket-edit",
        Description = "Create a ticket and edit its info through the EditTicketInfo activity.",
        Steps =
        [
            .. SignInSteps(),
            .. CreateTicketSteps("Replay: laptop docking station flickers", "replay,laptop"),
            .. Activity("EditTicketInfo", "edit-ticket-info",
                Steps.F("Title", "Replay: laptop docking station flickers (edited)"),
                Steps.F("TagList", "replay,laptop,dock"),
                Steps.F("comment", "Edited by the replay journey.")),
            .. TicketPanels(),
        ],
    };

    public static JourneySpec TicketComment() => new()
    {
        Name = "ticket-comment",
        Description = "Create a ticket and add a comment.",
        Steps =
        [
            .. SignInSteps(),
            .. CreateTicketSteps("Replay: VPN drops every hour", "replay,vpn"),
            .. Activity("AddComment", "add-comment", Steps.F("comment", "Happens on Wi-Fi and wired.")),
            Steps.Get("ticket events panel", "/ticket/ticket-events?ticketId={ticketId}", ajax: true),
        ],
    };

    public static JourneySpec TicketAssign() => new()
    {
        Name = "ticket-assign",
        Description = "Create a ticket and assign it to the first help-desk user in the list.",
        Steps =
        [
            .. SignInSteps(),
            .. CreateTicketSteps("Replay: new hire needs an account", "replay,onboarding"),
            .. Activity("Assign", "assign", Steps.F("AssignedTo", FormReader.FirstOption), Steps.F("comment", "Assigning to help desk.")),
            .. TicketPanels(),
        ],
    };

    public static JourneySpec TicketClose() => new()
    {
        Name = "ticket-close",
        Description = "Create a ticket, take it over, resolve it, close it.",
        Steps =
        [
            .. SignInSteps(),
            .. CreateTicketSteps("Replay: monitor cable missing", "replay,hardware"),
            .. Activity("TakeOver", "take-over", Steps.F("comment", "Taking this one.")),
            .. Activity("Resolve", "resolve", Steps.F("comment", "Cable delivered.")),
            .. Activity("Close", "close", Steps.F("comment", "Confirmed.")),
            Steps.Get("view closed ticket", "/ticket/{ticketId}", expect: 200),
            .. TicketPanels(),
        ],
    };

    public static JourneySpec TicketWorkflow() => new()
    {
        Name = "ticket-workflow",
        Description = "The remaining activities on one ticket: more-info cycle, pass, reassign, give up, force close, reopen.",
        Steps =
        [
            .. SignInSteps(),
            .. CreateTicketSteps("Replay: shared drive permissions", "replay,access"),
            .. Activity("TakeOver", "take-over", Steps.F("comment", "Taking this one.")),
            .. Activity("RequestMoreInfo", "request-more-info", Steps.F("comment", "Which folder exactly?")),
            .. Activity("CancelMoreInfo", "cancel-more-info", Steps.F("comment", "Found it myself.")),
            .. Activity("RequestMoreInfo", "request-more-info", 2, Steps.F("comment", "And which group needs access?")),
            .. Activity("SupplyMoreInfo", "supply-more-info", Steps.F("comment", "The finance group.")), // "reactivate" is checked by default
            .. Activity("Pass", "pass", Steps.F("AssignedTo", FormReader.FirstOption), Steps.F("comment", "Passing to help desk.")),
            .. Activity("ReAssign", "reassign", Steps.F("AssignedTo", FormReader.FirstOption), Steps.F("comment", "Reassigning.")),
            .. Activity("TakeOver", "take-over", 2, Steps.F("comment", "Taking it back.")),
            .. Activity("GiveUp", "give-up", Steps.F("comment", "Giving it up.")),
            .. Activity("Assign", "assign", Steps.F("AssignedTo", FormReader.FirstOption), Steps.F("comment", "Assigning.")),
            .. Activity("TakeOver", "take-over", 3, Steps.F("comment", "Taking it once more.")),
            .. Activity("ForceClose", "force-close", Steps.F("comment", "Closing without the owner.")),
            .. Activity("ReOpen", "reopen", Steps.F("comment", "Reopening.")),
            Steps.Get("view reopened ticket", "/ticket/{ticketId}", expect: 200),
            .. TicketPanels(),
        ],
    };

    public static JourneySpec Attachments() => new()
    {
        Name = "attachments",
        Description = "Upload pending files (one deleted again), create a ticket that commits the other, download it, add a file through ModifyAttachments.",
        Steps =
        [
            .. SignInSteps(),
            Steps.Get("new ticket page", "/ticket/new", expect: 200, extract: new ExtractSpec("tempId", "html", "input[name='tempId']", "value")),
            Steps.Post("upload pending file", "/file/upload?tempId={tempId}", [], ajax: true,
                file: new FileSpec("file", "replay-note.txt", "text/plain", "Replay attachment.\nLine two.\n")),
            Steps.Post("upload a second pending file", "/file/upload?tempId={tempId}", [], ajax: true,
                file: new FileSpec("file", "replay-extra.txt", "text/plain", "Uploaded by mistake.\n")),
            Steps.Post("delete the second pending file", "/file/delete", [Steps.F("id", "{tempId}"), Steps.F("fileName", "replay-extra.txt")], ajax: true),
            Steps.Get("pending attachments info", "/file/get-attachments-info?tempId={tempId}", ajax: true),
            Steps.Submit("create ticket with attachment", "form[action$='/ticket/new']",
            [
                Steps.F("ProjectId", FormReader.FirstOption),
                Steps.F("Title", "Replay: scanned invoice attached"),
                Steps.F("Details", "See the attached note."),
                Steps.F("TicketType", FormReader.FirstOption),
                Steps.F("Category", FormReader.FirstOption),
                Steps.F("Priority", FormReader.FirstOption),
                Steps.F("TagList", "replay,attachment"),
            ], fromStep: "new ticket page", expect: 302, extract: new ExtractSpec("ticketId", "location", @"/ticket/(\d+)")),
            Steps.Get("ticket files panel", "/ticket/ticket-files?ticketId={ticketId}", ajax: true),
            Steps.Get("attached files info", "/file/get-attachments-info?id={ticketId}", ajax: true),
            Steps.Get("download attachment", "/file/{ticketId}/replay-note.txt"),
            Steps.Get("load ModifyAttachments form", "/ticket-activity/load-activity?ticketId={ticketId}&activity=ModifyAttachments", ajax: true, expect: 200,
                extract: new ExtractSpec("modifyTempId", "html", "input[name='tempId']", "value")),
            Steps.Post("upload file for ModifyAttachments", "/file/upload?tempId={modifyTempId}", [], ajax: true,
                file: new FileSpec("file", "replay-added.txt", "text/plain", "Added later.\n")),
            Steps.Submit("submit ModifyAttachments", "form[action*='/ticket-activity/modify-attachments']",
                [Steps.F("comment", "Adding the signed copy.")], fromStep: "load ModifyAttachments form", ajax: true, expect: 200),
            Steps.Get("ticket files panel after modify", "/ticket/ticket-files?ticketId={ticketId}", ajax: true),
        ],
    };

    public static JourneySpec AdminSettings() => new()
    {
        Name = "admin-settings",
        Description = "Admin pages (read-only: nothing is saved).",
        Steps =
        [
            .. SignInSteps(),
            Steps.Get("application settings", "/admin/application-settings", expect: 200),
            Steps.Get("users", "/admin/users", expect: 200, extract: new ExtractSpec("userHref", "html", "a[href*='/admin/user/']", "href")),
            Steps.Get("users page 1 (ajax)", "/admin/users/page/1", ajax: true),
            Steps.Get("edit user", "{userHref}"),
            Steps.Get("new user page", "/admin/new"),
            Steps.Get("projects", "/admin/projects"),
            Steps.Get("new project page", "/admin/project/new"),
            Steps.Get("edit default project", "/admin/project/1"),
            Steps.Get("push notification settings", "/admin/push-notification-settings"),
            Steps.Get("delivery providers (partial)", "/admin/push-notification-settings/list-providers", ajax: true),
            Steps.Get("search manager", "/admin/search-manager"),
            Steps.Get("demo data page", "/admin/data-management/demo"),
        ],
    };

    private const string SmtpProvider =
        "TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider%2C%20TicketDesk.PushNotifications%2C%20Version%3D1.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull";

    public static JourneySpec AdminChanges() => new()
    {
        Name = "admin-changes",
        Description = "Admin forms that save: settings saved unchanged, a project and a user created, edited and deleted again.",
        Steps =
        [
            .. SignInSteps(),
            Steps.Get("application settings", "/admin/application-settings", expect: 200),
            Steps.Submit("save application settings unchanged", "form[action$='/admin/application-settings']", []),
            Steps.Get("push notification settings", "/admin/push-notification-settings", expect: 200),
            Steps.Submit("save push notification settings unchanged", "form[action$='/admin/push-notification-settings']", []),
            Steps.Get("configure SMTP provider", "/admin/push-notification-settings/configure-provider?providerAssemblyQualifiedName=" + SmtpProvider, expect: 200),
            Steps.Submit("save SMTP provider unchanged", "form[action$='/admin/push-notification-settings/configure-provider']", []),
            Steps.Get("new project page", "/admin/project/new", expect: 200),
            Steps.Submit("create project", "form[action$='/admin/project/new']",
                [Steps.F("ProjectName", "Replay Project"), Steps.F("ProjectDescription", "Created by the replay journey.")]),
            Steps.Get("projects", "/admin/projects", expect: 200,
                extract: new ExtractSpec("projectId", "regex", @"href=""/admin/project/(\d+)""[^>]*>\s*Replay Project")),
            Steps.Get("edit replay project", "/admin/project/{projectId}", expect: 200),
            Steps.Submit("save replay project", "form.form-horizontal[action*='/admin/project/']",
                [Steps.F("ProjectDescription", "Edited by the replay journey.")]),
            Steps.Get("edit replay project again", "/admin/project/{projectId}", expect: 200),
            Steps.Submit("delete replay project", "form[action*='/admin/project/delete/']", [Steps.F("ReassignTo", FormReader.FirstOption)]),
            Steps.Get("new user page", "/admin/new", expect: 200),
            Steps.Submit("create user", "form[action$='/admin/new']",
            [
                Steps.F("UserName", "replay.user@example.test"), Steps.F("Email", "replay.user@example.test"),
                // "Aaron" sorts first: the user list is ordered by display name, 25 per page
                Steps.F("Password", SeedPassword), Steps.F("ConfirmPassword", SeedPassword), Steps.F("DisplayName", "Aaron Replay"),
            ]),
            Steps.Get("users after create", "/admin/users", expect: 200,
                extract: new ExtractSpec("newUserHref", "regex", @"href=""(/admin/user/[0-9a-fA-F-]{36})"">\s*Aaron Replay")),
            Steps.Get("edit new user", "{newUserHref}", expect: 200),
            Steps.Submit("save new user", "form[action*='/admin/user/']:not([action*='/delete/'])", [Steps.F("User.DisplayName", "Aaron Replay (edited)")]),
            Steps.Get("edit new user again", "{newUserHref}", expect: 200),
            Steps.Submit("delete new user", "form[action*='/admin/user/delete/']", []),
            // Not here: admin/search-manager/remove-index and populate-index. They race the background indexer that the
            // ticket journeys above start; one of two runs hit a failed Lucene merge, after which a leftover write.lock
            // made every page return 500 (tests/characterization/golden/README.md, "Not covered").
        ],
    };

    public static JourneySpec InternalUserJourney() => new()
    {
        Name = "internal-user",
        Description = "A seeded internal (non-help-desk) user: ticket center, refused admin page, create a ticket and comment on it.",
        Steps =
        [
            .. SignInSteps(InternalUser, SeedPassword),
            Steps.Get("ticket center", "/tickets", expect: 200),
            Steps.Get("my tickets", "/tickets/mytickets"),
            Steps.Get("unassigned (help desk only)", "/tickets/unassigned"),
            Steps.Get("admin page is refused", "/admin/users"),
            .. CreateTicketSteps("Replay: keyboard missing keys", "replay,keyboard"),
            Steps.Get("view own ticket", "/ticket/{ticketId}", expect: 200),
            Steps.Get("activity buttons", "/ticket-activity/activity-buttons?ticketId={ticketId}", ajax: true),
            .. Activity("AddComment", "add-comment", Steps.F("comment", "The E and R keys.")),
            Steps.Get("sign out", "/user/sign-out"),
        ],
    };

    public static JourneySpec RegisterWhileDisabled() => new()
    {
        Name = "register-disabled",
        Description = "S4: registration is disabled (the GET redirects) but the POST still creates and signs in a user.",
        Steps =
        [
            Steps.Get("register page redirects (disabled)", "/user/register"),
            Steps.Get("sign-in page for an antiforgery token", "/user/sign-in", expect: 200,
                extract: new ExtractSpec("af", "html", "form[action*='sign-in'] input[name='__RequestVerificationToken']", "value")),
            Steps.Post("register anyway", "/user/register",
            [
                Steps.F("__RequestVerificationToken", "{af}"), Steps.F("UserName", "replay.register@example.test"),
                Steps.F("Email", "replay.register@example.test"), Steps.F("DisplayName", "Replay Register"),
                Steps.F("Password", SeedPassword), Steps.F("ConfirmPassword", SeedPassword),
            ]),
            Steps.Get("home as the registered user", "/"),
            Steps.Get("ticket center as a pending user", "/tickets"),
        ],
    };
}

public static class Recording
{
    /// <summary>Rebuilds journeys from the step definitions stored in a recording (hop 0 of each step).</summary>
    public static List<JourneySpec> ToJourneys(IEnumerable<RecordedExchange> exchanges) =>
        exchanges.Where(e => e.Hop == 0 && e.Step != null)
            .GroupBy(e => e.Journey)
            .Select(g => new JourneySpec
            {
                Name = g.Key,
                Steps = g.OrderBy(e => e.StepIndex).Select(e => e.Step!).ToList(),
            })
            .ToList();
}

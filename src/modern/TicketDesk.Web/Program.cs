using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Attachments;
using TicketDesk.Data;
using TicketDesk.Identity;
using TicketDesk.Notifications;
using TicketDesk.Projects;
using TicketDesk.Reports;
using TicketDesk.Search;
using TicketDesk.Settings;
using TicketDesk.Tickets;
using TicketDesk.Users;
using TicketDesk.Web.Hosting;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using TicketDesk.Web.Spa;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false); // no server banner (the legacy app sent IIS's)
builder.Configuration.AddMigrationRegistryFile(); // migration-registry.json, watched: switching a page needs no redeploy
builder.AddTicketDeskTls();       // Tls:CertificatePath: HTTPS in Kestrel with a certificate that is re-read when rotated (AKS)
builder.AddTicketDeskTelemetry(); // APPLICATIONINSIGHTS_CONNECTION_STRING: request telemetry to Application Insights (AKS)

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // numbers are numbers: no quoted numbers accepted, so the OpenAPI schemas (and the generated TypeScript client) say integer
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddTicketDeskData();
builder.Services.AddTicketDeskIdentity(builder.Configuration);
builder.Services
    .AddUsersModule()
    .AddAttachmentsModule(builder.Configuration)
    .AddProjectsModule()
    .AddTicketsModule()
    .AddReportsModule()
    .AddSearchModule()
    .AddNotificationsModule(builder.Configuration)
    .AddSettingsModule(builder.Configuration);
builder.Services.AddTicketDeskOpenApi();
builder.Services.AddTicketDeskHealthChecks();

// migrated legacy pages (Razor views over the legacy layout) and the registry that picks the Angular or the server page
builder.Services.AddMigrationRegistry(builder.Configuration);
builder.Services.AddOptions<SiteOptions>().Bind(builder.Configuration.GetSection(SiteOptions.SectionName));
builder.Services.AddSingleton<LegacyBundles>();
builder.Services.AddSingleton<LegacyStaticFiles>();
builder.Services.AddAngularFrontEnd(); // src/web, built into wwwroot/app: pages switched to Angular in the registry
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, LegacyAuthorizationResultHandler>();
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o =>
{
    // not the MVC 5 name (__RequestVerificationToken): a browser may still hold the legacy app's cookie of that name
    o.Cookie.Name = ".TicketDesk.Antiforgery";
    o.HeaderName = "RequestVerificationToken";
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAngularAssets(); // /app/** (the Angular bundle): static files before routing
app.UseRouting();
app.UseLegacyCulture();
// before authentication/authorization: a challenge or a forbid of a migrated page short-circuits there and must still get
// the legacy headers (routing has run, so the endpoint is known here)
app.UseMigratedRouteHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.MapTicketDeskHealthChecks();
app.MapOpenApi(OpenApi.Route);

var api = app.MapGroup("/api").RequireAntiforgeryOnWrites();
api.MapTicketsEndpoints();
api.MapProjectsEndpoints();
api.MapReportsEndpoints();
api.MapMigrationStatus();
api.MapSessionEndpoints();
// unknown API paths: a problem response instead of an empty 404
api.Map("/{**rest}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such API endpoint."))
    .WithOrder(1000)
    .ExcludeFromDescription();

app.MapControllers();
app.MapAngularPages();
app.Services.GetRequiredService<LegacyBundles>().Map(app);
app.Services.GetRequiredService<LegacyStaticFiles>().Map(app);

app.Run();

public partial class Program;

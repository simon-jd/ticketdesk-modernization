using Microsoft.OpenApi;

namespace TicketDesk.Web.Hosting;

public static class OpenApi
{
    public const string DocumentName = "v1";
    public const string Route = "/openapi/{documentName}.json";

    public static IServiceCollection AddTicketDeskOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, o => o.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "TicketDesk API",
                Version = "v1",
                Description = "Endpoints of the new TicketDesk app. Every path not listed here is served by the legacy app " +
                              "through the gateway. Authentication: the new app's Identity cookie.",
            };
            // the checked-in copy (docs/api/openapi.json) must not depend on the host that generated it
            document.Servers?.Clear();
            return Task.CompletedTask;
        }));
}

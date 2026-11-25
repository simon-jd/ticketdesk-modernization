using Microsoft.AspNetCore.Antiforgery;

namespace TicketDesk.Web.Hosting;

/// <summary>
/// Antiforgery for the REST API: every <c>/api</c> endpoint declared for POST, PUT, PATCH or DELETE validates the request token in
/// the <c>RequestVerificationToken</c> header (cookie authentication alone would let another site post with the user's cookie).
/// The Angular front end gets the token from the <c>XSRF-TOKEN</c> cookie its page shell sets; a missing or wrong token is a
/// 400 problem response. Decided per endpoint when the endpoint is built, so read-only endpoints pay nothing.
/// </summary>
public static class ApiAntiforgery
{
    private static readonly string[] StateChanging = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    public static TBuilder RequireAntiforgeryOnWrites<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint =>
        {
            var methods = endpoint.Metadata.OfType<HttpMethodMetadata>().SelectMany(m => m.HttpMethods);
            if (!methods.Any(m => StateChanging.Contains(m, StringComparer.OrdinalIgnoreCase))) return;
            endpoint.FilterFactories.Add((_, next) => async invocation =>
            {
                var http = invocation.HttpContext;
                var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(http))
                    return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing or invalid antiforgery token.",
                        detail: "State-changing API calls send the RequestVerificationToken header (the XSRF-TOKEN cookie's value).");
                return await next(invocation);
            });
        });
        return builder;
    }
}

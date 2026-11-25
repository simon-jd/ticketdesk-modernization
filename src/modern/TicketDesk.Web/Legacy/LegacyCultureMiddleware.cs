using System.Collections.Concurrent;
using System.Globalization;
using TicketDesk.Localization;

namespace TicketDesk.Web.Legacy;

/// <summary>
/// The legacy <c>LocalizationFilter</c>: the <c>_culture</c> cookie (or the default, en-US) sets both the culture and the UI
/// culture of the request. Accept-Language is ignored, as in the legacy app. Only migrated endpoints render anything
/// culture-dependent; proxied requests are unaffected.
/// </summary>
public static class LegacyCultureMiddleware
{
    private static readonly ConcurrentDictionary<string, CultureInfo> Cultures = new(StringComparer.OrdinalIgnoreCase);

    public static IApplicationBuilder UseLegacyCulture(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var culture = Get(LegacyCulture.GetImplementedCulture(context.Request.Cookies[LegacyCulture.CookieName]));
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return next(context);
    });

    /// <summary>
    /// The culture as .NET Framework on Windows (NLS) formatted it. ICU (Linux, recent Windows) differs in a few patterns the
    /// pages show, e.g. en-US puts a narrow no-break space (U+202F) before AM/PM; the legacy pages have a plain space.
    /// </summary>
    public static CultureInfo Get(string name) => Cultures.GetOrAdd(name, n =>
    {
        var c = (CultureInfo)CultureInfo.GetCultureInfo(n).Clone();
        var f = c.DateTimeFormat;
        f.ShortTimePattern = f.ShortTimePattern.Replace(' ', ' ');
        f.LongTimePattern = f.LongTimePattern.Replace(' ', ' ');
        f.FullDateTimePattern = f.FullDateTimePattern.Replace(' ', ' ');
        return CultureInfo.ReadOnly(c);
    });
}

// remote-auth-probe.cs - protocol-level check of the legacy app's System.Web adapters remote authentication endpoint.
//
//   TD_REMOTEAPP_API_KEY=... TD_PROBE_USERS='email1 email2' TD_PROBE_PASSWORDS='pw1 pw2' \
//     dotnet run --file ops/dev/remote-auth-probe.cs -- https://localhost:8443/ [--forwarded-host localhost:5443]
//
// Signs in to the legacy app per user (its own sign-in form), then calls GET /systemweb-adapters/authenticate the way the
// ASP.NET Core remote-app client (Microsoft.AspNetCore.SystemWebAdapters.CoreServices 2.3.0) does: Cookie forwarded,
// X-SystemWebAdapter-RemoteAppAuthentication-Key, x-migration-authenticate: true, x-forwarded-host/-proto, ?original-url=.
// A 200 body is read back with new ClaimsPrincipal(BinaryReader) - the same call the client makes - and its name and roles
// are printed. Negative cases: wrong key, no key, no cookie, no migration header. Prints one line per case; exit 1 when any
// case does not answer as expected. Never prints the key, passwords or cookie values.
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;

var baseUrl = new Uri(args.Length > 0 ? args[0] : "https://localhost:8443/");
var fwdHost = args.SkipWhile(a => a != "--forwarded-host").Skip(1).FirstOrDefault() ?? "localhost:5443";
var key = Environment.GetEnvironmentVariable("TD_REMOTEAPP_API_KEY") ?? throw new Exception("TD_REMOTEAPP_API_KEY not set");
var users = (Environment.GetEnvironmentVariable("TD_PROBE_USERS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
var passwords = (Environment.GetEnvironmentVariable("TD_PROBE_PASSWORDS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
const string KeyHeader = "X-SystemWebAdapter-RemoteAppAuthentication-Key";
const string Path = "systemweb-adapters/authenticate?original-url=%2Fticketcenter";
int failures = 0;

HttpClient NewClient(CookieContainer? jar) => new(new HttpClientHandler
{
    CookieContainer = jar ?? new CookieContainer(), UseCookies = jar != null, AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
}) { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(60) };

async Task<string?> SignInAsync(string user, string password)
{
    var jar = new CookieContainer();
    using var c = NewClient(jar);
    var page = await c.GetStringAsync("user/sign-in");
    var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["UserNameOrEmail"] = user, ["Password"] = password, ["RememberMe"] = "false" };
    using var resp = await c.PostAsync("user/sign-in", new FormUrlEncodedContent(form));
    var auth = jar.GetCookies(baseUrl)[".AspNet.ApplicationCookie"];
    return auth == null ? null : $".AspNet.ApplicationCookie={auth.Value}";
}

async Task<HttpResponseMessage> CallAsync(string? cookie, string? apiKey, bool migrationHeader, string path = Path)
{
    using var c = NewClient(null);
    var req = new HttpRequestMessage(HttpMethod.Get, path);
    req.Headers.Add("x-forwarded-host", fwdHost);
    req.Headers.Add("x-forwarded-proto", "https");
    if (migrationHeader) req.Headers.Add("x-migration-authenticate", "true");
    if (apiKey != null) req.Headers.Add(KeyHeader, apiKey);
    if (cookie != null) req.Headers.Add("Cookie", cookie);
    return await c.SendAsync(req);
}

void Report(string name, bool ok, string detail)
{
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: {detail}");
}

string? firstCookie = null;
for (var i = 0; i < users.Length; i++)
{
    var cookie = await SignInAsync(users[i], passwords[Math.Min(i, passwords.Length - 1)]);
    Report($"sign-in user {i + 1}", cookie != null, cookie != null ? "legacy cookie .AspNet.ApplicationCookie issued" : "no cookie");
    if (cookie == null) continue;
    firstCookie ??= cookie;
    using var resp = await CallAsync(cookie, key, true);
    var body = await resp.Content.ReadAsByteArrayAsync();
    if (resp.StatusCode != HttpStatusCode.OK) { Report($"user {i + 1} + key", false, $"HTTP {(int)resp.StatusCode}"); continue; }
    var principal = new ClaimsPrincipal(new BinaryReader(new MemoryStream(body)));
    var id = (ClaimsIdentity)principal.Identity!;
    var roles = principal.Claims.Where(c => c.Type == id.RoleClaimType).Select(c => c.Value).OrderBy(r => r);
    var given = principal.FindFirst(ClaimTypes.GivenName)?.Value;
    Report($"user {i + 1} + key", id.IsAuthenticated && string.Equals(id.Name, users[i], StringComparison.OrdinalIgnoreCase),
        $"HTTP 200 {resp.Content.Headers.ContentType}, {body.Length} bytes -> authenticated={id.IsAuthenticated} type={id.AuthenticationType} " +
        $"name={id.Name} given_name={given} roles=[{string.Join(", ", roles)}] claims={principal.Claims.Count()}");
}
if (firstCookie != null)
{
    using (var r = await CallAsync(firstCookie, Guid.NewGuid().ToString(), true))
        Report("cookie + wrong key", r.StatusCode == HttpStatusCode.BadRequest, $"HTTP {(int)r.StatusCode}, {(await r.Content.ReadAsByteArrayAsync()).Length} bytes");
    using (var r = await CallAsync(firstCookie, null, true))
        Report("cookie + no key", r.StatusCode == HttpStatusCode.BadRequest, $"HTTP {(int)r.StatusCode}, {(await r.Content.ReadAsByteArrayAsync()).Length} bytes");
    using (var r = await CallAsync(firstCookie, key, false, "systemweb-adapters/authenticate"))
        Report("cookie + key, no x-migration-authenticate, no original-url", r.StatusCode == HttpStatusCode.BadRequest, $"HTTP {(int)r.StatusCode}");
}
using (var r = await CallAsync(null, key, true))
{
    var loc = r.Headers.Location?.ToString() ?? "";
    Report("no cookie + key (challenge)", r.StatusCode == HttpStatusCode.Redirect && loc.StartsWith($"https://{fwdHost}/user/sign-in", StringComparison.OrdinalIgnoreCase),
        $"HTTP {(int)r.StatusCode} Location {loc}");
}
using (var r = await CallAsync(null, null, false))
{
    var loc = r.Headers.Location?.ToString() ?? "";
    Report("return after sign-in (no migration header, original-url)", r.StatusCode == HttpStatusCode.Redirect && loc == "/ticketcenter", $"HTTP {(int)r.StatusCode} Location {loc}");
}
Console.WriteLine($"{failures} failure(s)");
return failures == 0 ? 0 : 1;

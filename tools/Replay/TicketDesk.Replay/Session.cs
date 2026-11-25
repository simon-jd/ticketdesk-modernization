using System.Net;
using System.Text.RegularExpressions;

namespace TicketDesk.Replay;

/// <summary>Resolves {secret:NAME} placeholders from an env-style file and the process environment. Values never get recorded.</summary>
public sealed class SecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public static SecretStore FromSources(IEnumerable<string> envFiles, IEnumerable<KeyValuePair<string, string>>? extra = null)
    {
        var s = new SecretStore();
        foreach (var f in envFiles)
        {
            foreach (var raw in File.ReadAllLines(f))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..];
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var v = line[(eq + 1)..].Trim();
                if (v.Length >= 2 && (v[0] == '"' && v[^1] == '"' || v[0] == '\'' && v[^1] == '\'')) v = v[1..^1];
                s._values[line[..eq].Trim()] = v;
            }
        }
        if (extra != null) foreach (var kv in extra) s._values[kv.Key] = kv.Value;
        return s;
    }

    public void Set(string name, string value) => _values[name] = value;

    public string Get(string name)
    {
        if (_values.TryGetValue(name, out var v)) return v;
        var env = Environment.GetEnvironmentVariable(name);
        if (env != null) return env;
        throw new KeyNotFoundException($"secret '{name}' not found (secrets file or environment)");
    }

    /// <summary>Values that must never appear in a recording.</summary>
    public IEnumerable<KeyValuePair<string, string>> All => _values;
}

/// <summary>Expands {var} and {secret:NAME} placeholders.</summary>
public static partial class Template
{
    [GeneratedRegex(@"\{(?<secret>secret:)?(?<name>[A-Za-z_][A-Za-z0-9_.\-]*)\}")]
    private static partial Regex Placeholder();

    public static string Expand(string template, IReadOnlyDictionary<string, string> vars, SecretStore secrets, bool urlEncode = false) =>
        Placeholder().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            string value;
            if (m.Groups["secret"].Success) value = secrets.Get(name);
            else if (vars.TryGetValue(name, out var v)) value = v;
            else if (name.StartsWith("option:", StringComparison.Ordinal)) return m.Value;
            else throw new KeyNotFoundException($"journey variable '{name}' is not set (template '{template}')");
            return urlEncode ? Uri.EscapeDataString(value) : value;
        });

    /// <summary>Same as Expand but secrets are left as placeholders (for what gets written to disk).</summary>
    public static string ExpandForRecording(string template, IReadOnlyDictionary<string, string> vars) =>
        Placeholder().Replace(template, m =>
            m.Groups["secret"].Success ? m.Value
            : vars.TryGetValue(m.Groups["name"].Value, out var v) ? v : m.Value);
}

/// <summary>
/// Cookie-session handler that works with any inner handler (SocketsHttpHandler or the in-memory TestServer handler),
/// so the recorder sees exactly which cookies were sent.
/// </summary>
public sealed class CookieSessionHandler : DelegatingHandler
{
    public CookieContainer Cookies { get; } = new();

    public CookieSessionHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var header = Cookies.GetCookieHeader(uri);
        request.Headers.Remove("Cookie");
        if (!string.IsNullOrEmpty(header)) request.Headers.TryAddWithoutValidation("Cookie", header);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var sc in setCookies)
            {
                try { Cookies.SetCookies(uri, sc); }
                catch (CookieException) { /* malformed cookie: ignore like a browser would */ }
            }
        }
        return response;
    }
}

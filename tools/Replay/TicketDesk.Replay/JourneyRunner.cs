using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace TicketDesk.Replay;

public sealed class RunnerOptions
{
    public required Uri Target { get; init; }
    public string RunId { get; init; } = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
    public SecretStore Secrets { get; init; } = new();
    public RouteTable Routes { get; init; } = new();
    /// <summary>Creates the innermost handler (SocketsHttpHandler by default, TestServer handler in tests).</summary>
    public Func<HttpMessageHandler>? HandlerFactory { get; init; }
    public bool Insecure { get; init; }
    public int MaxRedirects { get; init; } = 10;
    public int MaxBodyChars { get; init; } = 2_000_000;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);
    /// <summary>Field names whose values are always redacted in recordings (case-insensitive regex).</summary>
    public string RedactFieldPattern { get; init; } = "password";
    /// <summary>Secrets whose names match this regex are also replaced by {secret:NAME} in recorded response bodies/headers
    /// (e.g. the lab admin e-mail shown on profile pages). Applied identically to every run, so comparisons are unaffected.</summary>
    public string RedactSecretNamePattern { get; init; } = "pass|email|user|name|key|token|secret";
    public Action<string>? Log { get; init; }

    /// <summary>
    /// For a page answered by the new app's Angular front end (its shell has no server-rendered forms or tokens), read the next
    /// step's form, extractor values and token from the same URL's server page (<see cref="JourneyRunner.UiHeader"/>). The shell
    /// answer is what gets recorded and compared; the extra GET is not recorded, only counted.
    /// </summary>
    public bool ServerPageForAngular { get; init; } = true;
}

/// <summary>Runs journeys against a target over real HTTP with one cookie session per journey and records every exchange.</summary>
public sealed class JourneyRunner(RunnerOptions options)
{
    private int _seq;
    private int _serverPageFetches;
    private int _postedLinks;

    /// <summary>Request header with which the new app serves a route's server page instead of its Angular page.</summary>
    public const string UiHeader = "X-TicketDesk-Ui";

    /// <summary>Unrecorded GETs of server pages behind Angular answers (<see cref="RunnerOptions.ServerPageForAngular"/>).</summary>
    public int ServerPageFetches => _serverPageFetches;

    /// <summary>Recorded GET steps sent as a POST because the page offers their URL as one now (<see cref="PostedLinks"/>).</summary>
    public int PostedLinkSteps => _postedLinks;

    /// <summary>The new app marks Angular answers with <c>X-TicketDesk-Backend: modern; route=...; ui=angular</c>.</summary>
    internal static bool IsAngularShell(HttpResponseMessage r) =>
        r.Headers.TryGetValues("X-TicketDesk-Backend", out var v) && v.Any(x => x.Contains("ui=angular", StringComparison.Ordinal));

    public async Task<List<RecordedExchange>> RunAsync(IEnumerable<JourneySpec> journeys, CancellationToken ct = default)
    {
        var all = new List<RecordedExchange>();
        foreach (var j in journeys)
        {
            all.AddRange(await RunJourneyAsync(j, ct));
        }
        return all;
    }

    public async Task<List<RecordedExchange>> RunJourneyAsync(JourneySpec journey, CancellationToken ct = default)
    {
        var inner = options.HandlerFactory?.Invoke() ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            SslOptions = options.Insecure
                ? new System.Net.Security.SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
                : new System.Net.Security.SslClientAuthenticationOptions(),
        };
        using var cookieHandler = new CookieSessionHandler(inner);
        using var client = new HttpClient(cookieHandler, disposeHandler: false) { Timeout = options.Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("td-replay/1.0 (+TicketDesk modernization harness)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        var ctx = new JourneyContext(journey.Name);
        var recorded = new List<RecordedExchange>();
        for (int i = 0; i < journey.Steps.Count; i++)
        {
            var step = journey.Steps[i];
            try
            {
                var exchanges = await RunStepAsync(client, cookieHandler, ctx, journey, i, step, ct);
                recorded.AddRange(exchanges);
                var last = exchanges[^1];
                if (step.ExpectStatus is int expected && exchanges[0].Response?.Status != expected)
                {
                    var err = $"step '{step.Name}': expected HTTP {expected}, got {exchanges[0].Response?.Status}";
                    recorded[^1] = last with { Error = err };
                    options.Log?.Invoke($"  ! {journey.Name}: {err} — journey stopped");
                    break;
                }
            }
            catch (StepFailedException ex)
            {
                // The request went out and its response is kept (e.g. a re-rendered form with validation errors), so the
                // recording shows why the step failed.
                var msg = $"step '{step.Name}' failed: {ex.Message}";
                options.Log?.Invoke($"  ! {journey.Name}: {msg} — journey stopped");
                recorded.AddRange(ex.Exchanges);
                recorded[^1] = recorded[^1] with { Error = msg };
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                var msg = $"step '{step.Name}' failed: {ex.GetType().Name}: {ex.Message}";
                options.Log?.Invoke($"  ! {journey.Name}: {msg} — journey stopped");
                recorded.Add(new RecordedExchange
                {
                    Seq = Interlocked.Increment(ref _seq),
                    RunId = options.RunId,
                    Target = options.Target.ToString(),
                    Journey = journey.Name,
                    StepIndex = i,
                    StepName = step.Name,
                    RouteKey = "(not sent)",
                    StartedAt = DateTimeOffset.UtcNow,
                    Request = new RecordedRequest { Method = step.Method, Path = step.Path ?? step.FormSelector ?? "" },
                    Variables = new(ctx.Vars),
                    Step = step,
                    Error = msg,
                });
                break;
            }
        }
        return recorded;
    }

    private sealed class StepFailedException(string message, List<RecordedExchange> exchanges) : Exception(message)
    {
        public List<RecordedExchange> Exchanges { get; } = exchanges;
    }

    private sealed class JourneyContext(string name)
    {
        public string Name { get; } = name;
        public Dictionary<string, string> Vars { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, (Uri Uri, string Body)> HtmlByStep { get; } = new(StringComparer.Ordinal);
        public (Uri Uri, string Body)? LastHtml { get; set; }

        /// <summary>Every HTML response of the journey so far, oldest first (where <see cref="PostedLinks"/> looks).</summary>
        public List<(Uri Uri, string Body)> Pages { get; } = [];

        /// <summary>The antiforgery token of the most recent HTML response that carried one (see <see cref="AntiforgeryHeader"/>).</summary>
        public string? LastToken { get; set; }
    }

    /// <summary>
    /// Header with which a page's script sends its antiforgery token on AJAX posts that have no form (the new app's ticket and
    /// new-ticket pages set it with $.ajaxSetup and Dropzone's defaults, S3). A browser running such a page sends it; the
    /// replay sends it for plain AJAX POST steps whose fields carry no token, from the latest page that rendered one. The
    /// legacy app ignores the header, so recordings against it are unchanged.
    /// </summary>
    public const string AntiforgeryHeader = "RequestVerificationToken";

    private static readonly Regex TokenRx = new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.Compiled);

    private async Task<List<RecordedExchange>> RunStepAsync(HttpClient client, CookieSessionHandler cookies, JourneyContext ctx,
        JourneySpec journey, int index, StepSpec step, CancellationToken ct)
    {
        var (request, recordedForm) = BuildRequest(ctx, step);
        var exchanges = new List<RecordedExchange>();
        var hop = 0;
        while (true)
        {
            var (exchange, response, body) = await SendAndRecordAsync(client, cookies, ctx, journey, index, step, hop, request, recordedForm, ct);
            exchanges.Add(exchange);
            var isRedirect = response.StatusCode is System.Net.HttpStatusCode.Moved or System.Net.HttpStatusCode.Found
                or System.Net.HttpStatusCode.SeeOther or System.Net.HttpStatusCode.TemporaryRedirect or System.Net.HttpStatusCode.PermanentRedirect;
            // what later steps read (forms, extracted values, the token): the page itself, or its server page behind an Angular shell
            var (page, pageBody) = (response, body);
            using var serverPage = body != null && IsHtml(response) && options.ServerPageForAngular && IsAngularShell(response)
                ? await GetServerPageAsync(client, request.RequestUri!, step.Ajax, ct) : null;
            if (serverPage != null) (page, pageBody) = (serverPage, await serverPage.Content.ReadAsStringAsync(ct));
            if (hop == 0)
            {
                try { RunExtractors(ctx, step, request.RequestUri!, page, pageBody); }
                catch (InvalidOperationException ex) { throw new StepFailedException(ex.Message, exchanges); }
            }
            if (pageBody != null && IsHtml(page))
            {
                ctx.LastHtml = (request.RequestUri!, pageBody); ctx.HtmlByStep[step.Name] = ctx.LastHtml.Value; ctx.Pages.Add(ctx.LastHtml.Value);
                if (TokenRx.Match(pageBody) is { Success: true } token) ctx.LastToken = System.Net.WebUtility.HtmlDecode(token.Groups[1].Value);
            }
            exchanges[^1] = exchanges[^1] with { Variables = new(ctx.Vars) };
            if (!isRedirect || !step.FollowRedirects || hop >= options.MaxRedirects || response.Headers.Location is null) break;
            var next = new Uri(request.RequestUri!, response.Headers.Location);
            if (!next.Authority.Equals(options.Target.Authority, StringComparison.OrdinalIgnoreCase)) break; // never leave the target
            request = new HttpRequestMessage(HttpMethod.Get, next);
            if (step.Ajax) request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            recordedForm = null;
            hop++;
        }
        return exchanges;
    }

    private async Task<HttpResponseMessage?> GetServerPageAsync(HttpClient client, Uri uri, bool ajax, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation(UiHeader, "server");
        if (ajax) request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        Interlocked.Increment(ref _serverPageFetches);
        if (response.IsSuccessStatusCode && IsHtml(response) && !IsAngularShell(response)) return response;
        options.Log?.Invoke($"  ? server page of {uri.AbsolutePath} not available (HTTP {(int)response.StatusCode}); using the Angular answer");
        response.Dispose();
        return null;
    }

    private (HttpRequestMessage Request, List<KeyValuePair<string, string>>? RecordedForm) BuildRequest(JourneyContext ctx, StepSpec step)
    {
        HttpRequestMessage req;
        List<KeyValuePair<string, string>>? recordedForm = null;
        if (step.Kind == StepKinds.Form)
        {
            var source = step.FromStep != null
                ? ctx.HtmlByStep.TryGetValue(step.FromStep, out var s) ? s : throw new InvalidOperationException($"no HTML response recorded for step '{step.FromStep}'")
                : ctx.LastHtml ?? throw new InvalidOperationException("no earlier HTML response to read a form from");
            var form = FormReader.Read(source.Body, source.Uri, step.FormSelector ?? "form");
            var overrides = step.Form.Select(f => new KeyValuePair<string, string>(f.Name, FormReader.IsOptionToken(f.Value) ? f.Value : Template.Expand(f.Value, ctx.Vars, options.Secrets)));
            var fields = FormReader.ApplyOverrides(form, overrides);
            var action = form.Action;
            if (step.Query.Count > 0) action = AppendQuery(action, step.Query.Select(q => new KeyValuePair<string, string>(q.Name, Template.Expand(q.Value, ctx.Vars, options.Secrets))));
            var method = form.Method;
            if (method == "GET")
            {
                req = new HttpRequestMessage(HttpMethod.Get, AppendQuery(action, fields));
            }
            else
            {
                req = new HttpRequestMessage(new HttpMethod(method), action) { Content = BuildBody(fields, step.File, form.Multipart, ctx) };
                recordedForm = Redact(fields, step);
            }
        }
        else
        {
            var path = Template.Expand(step.Path ?? "/", ctx.Vars, options.Secrets);
            var uri = new Uri(options.Target, path.TrimStart('/'));
            if (step.Query.Count > 0) uri = AppendQuery(uri, step.Query.Select(q => new KeyValuePair<string, string>(q.Name, Template.Expand(q.Value, ctx.Vars, options.Secrets))));
            req = new HttpRequestMessage(new HttpMethod(step.Method), uri);
            if (step.Form.Count > 0 || step.File != null)
            {
                var fields = step.Form.Select(f => new KeyValuePair<string, string>(f.Name, Template.Expand(f.Value, ctx.Vars, options.Secrets))).ToList();
                req.Content = BuildBody(fields, step.File, false, ctx);
                recordedForm = Redact(fields, step);
            }
            else if (req.Method == HttpMethod.Get && PostedLinks.Find(ctx.Pages, uri, step.Ajax, options.Routes) is { } post)
            {
                req.Method = HttpMethod.Post;
                req.Content = new FormUrlEncodedContent(post.Fields);
                recordedForm = Redact(post.Fields, step);
                Interlocked.Increment(ref _postedLinks);
                options.Log?.Invoke($"  ~ {step.Name}: GET {uri.PathAndQuery} is a post on {post.PageUri.AbsolutePath} now: sent as POST");
            }
        }
        if (step.Ajax) req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        if (step.Ajax && step.Kind != StepKinds.Form && req.Method == HttpMethod.Post && ctx.LastToken != null &&
            !step.Form.Any(f => f.Name.Equals("__RequestVerificationToken", StringComparison.OrdinalIgnoreCase)))
            req.Headers.TryAddWithoutValidation(AntiforgeryHeader, ctx.LastToken);
        return (req, recordedForm);
    }

    private HttpContent BuildBody(List<KeyValuePair<string, string>> fields, FileSpec? file, bool multipart, JourneyContext ctx)
    {
        if (file == null && !multipart) return new FormUrlEncodedContent(fields);
        var mp = new MultipartFormDataContent();
        foreach (var (k, v) in fields) mp.Add(new StringContent(v, Encoding.UTF8), k);
        if (file != null)
        {
            var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(Template.Expand(file.Content, ctx.Vars, options.Secrets)));
            bytes.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            mp.Add(bytes, file.FieldName, file.FileName);
        }
        return mp;
    }

    private List<KeyValuePair<string, string>> Redact(List<KeyValuePair<string, string>> fields, StepSpec step)
    {
        var secretValues = options.Secrets.All.Where(kv => kv.Value.Length >= 4).ToList();
        var templated = step.Form.ToDictionary(f => f.Name, f => f.Value, StringComparer.OrdinalIgnoreCase);
        return fields.Select(f =>
        {
            if (Regex.IsMatch(f.Key, options.RedactFieldPattern, RegexOptions.IgnoreCase))
                return new KeyValuePair<string, string>(f.Key, templated.TryGetValue(f.Key, out var t) && t.Contains("{secret:") ? t : "***");
            var v = f.Value;
            foreach (var (name, secret) in secretValues) v = v.Replace(secret, "{secret:" + name + "}", StringComparison.Ordinal);
            return new KeyValuePair<string, string>(f.Key, v);
        }).ToList();
    }

    private async Task<(RecordedExchange, HttpResponseMessage, string?)> SendAndRecordAsync(HttpClient client, CookieSessionHandler cookies,
        JourneyContext ctx, JourneySpec journey, int index, StepSpec step, int hop, HttpRequestMessage request,
        List<KeyValuePair<string, string>>? recordedForm, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in request.Headers) requestHeaders[h.Key] = string.Join(", ", h.Value);
        var cookieHeader = cookies.Cookies.GetCookieHeader(request.RequestUri!);
        if (!string.IsNullOrEmpty(cookieHeader)) requestHeaders["Cookie"] = string.Join("; ", cookieHeader.Split("; ").Select(c => c.Split('=')[0] + "=…"));
        var contentType = request.Content?.Headers.ContentType?.MediaType;

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        sw.Stop();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        string? text = null;
        string? recordedBody;
        bool isBase64 = false;
        if (IsTextual(mediaType) || (mediaType == null && bytes.Length > 0 && LooksLikeText(bytes)))
        {
            var charset = response.Content.Headers.ContentType?.CharSet;
            var enc = charset != null ? SafeEncoding(charset) : Encoding.UTF8;
            text = enc.GetString(bytes);
            recordedBody = text.Length > options.MaxBodyChars ? text[..options.MaxBodyChars] : text;
        }
        else
        {
            recordedBody = bytes.Length == 0 ? null : Convert.ToBase64String(bytes.Length > options.MaxBodyChars ? bytes[..options.MaxBodyChars] : bytes);
            isBase64 = bytes.Length > 0;
        }

        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in response.Headers) headers[h.Key] = h.Value.Select(RedactSecrets).ToArray();
        foreach (var h in response.Content.Headers) headers[h.Key] = h.Value.ToArray();
        if (!isBase64 && recordedBody != null) recordedBody = RedactSecrets(recordedBody);

        var relPath = RelativePath(request.RequestUri!);
        var exchange = new RecordedExchange
        {
            Seq = Interlocked.Increment(ref _seq),
            RunId = options.RunId,
            Target = options.Target.ToString(),
            Journey = journey.Name,
            StepIndex = index,
            StepName = step.Name,
            Hop = hop,
            RouteKey = options.Routes.Resolve(request.Method.Method, relPath),
            StartedAt = started,
            Request = new RecordedRequest
            {
                Method = request.Method.Method,
                Path = relPath,
                Headers = requestHeaders,
                ContentType = contentType,
                Form = recordedForm,
                Body = step.File != null && hop == 0 ? $"(multipart with file {step.File.FileName})" : null,
            },
            Response = new RecordedResponse
            {
                Status = (int)response.StatusCode,
                Headers = headers,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Body = recordedBody,
                BodyIsBase64 = isBase64,
                BodyLength = bytes.Length,
                ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            },
            Step = hop == 0 ? step : null,
        };
        options.Log?.Invoke($"  {exchange.Request.Method,-4} {relPath} -> {(int)response.StatusCode} ({bytes.Length} B, {sw.ElapsedMilliseconds} ms)");
        return (exchange, response, text);
    }

    private string RedactSecrets(string text)
    {
        foreach (var (name, value) in options.Secrets.All
                     .Where(kv => kv.Value.Length >= 4 && Regex.IsMatch(kv.Key, options.RedactSecretNamePattern, RegexOptions.IgnoreCase))
                     .OrderByDescending(kv => kv.Value.Length))
        {
            text = text.Replace(value, "{secret:" + name + "}", StringComparison.Ordinal);
            var encoded = System.Net.WebUtility.HtmlEncode(value);
            if (encoded != value) text = text.Replace(encoded, "{secret:" + name + "}", StringComparison.Ordinal);
        }
        return text;
    }

    private string RelativePath(Uri uri)
    {
        var basePath = options.Target.AbsolutePath.TrimEnd('/');
        var p = uri.PathAndQuery;
        if (basePath.Length > 0 && p.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) p = p[basePath.Length..];
        return p.Length == 0 ? "/" : p;
    }

    private void RunExtractors(JourneyContext ctx, StepSpec step, Uri requestUri, HttpResponseMessage response, string? body)
    {
        foreach (var x in step.Extract)
        {
            string? value = x.From switch
            {
                "location" => RegexGroup(response.Headers.Location is { } loc ? new Uri(requestUri, loc).PathAndQuery : null, x),
                "header" => RegexGroup(response.Headers.TryGetValues(x.HeaderName ?? "", out var hv) ? string.Join(", ", hv)
                    : response.Content.Headers.TryGetValues(x.HeaderName ?? "", out var cv) ? string.Join(", ", cv) : null, x),
                "regex" => RegexGroup(body, x),
                "html" => body == null ? null : HtmlValue(body, x),
                "json" => body == null ? null : JsonValue(body, x.Expression),
                _ => throw new InvalidOperationException($"unknown extractor source '{x.From}'"),
            };
            if (value == null) throw new InvalidOperationException($"extractor '{x.Var}' ({x.From}: {x.Expression}) found nothing");
            ctx.Vars[x.Var] = value;
        }
    }

    private static string? RegexGroup(string? input, ExtractSpec x)
    {
        if (input == null) return null;
        var m = Regex.Match(input, x.Expression);
        return m.Success ? m.Groups[x.Group].Value : null;
    }

    private static string? HtmlValue(string body, ExtractSpec x)
    {
        var el = new HtmlParser().ParseDocument(body).QuerySelector(x.Expression);
        if (el == null) return null;
        return x.Attribute == null ? el.TextContent.Trim() : el.GetAttribute(x.Attribute);
    }

    private static string? JsonValue(string body, string path)
    {
        JsonNode? node = JsonNode.Parse(body);
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node == null) return null;
            node = int.TryParse(part, out var idx) && node is JsonArray arr ? (idx < arr.Count ? arr[idx] : null) : (node as JsonObject)?[part];
        }
        return node?.ToString();
    }

    private static Uri AppendQuery(Uri uri, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var extra = string.Join('&', fields.Select(f => Uri.EscapeDataString(f.Key) + "=" + Uri.EscapeDataString(f.Value)));
        if (extra.Length == 0) return uri;
        var b = new UriBuilder(uri);
        b.Query = string.IsNullOrEmpty(b.Query) || b.Query == "?" ? extra : b.Query.TrimStart('?') + "&" + extra;
        return b.Uri;
    }

    internal static bool IsHtml(HttpResponseMessage r) => r.Content.Headers.ContentType?.MediaType is "text/html" or "application/xhtml+xml";

    internal static bool IsTextual(string? mediaType) => mediaType != null &&
        (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
         || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeText(byte[] b) => b.Take(512).All(x => x >= 9 && x != 0);

    private static Encoding SafeEncoding(string charset)
    {
        try { return Encoding.GetEncoding(charset.Trim('"')); } catch (ArgumentException) { return Encoding.UTF8; }
    }
}

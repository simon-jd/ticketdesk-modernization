using System.Text.Json;
using System.Text.Json.Serialization;

namespace TicketDesk.Replay;

/// <summary>A scripted user journey: an ordered list of steps run in one cookie session.</summary>
public sealed record JourneySpec
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public List<StepSpec> Steps { get; init; } = [];
}

/// <summary>
/// One step of a journey. Steps are plain data so they can be stored next to each recorded exchange
/// and re-executed later against another target (replay-from-file) without the journey source code.
/// </summary>
public sealed record StepSpec
{
    public required string Name { get; init; }

    /// <summary>"request" sends <see cref="Method"/> <see cref="Path"/>; "form" submits an HTML form found in an earlier response.</summary>
    public string Kind { get; init; } = StepKinds.Request;

    public string Method { get; init; } = "GET";

    /// <summary>Path (and optional query) relative to the target base, with {var} / {secret:NAME} placeholders.</summary>
    public string? Path { get; init; }

    public List<FieldSpec> Query { get; init; } = [];

    /// <summary>For "request": the urlencoded body. For "form": overrides applied on top of the form's own field values.</summary>
    public List<FieldSpec> Form { get; init; } = [];

    /// <summary>CSS selector of the form to submit (kind "form").</summary>
    public string? FormSelector { get; init; }

    /// <summary>Name of the step whose response holds the form; default is the most recent HTML response.</summary>
    public string? FromStep { get; init; }

    /// <summary>Optional multipart file part (forces multipart/form-data).</summary>
    public FileSpec? File { get; init; }

    /// <summary>Send X-Requested-With: XMLHttpRequest like jquery.unobtrusive-ajax and $.get do.</summary>
    public bool Ajax { get; init; }

    public bool FollowRedirects { get; init; } = true;

    public int? ExpectStatus { get; init; }

    public List<ExtractSpec> Extract { get; init; } = [];
}

public static class StepKinds
{
    public const string Request = "request";
    public const string Form = "form";
}

/// <summary>A form/query field. Value is a template; "{option:first}" picks the first non-empty option of a select.</summary>
public sealed record FieldSpec(string Name, string Value);

public sealed record FileSpec(string FieldName, string FileName, string ContentType, string Content);

/// <summary>Extract a value from a response into a journey variable.</summary>
/// <param name="Var">Variable name.</param>
/// <param name="From">location | header | html | json | regex</param>
/// <param name="Expression">Regex (location/header/regex), CSS selector (html), dotted path (json), header name (header).</param>
/// <param name="Attribute">For html: attribute to read; null reads text content.</param>
/// <param name="Group">Regex group to capture.</param>
/// <param name="HeaderName">For "header": header to read before applying the regex.</param>
public sealed record ExtractSpec(string Var, string From, string Expression, string? Attribute = null, int Group = 1, string? HeaderName = null);

public sealed record RecordedRequest
{
    public required string Method { get; init; }
    /// <summary>Path + query relative to the target base (so runs against different hosts compare).</summary>
    public required string Path { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ContentType { get; init; }
    /// <summary>Form fields in order (secrets and password-like fields redacted).</summary>
    public List<KeyValuePair<string, string>>? Form { get; init; }
    public string? Body { get; init; }
}

public sealed record RecordedResponse
{
    public int Status { get; init; }
    public Dictionary<string, string[]> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ContentType { get; init; }
    public string? Body { get; init; }
    public bool BodyIsBase64 { get; init; }
    public long BodyLength { get; init; }
    public double ElapsedMs { get; init; }
}

public sealed record RecordedExchange
{
    public int Seq { get; init; }
    public required string RunId { get; init; }
    public required string Target { get; init; }
    public required string Journey { get; init; }
    public int StepIndex { get; init; }
    public required string StepName { get; init; }
    /// <summary>0 for the step's own request, 1.. for followed redirect hops.</summary>
    public int Hop { get; init; }
    public required string RouteKey { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public required RecordedRequest Request { get; init; }
    public RecordedResponse? Response { get; init; }
    /// <summary>Journey variables after this exchange (values of generated IDs etc., used by the normalizers).</summary>
    public Dictionary<string, string> Variables { get; init; } = new();
    /// <summary>The step definition (only on hop 0), so a recording can be replayed without journey code.</summary>
    public StepSpec? Step { get; init; }
    public string? Error { get; init; }

    [JsonIgnore]
    public string MatchKey => $"{Journey}#{StepIndex:D3}.{Hop}";
}

public static class ReplayJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    public static async Task WriteJsonlAsync(string path, IEnumerable<RecordedExchange> exchanges, CancellationToken ct = default)
    {
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await using var w = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(false));
        foreach (var e in exchanges)
        {
            await w.WriteLineAsync(JsonSerializer.Serialize(e, Options).AsMemory(), ct);
        }
    }

    public static async Task<List<RecordedExchange>> ReadJsonlAsync(string path, CancellationToken ct = default)
    {
        var list = new List<RecordedExchange>();
        await foreach (var line in System.IO.File.ReadLinesAsync(path, ct))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            list.Add(JsonSerializer.Deserialize<RecordedExchange>(line, Options)
                     ?? throw new InvalidDataException($"bad JSONL line in {path}"));
        }
        return list;
    }

    public static List<JourneySpec> ReadJourneys(string path) =>
        JsonSerializer.Deserialize<List<JourneySpec>>(System.IO.File.ReadAllText(path), Options) ?? [];
}

using System.Reflection;
using System.Text;

namespace TicketDesk.Web.Migration;

/// <summary>One row of <c>docs/migration/routes.csv</c> (embedded in this assembly).</summary>
public sealed record RouteRow(string Id, string Controller, string Action, string Verb, string Route, string Auth, string Wave)
{
    /// <summary>Child actions ("(child action only)") render inside other pages and have no URL of their own.</summary>
    public bool IsRoutable => !Route.StartsWith('(');
}

/// <summary>
/// The legacy route inventory (89 rows: 87 routable, 2 child-action rows). It is the denominator of "routes migrated"
/// and the list of ids the migration registry accepts.
/// </summary>
public sealed class RouteInventory
{
    public const string ResourceName = "TicketDesk.Web.Migration.routes.csv";

    public IReadOnlyList<RouteRow> Rows { get; }
    private readonly Dictionary<string, RouteRow> _byId;

    public RouteInventory(IReadOnlyList<RouteRow> rows)
    {
        Rows = rows;
        _byId = rows.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
    }

    public RouteRow? Find(string id) => _byId.GetValueOrDefault(id);

    public static RouteInventory LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"embedded resource {ResourceName} is missing");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static RouteInventory Parse(string csv)
    {
        var lines = csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        var header = SplitCsv(lines[0]);
        int Col(string name)
        {
            var i = header.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 ? i : throw new InvalidDataException($"routes.csv has no '{name}' column");
        }
        int id = Col("id"), controller = Col("controller"), action = Col("action"), verb = Col("verb"), route = Col("route"), auth = Col("auth"), wave = Col("wave");
        var rows = new List<RouteRow>();
        foreach (var line in lines.Skip(1))
        {
            var c = SplitCsv(line);
            rows.Add(new RouteRow(c[id], c[controller], c[action], c[verb], c[route], c[auth], c[wave]));
        }
        var dup = rows.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidDataException($"routes.csv: duplicate id '{dup.Key}'");
        return new RouteInventory(rows);
    }

    /// <summary>RFC 4180 field splitting (quoted fields, doubled quotes).</summary>
    internal static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else sb.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result;
    }
}

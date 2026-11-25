// check-binding-redirects.cs - which assembly binding redirects does a .NET Framework bin folder need, and does Web.config have them?
//
//   dotnet run --file ops/dev/check-binding-redirects.cs -- BIN_DIR [WEB_CONFIG]
//
// Reads assembly metadata only (nothing is loaded or executed). For every strong-named reference from an assembly in BIN_DIR
// to another assembly in BIN_DIR with a different version, a redirect "0.0.0.0-<bin version> -> <bin version>" is needed.
// With WEB_CONFIG, each needed redirect is checked against <runtime><assemblyBinding>; exit 1 when one is missing or does not
// cover the referenced version, or when a reference asks for a newer version than the one in the folder (a downgrade).
// Without WEB_CONFIG, the needed redirects are printed as <dependentAssembly> XML.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

if (args.Length < 1) { Console.Error.WriteLine("usage: check-binding-redirects.cs BIN_DIR [WEB_CONFIG]"); return 2; }
var bin = new Dictionary<string, (Version Ver, string Token, string File)>(StringComparer.OrdinalIgnoreCase);
var refs = new List<(string From, string Name, Version Ver, string Token)>();
static string Hex(byte[] b) => b.Length == 0 ? "null" : Convert.ToHexString(b).ToLowerInvariant();
static byte[] TokenOf(byte[] publicKey)
{
    if (publicKey.Length == 0) return [];
    var h = System.Security.Cryptography.SHA1.HashData(publicKey);
    return h[^8..].Reverse().ToArray();
}
foreach (var f in Directory.GetFiles(args[0], "*.dll"))
{
    using var s = File.OpenRead(f);
    using var pe = new PEReader(s);
    if (!pe.HasMetadata) continue;
    var md = pe.GetMetadataReader();
    if (!md.IsAssembly) continue;
    var def = md.GetAssemblyDefinition();
    var name = md.GetString(def.Name);
    bin[name] = (def.Version, Hex(TokenOf(md.GetBlobBytes(def.PublicKey))), Path.GetFileName(f));
    foreach (var h in md.AssemblyReferences)
    {
        var r = md.GetAssemblyReference(h);
        var pk = md.GetBlobBytes(r.PublicKeyOrToken);
        var tok = (r.Flags & System.Reflection.AssemblyFlags.PublicKey) != 0 ? TokenOf(pk) : pk;
        refs.Add((name, md.GetString(r.Name), r.Version, Hex(tok)));
    }
}
var needed = refs.Where(r => bin.TryGetValue(r.Name, out var b) && b.Token != "null" && b.Token == r.Token && b.Ver != r.Ver)
    .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToList();

var redirects = new Dictionary<string, (Version Lo, Version Hi, Version New)>(StringComparer.OrdinalIgnoreCase);
if (args.Length > 1)
{
    XNamespace asm = "urn:schemas-microsoft-com:asm.v1";
    foreach (var da in XDocument.Load(args[1]).Descendants(asm + "dependentAssembly"))
    {
        var id = da.Element(asm + "assemblyIdentity"); var br = da.Element(asm + "bindingRedirect");
        if (id is null || br is null) continue;
        var old = ((string?)br.Attribute("oldVersion") ?? "").Split('-');
        redirects[(string)id.Attribute("name")!] = (Version.Parse(old[0]), Version.Parse(old[^1]), Version.Parse((string)br.Attribute("newVersion")!));
    }
}
int problems = 0;
foreach (var g in needed)
{
    var b = bin[g.Key];
    var asked = g.Select(r => r.Ver).Distinct().OrderBy(v => v).ToList();
    var askers = string.Join(", ", g.Select(r => $"{r.From}->{r.Ver}").Distinct().Take(4)) + (g.Count() > 4 ? ", ..." : "");
    var downgrade = asked.Any(v => v > b.Ver);
    if (args.Length == 1)
    {
        Console.WriteLine($"      <dependentAssembly>\n        <assemblyIdentity name=\"{g.Key}\" publicKeyToken=\"{b.Token}\" culture=\"neutral\" />\n        <bindingRedirect oldVersion=\"0.0.0.0-{b.Ver}\" newVersion=\"{b.Ver}\" />\n      </dependentAssembly>");
        if (downgrade) { Console.Error.WriteLine($"DOWNGRADE {g.Key}: bin {b.Ver}, asked {string.Join("/", asked)} ({askers})"); problems++; }
        continue;
    }
    string status;
    if (!redirects.TryGetValue(g.Key, out var rd)) status = "MISSING";
    else if (rd.New != b.Ver) status = $"WRONG newVersion {rd.New}";
    else if (asked.Any(v => v < rd.Lo || v > rd.Hi)) status = $"NOT COVERED by {rd.Lo}-{rd.Hi}";
    else status = "ok";
    if (downgrade) status += " DOWNGRADE";
    if (status != "ok") problems++;
    Console.WriteLine($"{status,-12} {g.Key} bin {b.Ver} asked {string.Join("/", asked)} ({askers})");
}
if (args.Length > 1)
{
    foreach (var (name, rd) in redirects.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
    {
        if (!bin.TryGetValue(name, out var b)) Console.WriteLine($"info         redirect for {name} -> {rd.New}: not in the bin folder (framework/GAC assembly)");
        else if (b.Ver != rd.New)
        {
            // Harmful only when some reference falls into the redirected range (it would be sent to a version that is not there).
            var hit = refs.Where(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && r.Ver >= rd.Lo && r.Ver <= rd.Hi).ToList();
            if (hit.Count > 0) { Console.WriteLine($"WRONG        redirect for {name} -> {rd.New} but bin has {b.Ver}; used by {hit[0].From}"); problems++; }
            else Console.WriteLine($"stale        redirect for {name} {rd.Lo}-{rd.Hi} -> {rd.New}, bin has {b.Ver}; no reference in that range (unused)");
        }
    }
    Console.WriteLine($"{bin.Count} assemblies, {needed.Count} need a redirect, {problems} problem(s)");
}
return problems == 0 ? 0 : 1;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TicketDesk.SqlPerf;

/// <summary>Renders an actual execution plan (.sqlplan showplan XML) as an indented operator tree with actual numbers.</summary>
public static partial class PlanText
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static string Render(string xml, int maxPredicate = 110)
    {
        var doc = XDocument.Parse(xml);
        var sb = new StringBuilder();
        foreach (var stmt in doc.Descendants(Ns + "StmtSimple"))
        {
            var text = Spaces().Replace(stmt.Attribute("StatementText")?.Value ?? "", " ").Trim();
            sb.Append("statement: ").Append(text.Length > 160 ? text[..160] + "..." : text).Append('\n');
            var qp = stmt.Element(Ns + "QueryPlan");
            if (qp == null) { sb.Append("  (no plan)\n"); continue; }
            var dop = qp.Attribute("DegreeOfParallelism")?.Value;
            var grant = qp.Element(Ns + "MemoryGrantInfo");
            sb.Append("  cost ").Append(Num(stmt.Attribute("StatementSubTreeCost")?.Value)).Append(", DOP ").Append(dop ?? "1");
            if (grant != null) sb.Append(", memory grant ").Append(grant.Attribute("GrantedMemory")?.Value).Append(" KB (used ").Append(grant.Attribute("MaxUsedMemory")?.Value).Append(')');
            sb.Append('\n');
            foreach (var w in qp.Elements(Ns + "Warnings").Elements()) sb.Append("  warning: ").Append(w.Name.LocalName).Append(' ').Append(string.Join(" ", w.Attributes().Select(a => $"{a.Name.LocalName}={a.Value}"))).Append('\n');
            var root = qp.Element(Ns + "RelOp");
            if (root != null) Node(root, 1, sb, maxPredicate);
        }
        return sb.ToString();
    }

    private static void Node(XElement relOp, int depth, StringBuilder sb, int maxPredicate)
    {
        var phys = relOp.Attribute("PhysicalOp")?.Value ?? "?";
        var logical = relOp.Attribute("LogicalOp")?.Value ?? "";
        var counters = relOp.Element(Ns + "RunTimeInformation")?.Elements(Ns + "RunTimeCountersPerThread").ToList() ?? [];
        long Sum(string attr) => counters.Sum(c => long.TryParse(c.Attribute(attr)?.Value, out var v) ? v : 0);
        var opElement = relOp.Elements().FirstOrDefault(e => e.Name.LocalName is not ("OutputList" or "RunTimeInformation" or "Warnings" or "MemoryFractions" or "RunTimePartitionSummary" or "InternalInfo"));
        var obj = opElement?.Elements(Ns + "Object").FirstOrDefault();
        var objText = obj == null ? "" : " " + string.Join(".", new[] { obj.Attribute("Table")?.Value, obj.Attribute("Index")?.Value }.Where(x => x != null)).Replace("[", "").Replace("]", "");
        if (obj?.Attribute("Filtered")?.Value == "1") objText += " (filtered)";

        sb.Append(new string(' ', depth * 2)).Append(phys);
        if (logical.Length > 0 && logical != phys) sb.Append(" (").Append(logical).Append(')');
        sb.Append(objText);
        if (relOp.Attribute("Parallel")?.Value == "1") sb.Append(" [parallel]");
        var mode = counters.Select(c => c.Attribute("ActualExecutionMode")?.Value).FirstOrDefault(m => m != null);
        if (mode == "Batch") sb.Append(" [batch]");
        sb.Append(" | rows ").Append(counters.Count > 0 ? Sum("ActualRows").ToString("N0", CultureInfo.InvariantCulture) : "?")
          .Append(" (est ").Append(Num(relOp.Attribute("EstimateRows")?.Value)).Append(')');
        var execs = Sum("ActualExecutions");
        if (execs > 1) sb.Append(", execs ").Append(execs.ToString("N0", CultureInfo.InvariantCulture));
        var reads = Sum("ActualLogicalReads");
        if (reads > 0) sb.Append(", reads ").Append(reads.ToString("N0", CultureInfo.InvariantCulture));
        // per-operator CPU (sum over threads; includes the operator's children in row mode, its own work in batch mode)
        var cpu = Sum("ActualCPUms");
        if (cpu > 0) sb.Append(", cpu ").Append(cpu.ToString("N0", CultureInfo.InvariantCulture)).Append(" ms");
        if (opElement?.Name.LocalName == "TopSort" || opElement?.Name.LocalName == "Top")
        {
            var rowsAttr = opElement.Attribute("Rows")?.Value;
            if (rowsAttr != null) sb.Append(", top ").Append(rowsAttr);
        }
        var own = opElement == null ? [] : Own(opElement).ToList();
        var seek = own.FirstOrDefault(e => e.Name == Ns + "SeekPredicateNew") ?? own.FirstOrDefault(e => e.Name == Ns + "SeekPredicate");
        if (seek != null) sb.Append(", seek: ").Append(Predicate(seek, maxPredicate));
        var pred = opElement?.Elements(Ns + "Predicate").FirstOrDefault();
        if (pred != null) sb.Append(", where: ").Append(Predicate(pred, maxPredicate));
        foreach (var w in relOp.Elements(Ns + "Warnings").Elements()) sb.Append(", WARNING ").Append(w.Name.LocalName);
        sb.Append('\n');
        foreach (var child in ChildRelOps(relOp)) Node(child, depth + 1, sb, maxPredicate);
    }

    /// <summary>Descendants of an operator element that belong to it, not to the operators nested under it.</summary>
    private static IEnumerable<XElement> Own(XElement e)
    {
        foreach (var c in e.Elements())
        {
            if (c.Name == Ns + "RelOp") continue;
            yield return c;
            foreach (var d in Own(c)) yield return d;
        }
    }

    private static IEnumerable<XElement> ChildRelOps(XElement relOp)
    {
        // RelOp children sit under the operator element (e.g. <NestedLoops><RelOp/>...), possibly nested deeper
        foreach (var e in relOp.Elements())
        {
            if (e.Name.LocalName is "OutputList" or "RunTimeInformation" or "Warnings") continue;
            foreach (var r in Direct(e)) yield return r;
        }

        static IEnumerable<XElement> Direct(XElement e)
        {
            foreach (var c in e.Elements())
            {
                if (c.Name == Ns + "RelOp") yield return c;
                else if (c.Name.LocalName is not ("ScalarOperator" or "Predicate" or "SeekPredicates" or "DefinedValues" or "OuterReferences" or "OrderBy"))
                    foreach (var d in Direct(c)) yield return d;
            }
        }
    }

    private static string Predicate(XElement e, int max)
    {
        var scalar = e.Descendants(Ns + "ScalarOperator").FirstOrDefault()?.Attribute("ScalarString")?.Value;
        var text = scalar ?? string.Join(" AND ", e.Descendants(Ns + "ColumnReference").Select(c => c.Attribute("Column")?.Value).Distinct());
        text = Spaces().Replace(text.Replace("[TicketDeskL].", "").Replace("[dbo].", "").Replace("[", "").Replace("]", ""), " ");
        return text.Length > max ? text[..max] + "..." : text;
    }

    private static string Num(string? v) =>
        double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d.ToString(d >= 100 ? "N0" : "0.###", CultureInfo.InvariantCulture) : v ?? "?";
}

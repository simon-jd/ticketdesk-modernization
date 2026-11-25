using System.Globalization;
using System.Text.RegularExpressions;

namespace TicketDesk.SqlPerf;

/// <summary>Per-table numbers from one <c>SET STATISTICS IO</c> line.</summary>
public sealed record TableIo(string Table, long ScanCount, long LogicalReads, long PhysicalReads, long ReadAheadReads, long LobLogicalReads);

/// <summary>What <c>SET STATISTICS IO, TIME ON</c> reported for one command.</summary>
public sealed record CommandStatistics(
    long ExecCpuMs, long ExecElapsedMs, long CompileCpuMs, long CompileElapsedMs, IReadOnlyList<TableIo> Tables)
{
    public long LogicalReads => Tables.Sum(t => t.LogicalReads);
    public long LobLogicalReads => Tables.Sum(t => t.LobLogicalReads);
    public long PhysicalReads => Tables.Sum(t => t.PhysicalReads);
    public long ReadAheadReads => Tables.Sum(t => t.ReadAheadReads);
}

public static partial class StatisticsParser
{
    [GeneratedRegex(@"parse and compile time:\s*CPU time = (\d+) ms,\s*elapsed time = (\d+) ms", RegexOptions.IgnoreCase)]
    private static partial Regex Compile();

    [GeneratedRegex(@"Execution Times:\s*CPU time = (\d+) ms,\s*elapsed time = (\d+) ms", RegexOptions.IgnoreCase)]
    private static partial Regex Exec();

    [GeneratedRegex(@"^Table '(?<t>[^']+)'\. Scan count (?<sc>\d+), logical reads (?<lr>\d+), physical reads (?<pr>\d+)")]
    private static partial Regex TableLine();

    // "page server read-ahead reads" also ends in "read-ahead reads": take the first one preceded by ", " directly
    [GeneratedRegex(@", read-ahead reads (\d+)")]
    private static partial Regex ReadAhead();

    [GeneratedRegex(@"lob logical reads (\d+)")]
    private static partial Regex LobLogical();

    /// <summary>
    /// Nested statements (a procedure, sp_executesql, the statements inside them) each report "Execution Times", and an
    /// outer report includes the inner ones, so the command's execution time is the largest reported value. Compile
    /// times and IO lines are per statement and are summed.
    /// </summary>
    public static CommandStatistics Parse(IEnumerable<string> messages)
    {
        long execCpu = 0, execElapsed = 0, compileCpu = 0, compileElapsed = 0;
        var tables = new List<TableIo>();
        foreach (var message in messages)
        {
            foreach (Match m in Compile().Matches(message))
            {
                compileCpu += L(m.Groups[1].Value);
                compileElapsed += L(m.Groups[2].Value);
            }
            foreach (Match m in Exec().Matches(message))
            {
                execCpu = Math.Max(execCpu, L(m.Groups[1].Value));
                execElapsed = Math.Max(execElapsed, L(m.Groups[2].Value));
            }
            foreach (var line in message.Split('\n'))
            {
                var t = TableLine().Match(line.Trim());
                if (!t.Success) continue;
                var ra = ReadAhead().Match(line);
                var lob = LobLogical().Match(line);
                tables.Add(new TableIo(t.Groups["t"].Value, L(t.Groups["sc"].Value), L(t.Groups["lr"].Value), L(t.Groups["pr"].Value),
                    ra.Success ? L(ra.Groups[1].Value) : 0, lob.Success ? L(lob.Groups[1].Value) : 0));
            }
        }
        return new CommandStatistics(execCpu, execElapsed, compileCpu, compileElapsed, tables);
    }

    private static long L(string s) => long.Parse(s, CultureInfo.InvariantCulture);
}

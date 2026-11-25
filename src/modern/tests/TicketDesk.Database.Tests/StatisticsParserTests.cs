using TicketDesk.SqlPerf;

namespace TicketDesk.Database.Tests;

/// <summary>td-sqlperf's reading of SET STATISTICS IO/TIME output (the numbers in docs/RESULTS.md come from it).</summary>
public sealed class StatisticsParserTests
{
    [Fact]
    public void Nested_execution_times_take_the_outermost_and_io_lines_are_summed()
    {
        string[] messages =
        [
            "SQL Server parse and compile time: \n   CPU time = 3 ms, elapsed time = 4 ms.",
            "Table 'Tickets'. Scan count 1, logical reads 3326, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.",
            "\n SQL Server Execution Times:\n   CPU time = 40 ms,  elapsed time = 41 ms.",
            "Table 'IdentityUsers'. Scan count 0, logical reads 117, physical reads 2, page server reads 0, read-ahead reads 9, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.\nTable 'Tickets'. Scan count 1, logical reads 63, physical reads 1, page server reads 0, read-ahead reads 5, page server read-ahead reads 7, lob logical reads 4, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.",
            "\n SQL Server Execution Times:\n   CPU time = 2 ms,  elapsed time = 3 ms.",
            "\n SQL Server Execution Times:\n   CPU time = 45 ms,  elapsed time = 47 ms.",
        ];
        var s = StatisticsParser.Parse(messages);
        Assert.Equal(45, s.ExecCpuMs);
        Assert.Equal(47, s.ExecElapsedMs);
        Assert.Equal(3, s.CompileCpuMs);
        Assert.Equal(3326 + 117 + 63, s.LogicalReads);
        Assert.Equal(3, s.PhysicalReads);
        Assert.Equal(9 + 5, s.ReadAheadReads); // not the 7 page-server read-aheads
        Assert.Equal(4, s.LobLogicalReads);
        Assert.Equal(3, s.Tables.Count);
    }

    [Fact]
    public void Percentiles_are_median_and_nearest_rank()
    {
        Assert.Equal(2.5, Stats.Median([4, 1, 3, 2]));
        Assert.Equal(19, Stats.NearestRank(Enumerable.Range(1, 20).Select(i => (double)i).ToList(), 0.95));
        Assert.Equal(5, Stats.NearestRank([1, 2, 3, 4, 5], 0.95));
    }
}

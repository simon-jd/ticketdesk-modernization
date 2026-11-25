using TicketDesk.Search;

namespace TicketDesk.Modules.Tests;

/// <summary>The legacy search box's Lucene syntax, as far as it is kept, and that no input reaches CONTAINS unquoted.</summary>
public sealed class SearchQueryTests
{
    private static readonly HashSet<string> Stop = ["the", "a", "is", "of", "and", "1"];

    private static SearchQuery P(string text) => SearchQuery.Parse(text, Stop);

    [Fact]
    public void Words_are_alternatives_like_the_default_OR_operator()
    {
        var q = P("Printer  Network");
        Assert.Equal([new SearchClause(SearchOccur.Should, SearchField.Any, "printer"), new SearchClause(SearchOccur.Should, SearchField.Any, "network")], q.Clauses);
        Assert.True(q.HasPositive);
    }

    [Fact]
    public void Required_and_excluded_terms()
    {
        Assert.Equal([SearchOccur.Must, SearchOccur.MustNot, SearchOccur.MustNot, SearchOccur.Should], P("+toner -laser !wifi dock").Clauses.Select(c => c.Occur));
        Assert.Equal([SearchOccur.Must, SearchOccur.Must], P("toner AND laser").Clauses.Select(c => c.Occur));
        Assert.Equal([SearchOccur.Must, SearchOccur.Must], P("toner && laser").Clauses.Select(c => c.Occur));
        Assert.Equal([SearchOccur.Should, SearchOccur.MustNot], P("toner NOT laser").Clauses.Select(c => c.Occur));
        Assert.Equal([SearchOccur.Should, SearchOccur.Should], P("toner OR laser").Clauses.Select(c => c.Occur));
        // lower-case "and"/"or" are words (Lucene operators are upper case), here stop words
        Assert.Equal(["toner", "laser"], P("toner and laser").Clauses.Select(c => c.Text));
    }

    [Fact]
    public void A_query_of_exclusions_only_matches_nothing()
    {
        Assert.False(P("-printer").HasPositive);
        Assert.False(P("").HasPositive);
        Assert.False(P("the of a").HasPositive); // stop words only
    }

    [Fact]
    public void Phrases_prefixes_and_the_legacy_field_names()
    {
        Assert.Equal(new SearchClause(SearchOccur.Should, SearchField.Any, "out toner", IsPhrase: true), P("\"out of toner\"").Clauses.Single());
        Assert.Equal(new SearchClause(SearchOccur.Should, SearchField.Any, "print", IsPrefix: true), P("print*").Clauses.Single());
        Assert.Equal(new SearchClause(SearchOccur.Must, SearchField.Tags, "printer"), P("+tags:printer").Clauses.Single());
        Assert.Equal(new SearchClause(SearchOccur.Should, SearchField.Title, "no toner", IsPhrase: true), P("title:\"no toner\"").Clauses.Single());
        Assert.Equal(new SearchClause(SearchOccur.Should, SearchField.Id, "42"), P("id:42").Clauses.Single());
        Assert.Equal(42, P("42").Clauses.Single().TicketId);
        Assert.Null(P("details:42").Clauses.Single().TicketId);
        // a field the legacy document did not have: Lucene matched nothing for it
        Assert.Empty(P("owner:bob").Clauses);
        Assert.True(P("+owner:bob printer").MatchesNothing);
    }

    [Fact]
    public void Words_keep_inner_joiners_and_split_elsewhere()
    {
        Assert.Equal("network-041", P("network-041").Clauses.Single().Text);
        Assert.Equal("o'neil", P("O'Neil").Clauses.Single().Text);
        Assert.Equal(new SearchClause(SearchOccur.Should, SearchField.Any, "foo bar", IsPhrase: true), P("foo/bar").Clauses.Single());
        Assert.Equal("printer", P("(printer)^2").Clauses.Single().Text);
        Assert.Equal("printer", P("printer~0.8").Clauses.Single().Text);
    }

    [Theory]
    [InlineData("\" OR 1=1 --")]
    [InlineData("printer\") OR (\"x")]
    [InlineData("NEAR((a, b), 5)")]
    [InlineData("FORMSOF(INFLECTIONAL, print)")]
    [InlineData("*")]
    [InlineData("\"")]
    [InlineData("+")]
    [InlineData("a*b* *c ??? [x TO y] {z}")]
    [InlineData("ISABOUT(printer WEIGHT(.8))")]
    public void No_input_reaches_CONTAINS_unquoted(string text)
    {
        foreach (var c in P(text).Clauses)
        {
            Assert.Matches("^\"[\\p{L}\\p{N}][\\p{L}\\p{N} _.'@&-]*\\*?\"$", c.ContainsTerm);
            Assert.DoesNotContain('"', c.Text);
        }
    }

    [Fact]
    public void Long_input_is_bounded()
    {
        var q = P(string.Join(' ', Enumerable.Range(0, 100).Select(i => $"w{i}")) + " " + new string('x', 500));
        Assert.Equal(SearchQuery.MaxClauses, q.Clauses.Count);
        Assert.All(q.Clauses, c => Assert.True(c.Text.Length <= SearchQuery.MaxTermLength));
    }

    [Fact]
    public void User_text_goes_into_parameters_only()
    {
        var q = P("+\"zz top\" -x' ;DROP TABLE dbo.Tickets-- 77");
        var (sql, parameters) = FullTextTicketSearch.BuildSql(q, new SearchRequest("", 3, new SearchViewer("user-1", false)));
        Assert.DoesNotContain("zz", sql);
        Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user-1", sql);
        Assert.Contains("CONTAINSTABLE(dbo.Tickets, Title,", sql);
        Assert.Contains("CONTAINSTABLE(dbo.TicketEvents, Comment,", sql);
        Assert.Contains("t.ProjectId = @project", sql);
        Assert.Contains("dbo.TicketSubscribers", sql);
        Assert.Contains(parameters, p => (p.Value as string) == "\"zz top\"");
        Assert.Contains(parameters, p => p.Value is 77);
    }
}

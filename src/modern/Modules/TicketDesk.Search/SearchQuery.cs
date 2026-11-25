using System.Globalization;
using System.Text;

namespace TicketDesk.Search;

public enum SearchOccur { Should, Must, MustNot }

/// <summary>The fields of the legacy Lucene document (<c>LuceneSearchIndexItemExtensions</c>): id, title, details, tags, events.</summary>
public enum SearchField { Any, Id, Title, Details, Tags, Events }

/// <summary>One term or phrase of a search, with how it must occur and where.</summary>
public sealed record SearchClause(SearchOccur Occur, SearchField Field, string Text, bool IsPhrase = false, bool IsPrefix = false)
{
    /// <summary>The term in CONTAINS syntax: always a quoted simple term or prefix term, never user syntax.</summary>
    public string ContainsTerm => IsPrefix ? $"\"{Text}*\"" : $"\"{Text}\"";

    /// <summary>A whole-number word: the legacy index also matched it against the ticket id (field "id", not analyzed).</summary>
    public int? TicketId => !IsPhrase && !IsPrefix && Field is SearchField.Any or SearchField.Id &&
                            int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    public bool AppliesTo(SearchField field) => Field == SearchField.Any || Field == field;
}

/// <summary>
/// The legacy search box took Lucene query syntax (<c>MultiFieldQueryParser</c> over id, title, details, tags and events,
/// default operator OR, StandardAnalyzer). This is the subset worth keeping, turned into clauses that the full-text query
/// builds from: words (any of them), <c>"phrases"</c>, <c>prefix*</c>, <c>+required</c>, <c>-excluded</c>, <c>AND</c> /
/// <c>OR</c> / <c>NOT</c> (also <c>&amp;&amp;</c>, <c>||</c>, <c>!</c>), and the legacy field names (<c>tags:printer</c>,
/// <c>title:"no toner"</c>, <c>id:42</c>). Everything else that is syntax in Lucene (parentheses, boosts <c>^2</c>, fuzzy
/// <c>~</c>, ranges, leading wildcards) is dropped instead of being passed on: the user's text never reaches CONTAINS
/// unquoted, so no input can make the query fail. Lower-casing and stop words follow the analyzer: SQL Server's English
/// system stoplist is passed in, and words on it are dropped (Lucene's StandardAnalyzer dropped its own stop words).
/// </summary>
public sealed record SearchQuery(IReadOnlyList<SearchClause> Clauses, bool MatchesNothing = false)
{
    public const int MaxClauses = 16;
    public const int MaxTermLength = 100;

    public static readonly SearchQuery Empty = new([]);

    /// <summary>Lucene: a query without a positive clause (only exclusions) matches nothing.</summary>
    public bool HasPositive => !MatchesNothing && Clauses.Any(c => c.Occur != SearchOccur.MustNot);

    private static readonly Dictionary<string, SearchField> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = SearchField.Id, ["title"] = SearchField.Title, ["details"] = SearchField.Details, ["tags"] = SearchField.Tags, ["events"] = SearchField.Events,
    };

    public static SearchQuery Parse(string? text, IReadOnlySet<string> stopwords)
    {
        if (string.IsNullOrWhiteSpace(text)) return Empty;
        var raw = Tokenize(text);
        var clauses = new List<SearchClause>();
        var matchesNothing = false;
        SearchOccur? pendingOccur = null; // from a NOT operator
        var andBefore = false;            // an AND operator stands before the next clause
        foreach (var tok in raw)
        {
            if (tok.Operator is { } op)
            {
                if (op == "AND")
                {
                    andBefore = true;
                    // the clause before an AND is required too (Lucene QueryParser: a AND b => +a +b)
                    if (clauses.Count > 0 && clauses[^1].Occur == SearchOccur.Should) clauses[^1] = clauses[^1] with { Occur = SearchOccur.Must };
                }
                else if (op == "NOT") pendingOccur = SearchOccur.MustNot;
                continue; // OR is the default operator
            }
            var occur = tok.Sign switch { '+' => SearchOccur.Must, '-' or '!' => SearchOccur.MustNot, _ => SearchOccur.Should };
            if (pendingOccur is { } p) occur = p;
            else if (andBefore && occur == SearchOccur.Should) occur = SearchOccur.Must;
            pendingOccur = null;
            andBefore = false;

            var field = SearchField.Any;
            if (tok.Field != null)
            {
                if (!Fields.TryGetValue(tok.Field, out field))
                {
                    // Lucene searched a field the document does not have: such a clause never matches
                    if (occur == SearchOccur.Must) matchesNothing = true;
                    continue;
                }
            }
            var clause = Clause(occur, field, tok, stopwords);
            if (clause == null)
            {
                continue; // stop words only: the analyzer removed the whole clause
            }
            if (clauses.Count < MaxClauses) clauses.Add(clause);
        }
        return new SearchQuery(clauses, matchesNothing);
    }

    private static SearchClause? Clause(SearchOccur occur, SearchField field, RawToken tok, IReadOnlySet<string> stopwords)
    {
        var words = Words(tok.Text);
        var prefix = !tok.Quoted && tok.Text.EndsWith('*') && words.Count == 1;
        if (field == SearchField.Id)
        {
            // not analyzed in the legacy index: the whole value is the term
            var id = string.Concat(tok.Text.Where(char.IsDigit));
            return id.Length is > 0 and <= 10 ? new SearchClause(occur, field, id) : null;
        }
        if (!prefix) words = words.Where(w => !stopwords.Contains(w)).ToList();
        if (words.Count == 0) return null;
        // several words in one Lucene term (quoted, or split by the analyzer as in "foo/bar") form a phrase
        return words.Count == 1
            ? new SearchClause(occur, field, words[0], IsPrefix: prefix)
            : new SearchClause(occur, field, string.Join(' ', words), IsPhrase: true);
    }

    /// <summary>Lower-cased words: letters and digits, joined inside a word by - _ . ' @ &amp; (like "network-041", "o'neil").</summary>
    internal static List<string> Words(string text)
    {
        var words = new List<string>();
        var sb = new StringBuilder();
        void Flush()
        {
            var w = sb.ToString().Trim('-', '_', '.', '\'', '@', '&');
            if (w.Length > 0) words.Add(w.Length > MaxTermLength ? w[..MaxTermLength] : w);
            sb.Clear();
        }
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (sb.Length > 0 && ch is '-' or '_' or '.' or '\'' or '@' or '&') sb.Append(ch);
            else Flush();
        }
        Flush();
        return words;
    }

    private sealed record RawToken(string Text, bool Quoted, char Sign, string? Field, string? Operator);

    private static List<RawToken> Tokenize(string text)
    {
        var tokens = new List<RawToken>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] is '(' or ')')) i++;
            if (i >= text.Length) break;
            var sign = '\0';
            if (text[i] is '+' or '-' or '!' && !(text[i] == '!' && i + 1 < text.Length && text[i + 1] == '='))
            {
                sign = text[i];
                i++;
            }
            if (i >= text.Length) break;
            string? field = null;
            // field:value (a word of letters followed by a colon)
            var j = i;
            while (j < text.Length && char.IsLetter(text[j])) j++;
            if (j > i && j < text.Length && text[j] == ':')
            {
                field = text[i..j];
                i = j + 1;
            }
            if (i < text.Length && text[i] == '"')
            {
                var end = text.IndexOf('"', i + 1);
                if (end < 0) end = text.Length;
                tokens.Add(new RawToken(text[(i + 1)..end], true, sign, field, null));
                i = Math.Min(text.Length, end + 1);
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != ')') i++; // ~2 or ^3 after a phrase
                continue;
            }
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('(' or ')')) i++;
            var word = text[start..i];
            var caret = word.IndexOfAny(['^', '~']);
            if (caret >= 0) word = word[..caret];
            if (sign == '\0' && field == null && word is "AND" or "&&" or "OR" or "||" or "NOT")
            {
                tokens.Add(new RawToken(word, false, sign, null, word switch { "&&" => "AND", "||" => "OR", _ => word }));
                continue;
            }
            if (word.Length > 0) tokens.Add(new RawToken(word, false, sign, field, null));
        }
        return tokens;
    }
}

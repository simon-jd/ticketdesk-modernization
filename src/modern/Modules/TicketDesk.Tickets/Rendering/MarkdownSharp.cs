// MarkdownSharp 1.13.0.0 (the version the legacy TicketDesk app ships), ported to .NET 10 for byte-equal ticket rendering.
// Source: decompiled from the NuGet package's lib/35/MarkdownSharp.dll (ilspycmd 11.2), then changed only where the runtime
// differs: (1) StartsWith/EndsWith with string literals compare ordinally (under ICU, "\u001a" - the hash-key marker - is an
// ignorable character, so culture-sensitive StartsWith("\u001a") was true for every paragraph); (2) the constructor that read
// options from app.config (System.Configuration) and the obsolete MarkdownOld class are left out. Proven byte-equal against the
// legacy DLL on the lab VM: ops/dev/render-parity/ (docs/evidence/tickets-*/rendering/).
//
// MarkdownSharp - Copyright (c) 2009-2011 Jeff Atwood (http://www.codinghorror.com/), MIT License:
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy,
// modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software
// is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
// KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// Based on Markdown.pl - Copyright (c) 2004 John Gruber (http://daringfireball.net/projects/markdown/), BSD-style license.
#nullable enable annotations
#nullable disable warnings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TicketDesk.Tickets.Rendering.MarkdownSharp;

public interface IMarkdownOptions
{
	/// <summary>
	/// when true, (most) bare plain URLs are auto-hyperlinked  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	bool AutoHyperlink { get; }

	/// <summary>
	/// when true, RETURN becomes a literal newline  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	bool AutoNewLines { get; }

	/// <summary>
	/// use "&gt;" for HTML output, or " /&gt;" for XHTML output
	/// </summary>
	string EmptyElementSuffix { get; }

	/// <summary>
	/// when true, problematic URL characters like [, ], (, and so forth will be encoded 
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	bool EncodeProblemUrlCharacters { get; }

	/// <summary>
	/// when false, email addresses will never be auto-linked  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	bool LinkEmails { get; }

	/// <summary>
	/// when true, bold and italic require non-word characters on either side  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	bool StrictBoldItalic { get; }
}
/// <summary>
/// Markdown is a text-to-HTML conversion tool for web writers. 
/// Markdown allows you to write using an easy-to-read, easy-to-write plain text format, 
/// then convert it to structurally valid XHTML (or HTML).
/// </summary>
public class Markdown
{
	private enum TokenType
	{
		Text,
		Tag
	}

	private struct Token(TokenType type, string value)
	{
		public TokenType Type = type;

		public string Value = value;
	}

	private const string _version = "1.13";

	/// <summary>
	/// maximum nested depth of [] and () supported by the transform; implementation detail
	/// </summary>
	private const int _nestDepth = 6;

	/// <summary>
	/// Tabs are automatically converted to spaces as part of the transform  
	/// this constant determines how "wide" those tabs become in spaces  
	/// </summary>
	private const int _tabWidth = 4;

	private const string _markerUL = "[*+-]";

	private const string _markerOL = "\\d+[.]";

	private static readonly Dictionary<string, string> _escapeTable;

	private static readonly Dictionary<string, string> _invertedEscapeTable;

	private static readonly Dictionary<string, string> _backslashEscapeTable;

	private readonly Dictionary<string, string> _urls = new Dictionary<string, string>();

	private readonly Dictionary<string, string> _titles = new Dictionary<string, string>();

	private readonly Dictionary<string, string> _htmlBlocks = new Dictionary<string, string>();

	private int _listLevel;

	private static Regex _newlinesLeadingTrailing;

	private static Regex _newlinesMultiple;

	private static Regex _leadingWhitespace;

	private static string _nestedBracketsPattern;

	private static string _nestedParensPattern;

	private static Regex _linkDef;

	private static Regex _blocksHtml;

	private static Regex _htmlTokens;

	private static Regex _anchorRef;

	private static Regex _anchorInline;

	private static Regex _anchorRefShortcut;

	private static Regex _imagesRef;

	private static Regex _imagesInline;

	private static Regex _headerSetext;

	private static Regex _headerAtx;

	private static Regex _horizontalRules;

	private static string _wholeList;

	private static Regex _listNested;

	private static Regex _listTopLevel;

	private static Regex _codeBlock;

	private static Regex _codeSpan;

	private static Regex _bold;

	private static Regex _strictBold;

	private static Regex _italic;

	private static Regex _strictItalic;

	private static Regex _blockquote;

	private static Regex _autolinkBare;

	private static Regex _outDent;

	private static Regex _codeEncoder;

	private static Regex _amps;

	private static Regex _angles;

	private static Regex _backslashEscapes;

	private static Regex _unescapes;

	private static char[] _problemUrlChars;

	public IMarkdownOptions Options { get; private set; }

	/// <summary>
	/// current version of MarkdownSharp;  
	/// see http://code.google.com/p/markdownsharp/ for the latest code or to contribute
	/// </summary>
	public string Version => "1.13";

	/// <summary>
	/// Create a new Markdown instance using default options
	/// </summary>
	public Markdown()
		: this(new MarkdownOptions())
	{
	}

	/// <summary>
	/// Create a new Markdown instance and optionally load options from the supplied options parameter.
	/// </summary>
	public Markdown(IMarkdownOptions options)
	{
		Options = options;
	}


	/// <summary>
	/// In the static constuctor we'll initialize what stays the same across all transforms.
	/// </summary>
	static Markdown()
	{
		_newlinesLeadingTrailing = new Regex("^\\n+|\\n+\\z", RegexOptions.Compiled);
		_newlinesMultiple = new Regex("\\n{2,}", RegexOptions.Compiled);
		_leadingWhitespace = new Regex("^[ ]*", RegexOptions.Compiled);
		_linkDef = new Regex($"\r\n                        ^[ ]{{0,{3}}}\\[(.+)\\]:  # id = $1\r\n                          [ ]*\r\n                          \\n?                   # maybe *one* newline\r\n                          [ ]*\r\n                        <?(\\S+?)>?              # url = $2\r\n                          [ ]*\r\n                          \\n?                   # maybe one newline\r\n                          [ ]*\r\n                        (?:\r\n                            (?<=\\s)             # lookbehind for whitespace\r\n                            [\"(]\r\n                            (.+?)               # title = $3\r\n                            [\")]\r\n                            [ ]*\r\n                        )?                      # title is optional\r\n                        (?:\\n+|\\Z)", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_blocksHtml = new Regex(GetBlockPattern(), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
		_htmlTokens = new Regex("\r\n            (<!(?:--.*?--\\s*)+>)|        # match <!-- foo -->\r\n            (<\\?.*?\\?>)|                 # match <?foo?> " + RepeatString(" \r\n            (<[A-Za-z\\/!$](?:[^<>]|", 6) + RepeatString(")*>)", 6) + " # match <tag> and </tag>", RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_anchorRef = new Regex($"\r\n            (                               # wrap whole match in $1\r\n                \\[\r\n                    ({GetNestedBracketsPattern()})                   # link text = $2\r\n                \\]\r\n\r\n                [ ]?                        # one optional space\r\n                (?:\\n[ ]*)?                 # one optional newline followed by spaces\r\n\r\n                \\[\r\n                    (.*?)                   # id = $3\r\n                \\]\r\n            )", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_anchorInline = new Regex($"\r\n                (                           # wrap whole match in $1\r\n                    \\[\r\n                        ({GetNestedBracketsPattern()})               # link text = $2\r\n                    \\]\r\n                    \\(                      # literal paren\r\n                        [ ]*\r\n                        ({GetNestedParensPattern()})               # href = $3\r\n                        [ ]*\r\n                        (                   # $4\r\n                        (['\"])           # quote char = $5\r\n                        (.*?)               # title = $6\r\n                        \\5                  # matching quote\r\n                        [ ]*                # ignore any spaces between closing quote and )\r\n                        )?                  # title is optional\r\n                    \\)\r\n                )", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_anchorRefShortcut = new Regex("\r\n            (                               # wrap whole match in $1\r\n              \\[\r\n                 ([^\\[\\]]+)                 # link text = $2; can't contain [ or ]\r\n              \\]\r\n            )", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_imagesRef = new Regex("\r\n                    (               # wrap whole match in $1\r\n                    !\\[\r\n                        (.*?)       # alt text = $2\r\n                    \\]\r\n\r\n                    [ ]?            # one optional space\r\n                    (?:\\n[ ]*)?     # one optional newline followed by spaces\r\n\r\n                    \\[\r\n                        (.*?)       # id = $3\r\n                    \\]\r\n\r\n                    )", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_imagesInline = new Regex($"\r\n              (                     # wrap whole match in $1\r\n                !\\[\r\n                    (.*?)           # alt text = $2\r\n                \\]\r\n                \\s?                 # one optional whitespace character\r\n                \\(                  # literal paren\r\n                    [ ]*\r\n                    ({GetNestedParensPattern()})           # href = $3\r\n                    [ ]*\r\n                    (               # $4\r\n                    (['\"])       # quote char = $5\r\n                    (.*?)           # title = $6\r\n                    \\5              # matching quote\r\n                    [ ]*\r\n                    )?              # title is optional\r\n                \\)\r\n              )", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_headerSetext = new Regex("\r\n                ^(.+?)\r\n                [ ]*\r\n                \\n\r\n                (=+|-+)     # $1 = string of ='s or -'s\r\n                [ ]*\r\n                \\n+", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_headerAtx = new Regex("\r\n                ^(\\#{1,6})  # $1 = string of #'s\r\n                [ ]*\r\n                (.+?)       # $2 = Header text\r\n                [ ]*\r\n                \\#*         # optional closing #'s (not counted)\r\n                \\n+", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_horizontalRules = new Regex("\r\n            ^[ ]{0,3}         # Leading space\r\n                ([-*_])       # $1: First marker\r\n                (?>           # Repeated marker group\r\n                    [ ]{0,2}  # Zero, one, or two spaces.\r\n                    \\1        # Marker character\r\n                ){2,}         # Group repeated at least twice\r\n                [ ]*          # Trailing spaces\r\n                $             # End of line.\r\n            ", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_wholeList = string.Format("\r\n            (                               # $1 = whole list\r\n              (                             # $2\r\n                [ ]{{0,{1}}}\r\n                ({0})                       # $3 = first list item marker\r\n                [ ]+\r\n              )\r\n              (?s:.+?)\r\n              (                             # $4\r\n                  \\z\r\n                |\r\n                  \\n{{2,}}\r\n                  (?=\\S)\r\n                  (?!                       # Negative lookahead for another list item marker\r\n                    [ ]*\r\n                    {0}[ ]+\r\n                  )\r\n              )\r\n            )", string.Format("(?:{0}|{1})", "[*+-]", "\\d+[.]"), 3);
		_listNested = new Regex("^" + _wholeList, RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_listTopLevel = new Regex("(?:(?<=\\n\\n)|\\A\\n?)" + _wholeList, RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_codeBlock = new Regex(string.Format("\r\n                    (?:\\n\\n|\\A)\r\n                    (                        # $1 = the code block -- one or more lines, starting with a space\r\n                    (?:\r\n                        (?:[ ]{{{0}}})       # Lines must start with a tab-width of spaces\r\n                        .*\\n+\r\n                    )+\r\n                    )\r\n                    ((?=^[ ]{{0,{0}}}\\S)|\\Z) # Lookahead for non-space at line-start, or end of doc", 4), RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_codeSpan = new Regex("\r\n                    (?<!\\\\)   # Character before opening ` can't be a backslash\r\n                    (`+)      # $1 = Opening run of `\r\n                    (.+?)     # $2 = The code block\r\n                    (?<!`)\r\n                    \\1\r\n                    (?!`)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_bold = new Regex("(\\*\\*|__) (?=\\S) (.+?[*_]*) (?<=\\S) \\1", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_strictBold = new Regex("([\\W_]|^) (\\*\\*|__) (?=\\S) ([^\\r]*?\\S[\\*_]*) \\2 ([\\W_]|$)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_italic = new Regex("(\\*|_) (?=\\S) (.+?) (?<=\\S) \\1", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_strictItalic = new Regex("([\\W_]|^) (\\*|_) (?=\\S) ([^\\r\\*_]*?\\S) \\2 ([\\W_]|$)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		_blockquote = new Regex("\r\n            (                           # Wrap whole match in $1\r\n                (\r\n                ^[ ]*>[ ]?              # '>' at the start of a line\r\n                    .+\\n                # rest of the first line\r\n                (.+\\n)*                 # subsequent consecutive lines\r\n                \\n*                     # blanks\r\n                )+\r\n            )", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		_autolinkBare = new Regex("(^|\\s)(https?|ftp)(://[-A-Z0-9+&@#/%?=~_|\\[\\]\\(\\)!:,\\.;]*[-A-Z0-9+&@#/%=~_|\\[\\]])($|\\W)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		_outDent = new Regex("^[ ]{1," + 4 + "}", RegexOptions.Multiline | RegexOptions.Compiled);
		_codeEncoder = new Regex("&|<|>|\\\\|\\*|_|\\{|\\}|\\[|\\]", RegexOptions.Compiled);
		_amps = new Regex("&(?!(#[0-9]+)|(#[xX][a-fA-F0-9])|([a-zA-Z][a-zA-Z0-9]*);)", RegexOptions.ExplicitCapture | RegexOptions.Compiled);
		_angles = new Regex("<(?![A-Za-z/?\\$!])", RegexOptions.ExplicitCapture | RegexOptions.Compiled);
		_unescapes = new Regex("\u001a\\d+\u001a", RegexOptions.Compiled);
		_problemUrlChars = "\"'*()[]$:".ToCharArray();
		_escapeTable = new Dictionary<string, string>();
		_invertedEscapeTable = new Dictionary<string, string>();
		_backslashEscapeTable = new Dictionary<string, string>();
		string text = "";
		string text2 = "\\`*_{}[]()>#+-.!";
		for (int i = 0; i < text2.Length; i++)
		{
			string text3 = text2[i].ToString();
			string hashKey = GetHashKey(text3);
			_escapeTable.Add(text3, hashKey);
			_invertedEscapeTable.Add(hashKey, text3);
			_backslashEscapeTable.Add("\\" + text3, hashKey);
			text = text + Regex.Escape("\\" + text3) + "|";
		}
		_backslashEscapes = new Regex(text.Substring(0, text.Length - 1), RegexOptions.Compiled);
	}

	/// <summary>
	/// Transforms the provided Markdown-formatted text to HTML;  
	/// see http://en.wikipedia.org/wiki/Markdown
	/// </summary>
	/// <remarks>
	/// The order in which other subs are called here is
	/// essential. Link and image substitutions need to happen before
	/// EscapeSpecialChars(), so that any *'s or _'s in the a
	/// and img tags get encoded.
	/// </remarks>
	public string Transform(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		Setup();
		text = Normalize(text);
		text = HashHTMLBlocks(text);
		text = StripLinkDefinitions(text);
		text = RunBlockGamut(text);
		text = Unescape(text);
		Cleanup();
		return text + "\n";
	}

	/// <summary>
	/// Perform transformations that form block-level tags like paragraphs, headers, and list items.
	/// </summary>
	private string RunBlockGamut(string text)
	{
		text = DoHeaders(text);
		text = DoHorizontalRules(text);
		text = DoLists(text);
		text = DoCodeBlocks(text);
		text = DoBlockQuotes(text);
		text = HashHTMLBlocks(text);
		text = FormParagraphs(text);
		return text;
	}

	/// <summary>
	/// Perform transformations that occur *within* block-level tags like paragraphs, headers, and list items.
	/// </summary>
	private string RunSpanGamut(string text)
	{
		text = DoCodeSpans(text);
		text = EscapeSpecialCharsWithinTagAttributes(text);
		text = EscapeBackslashes(text);
		text = DoImages(text);
		text = DoAnchors(text);
		text = DoAutoLinks(text);
		text = EncodeAmpsAndAngles(text);
		text = DoItalicsAndBold(text);
		text = DoHardBreaks(text);
		return text;
	}

	/// <summary>
	/// splits on two or more newlines, to form "paragraphs";    
	/// each paragraph is then unhashed (if it is a hash) or wrapped in HTML p tag
	/// </summary>
	private string FormParagraphs(string text)
	{
		string[] array = _newlinesMultiple.Split(_newlinesLeadingTrailing.Replace(text, ""));
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].StartsWith("\u001a", StringComparison.Ordinal))
			{
				array[i] = _htmlBlocks[array[i]];
			}
			else
			{
				array[i] = _leadingWhitespace.Replace(RunSpanGamut(array[i]), "<p>") + "</p>";
			}
		}
		return string.Join("\n\n", array);
	}

	private void Setup()
	{
		_urls.Clear();
		_titles.Clear();
		_htmlBlocks.Clear();
		_listLevel = 0;
	}

	private void Cleanup()
	{
		Setup();
	}

	/// <summary>
	/// Reusable pattern to match balanced [brackets]. See Friedl's 
	/// "Mastering Regular Expressions", 2nd Ed., pp. 328-331.
	/// </summary>
	private static string GetNestedBracketsPattern()
	{
		if (_nestedBracketsPattern == null)
		{
			_nestedBracketsPattern = RepeatString("\r\n                    (?>              # Atomic matching\r\n                       [^\\[\\]]+      # Anything other than brackets\r\n                     |\r\n                       \\[\r\n                           ", 6) + RepeatString(" \\]\r\n                    )*", 6);
		}
		return _nestedBracketsPattern;
	}

	/// <summary>
	/// Reusable pattern to match balanced (parens). See Friedl's 
	/// "Mastering Regular Expressions", 2nd Ed., pp. 328-331.
	/// </summary>
	private static string GetNestedParensPattern()
	{
		if (_nestedParensPattern == null)
		{
			_nestedParensPattern = RepeatString("\r\n                    (?>              # Atomic matching\r\n                       [^()\\s]+      # Anything other than parens or whitespace\r\n                     |\r\n                       \\(\r\n                           ", 6) + RepeatString(" \\)\r\n                    )*", 6);
		}
		return _nestedParensPattern;
	}

	/// <summary>
	/// Strips link definitions from text, stores the URLs and titles in hash references.
	/// </summary>
	/// <remarks>
	/// ^[id]: url "optional title"
	/// </remarks>
	private string StripLinkDefinitions(string text)
	{
		return _linkDef.Replace(text, LinkEvaluator);
	}

	private string LinkEvaluator(Match match)
	{
		string key = match.Groups[1].Value.ToLowerInvariant();
		_urls[key] = EncodeAmpsAndAngles(match.Groups[2].Value);
		if (match.Groups[3] != null && match.Groups[3].Length > 0)
		{
			_titles[key] = match.Groups[3].Value.Replace("\"", "&quot;");
		}
		return "";
	}

	/// <summary>
	/// derived pretty much verbatim from PHP Markdown
	/// </summary>
	private static string GetBlockPattern()
	{
		string newValue = "ins|del";
		string newValue2 = "p|div|h[1-6]|blockquote|pre|table|dl|ol|ul|address|script|noscript|form|fieldset|iframe|math";
		string text = "\r\n            (?>\t\t\t\t            # optional tag attributes\r\n              \\s\t\t\t            # starts with whitespace\r\n              (?>\r\n                [^>\"/]+\t            # text outside quotes\r\n              |\r\n                /+(?!>)\t\t            # slash not followed by >\r\n              |\r\n                \"[^\"]*\"\t\t        # text inside double quotes (tolerate >)\r\n              |\r\n                '[^']*'\t                # text inside single quotes (tolerate >)\r\n              )*\r\n            )?\t\r\n            ";
		string text2 = RepeatString("\r\n                (?>\r\n                  [^<]+\t\t\t        # content without tag\r\n                |\r\n                  <\\2\t\t\t        # nested opening tag\r\n                    " + text + "       # attributes\r\n                  (?>\r\n                      />\r\n                  |\r\n                      >", 6) + ".*?" + RepeatString("\r\n                      </\\2\\s*>\t        # closing nested tag\r\n                  )\r\n                  |\t\t\t\t\r\n                  <(?!/\\2\\s*>           # other tags with a different name\r\n                  )\r\n                )*", 6);
		string newValue3 = text2.Replace("\\2", "\\3");
		string text3 = "\r\n            (?>\r\n                  (?>\r\n                    (?<=\\n)     # Starting after a blank line\r\n                    |           # or\r\n                    \\A\\n?       # the beginning of the doc\r\n                  )\r\n                  (             # save in $1\r\n\r\n                    # Match from `\\n<tag>` to `</tag>\\n`, handling nested tags \r\n                    # in between.\r\n                      \r\n                        [ ]{0,$less_than_tab}\r\n                        <($block_tags_b_re)   # start tag = $2\r\n                        $attr>                # attributes followed by > and \\n\r\n                        $content              # content, support nesting\r\n                        </\\2>                 # the matching end tag\r\n                        [ ]*                  # trailing spaces\r\n                        (?=\\n+|\\Z)            # followed by a newline or end of document\r\n\r\n                  | # Special version for tags of group a.\r\n\r\n                        [ ]{0,$less_than_tab}\r\n                        <($block_tags_a_re)   # start tag = $3\r\n                        $attr>[ ]*\\n          # attributes followed by >\r\n                        $content2             # content, support nesting\r\n                        </\\3>                 # the matching end tag\r\n                        [ ]*                  # trailing spaces\r\n                        (?=\\n+|\\Z)            # followed by a newline or end of document\r\n                      \r\n                  | # Special case just for <hr />. It was easier to make a special \r\n                    # case than to make the other regex more complicated.\r\n                  \r\n                        [ ]{0,$less_than_tab}\r\n                        <(hr)                 # start tag = $2\r\n                        $attr                 # attributes\r\n                        /?>                   # the matching end tag\r\n                        [ ]*\r\n                        (?=\\n{2,}|\\Z)         # followed by a blank line or end of document\r\n                  \r\n                  | # Special case for standalone HTML comments:\r\n                  \r\n                      [ ]{0,$less_than_tab}\r\n                      (?s:\r\n                        <!-- .*? -->\r\n                      )\r\n                      [ ]*\r\n                      (?=\\n{2,}|\\Z)            # followed by a blank line or end of document\r\n                  \r\n                  | # PHP and ASP-style processor instructions (<? and <%)\r\n                  \r\n                      [ ]{0,$less_than_tab}\r\n                      (?s:\r\n                        <([?%])                # $2\r\n                        .*?\r\n                        \\2>\r\n                      )\r\n                      [ ]*\r\n                      (?=\\n{2,}|\\Z)            # followed by a blank line or end of document\r\n                      \r\n                  )\r\n            )";
		text3 = text3.Replace("$less_than_tab", 3.ToString());
		text3 = text3.Replace("$block_tags_b_re", newValue2);
		text3 = text3.Replace("$block_tags_a_re", newValue);
		text3 = text3.Replace("$attr", text);
		text3 = text3.Replace("$content2", newValue3);
		return text3.Replace("$content", text2);
	}

	/// <summary>
	/// replaces any block-level HTML blocks with hash entries
	/// </summary>
	private string HashHTMLBlocks(string text)
	{
		return _blocksHtml.Replace(text, HtmlEvaluator);
	}

	private string HtmlEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		string hashKey = GetHashKey(value);
		_htmlBlocks[hashKey] = value;
		return "\n\n" + hashKey + "\n\n";
	}

	private static string GetHashKey(string s)
	{
		return "\u001a" + Math.Abs(s.GetHashCode()) + "\u001a";
	}

	/// <summary>
	/// returns an array of HTML tokens comprising the input string. Each token is 
	/// either a tag (possibly with nested, tags contained therein, such 
	/// as &lt;a href="&lt;MTFoo&gt;"&gt;, or a run of text between tags. Each element of the 
	/// array is a two-element array; the first is either 'tag' or 'text'; the second is 
	/// the actual value.
	/// </summary>
	private List<Token> TokenizeHTML(string text)
	{
		int num = 0;
		int num2 = 0;
		List<Token> list = new List<Token>();
		foreach (Match item in _htmlTokens.Matches(text))
		{
			num2 = item.Index;
			if (num < num2)
			{
				list.Add(new Token(TokenType.Text, text.Substring(num, num2 - num)));
			}
			list.Add(new Token(TokenType.Tag, item.Value));
			num = num2 + item.Length;
		}
		if (num < text.Length)
		{
			list.Add(new Token(TokenType.Text, text.Substring(num, text.Length - num)));
		}
		return list;
	}

	/// <summary>
	/// Turn Markdown link shortcuts into HTML anchor tags
	/// </summary>
	/// <remarks>
	/// [link text](url "title") 
	/// [link text][id] 
	/// [id] 
	/// </remarks>
	private string DoAnchors(string text)
	{
		text = _anchorRef.Replace(text, AnchorRefEvaluator);
		text = _anchorInline.Replace(text, AnchorInlineEvaluator);
		text = _anchorRefShortcut.Replace(text, AnchorRefShortcutEvaluator);
		return text;
	}

	private string AnchorRefEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		string value2 = match.Groups[2].Value;
		string text = match.Groups[3].Value.ToLowerInvariant();
		if (text == "")
		{
			text = value2.ToLowerInvariant();
		}
		if (_urls.ContainsKey(text))
		{
			string url = _urls[text];
			url = EncodeProblemUrlChars(url);
			url = EscapeBoldItalic(url);
			string text2 = "<a href=\"" + url + "\"";
			if (_titles.ContainsKey(text))
			{
				string s = _titles[text];
				s = EscapeBoldItalic(s);
				text2 = text2 + " title=\"" + s + "\"";
			}
			return text2 + ">" + value2 + "</a>";
		}
		return value;
	}

	private string AnchorRefShortcutEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		string value2 = match.Groups[2].Value;
		string key = Regex.Replace(value2.ToLowerInvariant(), "[ ]*\\n[ ]*", " ");
		if (_urls.ContainsKey(key))
		{
			string url = _urls[key];
			url = EncodeProblemUrlChars(url);
			url = EscapeBoldItalic(url);
			string text = "<a href=\"" + url + "\"";
			if (_titles.ContainsKey(key))
			{
				string s = _titles[key];
				s = EscapeBoldItalic(s);
				text = text + " title=\"" + s + "\"";
			}
			return text + ">" + value2 + "</a>";
		}
		return value;
	}

	private string AnchorInlineEvaluator(Match match)
	{
		string value = match.Groups[2].Value;
		string value2 = match.Groups[3].Value;
		string value3 = match.Groups[6].Value;
		value2 = EncodeProblemUrlChars(value2);
		value2 = EscapeBoldItalic(value2);
		if (value2.StartsWith("<", StringComparison.Ordinal) && value2.EndsWith(">", StringComparison.Ordinal))
		{
			value2 = value2.Substring(1, value2.Length - 2);
		}
		string text = $"<a href=\"{value2}\"";
		if (!string.IsNullOrEmpty(value3))
		{
			value3 = value3.Replace("\"", "&quot;");
			value3 = EscapeBoldItalic(value3);
			text += $" title=\"{value3}\"";
		}
		return text + $">{value}</a>";
	}

	/// <summary>
	/// Turn Markdown image shortcuts into HTML img tags. 
	/// </summary>
	/// <remarks>
	/// ![alt text][id]
	/// ![alt text](url "optional title")
	/// </remarks>
	private string DoImages(string text)
	{
		text = _imagesRef.Replace(text, ImageReferenceEvaluator);
		text = _imagesInline.Replace(text, ImageInlineEvaluator);
		return text;
	}

	private string ImageReferenceEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		string value2 = match.Groups[2].Value;
		string text = match.Groups[3].Value.ToLowerInvariant();
		if (text == "")
		{
			text = value2.ToLowerInvariant();
		}
		value2 = value2.Replace("\"", "&quot;");
		if (_urls.ContainsKey(text))
		{
			string url = _urls[text];
			url = EncodeProblemUrlChars(url);
			url = EscapeBoldItalic(url);
			string text2 = $"<img src=\"{url}\" alt=\"{value2}\"";
			if (_titles.ContainsKey(text))
			{
				string s = _titles[text];
				s = EscapeBoldItalic(s);
				text2 += $" title=\"{s}\"";
			}
			return text2 + Options.EmptyElementSuffix;
		}
		return value;
	}

	private string ImageInlineEvaluator(Match match)
	{
		string value = match.Groups[2].Value;
		string text = match.Groups[3].Value;
		string value2 = match.Groups[6].Value;
		value = value.Replace("\"", "&quot;");
		value2 = value2.Replace("\"", "&quot;");
		if (text.StartsWith("<", StringComparison.Ordinal) && text.EndsWith(">", StringComparison.Ordinal))
		{
			text = text.Substring(1, text.Length - 2);
		}
		text = EncodeProblemUrlChars(text);
		text = EscapeBoldItalic(text);
		string text2 = $"<img src=\"{text}\" alt=\"{value}\"";
		if (!string.IsNullOrEmpty(value2))
		{
			value2 = EscapeBoldItalic(value2);
			text2 += $" title=\"{value2}\"";
		}
		return text2 + Options.EmptyElementSuffix;
	}

	/// <summary>
	/// Turn Markdown headers into HTML header tags
	/// </summary>
	/// <remarks>
	/// Header 1  
	/// ========  
	///
	/// Header 2  
	/// --------  
	///
	/// # Header 1  
	/// ## Header 2  
	/// ## Header 2 with closing hashes ##  
	/// ...  
	/// ###### Header 6  
	/// </remarks>
	private string DoHeaders(string text)
	{
		text = _headerSetext.Replace(text, SetextHeaderEvaluator);
		text = _headerAtx.Replace(text, AtxHeaderEvaluator);
		return text;
	}

	private string SetextHeaderEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		int num = (match.Groups[2].Value.StartsWith("=", StringComparison.Ordinal) ? 1 : 2);
		return string.Format("<h{1}>{0}</h{1}>\n\n", RunSpanGamut(value), num);
	}

	private string AtxHeaderEvaluator(Match match)
	{
		string value = match.Groups[2].Value;
		int length = match.Groups[1].Value.Length;
		return string.Format("<h{1}>{0}</h{1}>\n\n", RunSpanGamut(value), length);
	}

	/// <summary>
	/// Turn Markdown horizontal rules into HTML hr tags
	/// </summary>
	/// <remarks>
	/// ***  
	/// * * *  
	/// ---
	/// - - -
	/// </remarks>
	private string DoHorizontalRules(string text)
	{
		return _horizontalRules.Replace(text, "<hr" + Options.EmptyElementSuffix + "\n");
	}

	/// <summary>
	/// Turn Markdown lists into HTML ul and ol and li tags
	/// </summary>
	private string DoLists(string text)
	{
		text = ((_listLevel <= 0) ? _listTopLevel.Replace(text, ListEvaluator) : _listNested.Replace(text, ListEvaluator));
		return text;
	}

	private string ListEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		string text = (Regex.IsMatch(match.Groups[3].Value, "[*+-]") ? "ul" : "ol");
		value = Regex.Replace(value, "\\n{2,}", "\n\n\n");
		string arg = ProcessListItems(value, (text == "ul") ? "[*+-]" : "\\d+[.]");
		return string.Format("<{0}>\n{1}</{0}>\n", text, arg);
	}

	/// <summary>
	/// Process the contents of a single ordered or unordered list, splitting it
	/// into individual list items.
	/// </summary>
	private string ProcessListItems(string list, string marker)
	{
		_listLevel++;
		list = Regex.Replace(list, "\\n{2,}\\z", "\n");
		string pattern = string.Format("(\\n)?                      # leading line = $1\r\n                (^[ ]*)                    # leading whitespace = $2\r\n                ({0}) [ ]+                 # list marker = $3\r\n                ((?s:.+?)                  # list item text = $4\r\n                (\\n{{1,2}}))      \r\n                (?= \\n* (\\z | \\2 ({0}) [ ]+))", marker);
		list = Regex.Replace(list, pattern, ListItemEvaluator, RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
		_listLevel--;
		return list;
	}

	private string ListItemEvaluator(Match match)
	{
		string value = match.Groups[4].Value;
		string value2 = match.Groups[1].Value;
		if (!string.IsNullOrEmpty(value2) || Regex.IsMatch(value, "\\n{2,}"))
		{
			value = RunBlockGamut(Outdent(value) + "\n");
		}
		else
		{
			value = DoLists(Outdent(value));
			value = value.TrimEnd(new char[1] { '\n' });
			value = RunSpanGamut(value);
		}
		return $"<li>{value}</li>\n";
	}

	/// <summary>
	/// /// Turn Markdown 4-space indented code into HTML pre code blocks
	/// </summary>
	private string DoCodeBlocks(string text)
	{
		text = _codeBlock.Replace(text, CodeBlockEvaluator);
		return text;
	}

	private string CodeBlockEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		value = EncodeCode(Outdent(value));
		value = _newlinesLeadingTrailing.Replace(value, "");
		return "\n\n<pre><code>" + value + "\n</code></pre>\n\n";
	}

	/// <summary>
	/// Turn Markdown `code spans` into HTML code tags
	/// </summary>
	private string DoCodeSpans(string text)
	{
		return _codeSpan.Replace(text, CodeSpanEvaluator);
	}

	private string CodeSpanEvaluator(Match match)
	{
		string value = match.Groups[2].Value;
		value = Regex.Replace(value, "^[ ]*", "");
		value = Regex.Replace(value, "[ ]*$", "");
		value = EncodeCode(value);
		return "<code>" + value + "</code>";
	}

	/// <summary>
	/// Turn Markdown *italics* and **bold** into HTML strong and em tags
	/// </summary>
	private string DoItalicsAndBold(string text)
	{
		if (Options.StrictBoldItalic)
		{
			text = _strictBold.Replace(text, "$1<strong>$3</strong>$4");
			text = _strictItalic.Replace(text, "$1<em>$3</em>$4");
		}
		else
		{
			text = _bold.Replace(text, "<strong>$2</strong>");
			text = _italic.Replace(text, "<em>$2</em>");
		}
		return text;
	}

	/// <summary>
	/// Turn markdown line breaks (two space at end of line) into HTML break tags
	/// </summary>
	private string DoHardBreaks(string text)
	{
		text = ((!Options.AutoNewLines) ? Regex.Replace(text, " {2,}\\n", $"<br{Options.EmptyElementSuffix}\n") : Regex.Replace(text, "\\n", $"<br{Options.EmptyElementSuffix}\n"));
		return text;
	}

	/// <summary>
	/// Turn Markdown &gt; quoted blocks into HTML blockquote blocks
	/// </summary>
	private string DoBlockQuotes(string text)
	{
		return _blockquote.Replace(text, BlockQuoteEvaluator);
	}

	private string BlockQuoteEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		value = Regex.Replace(value, "^[ ]*>[ ]?", "", RegexOptions.Multiline);
		value = Regex.Replace(value, "^[ ]+$", "", RegexOptions.Multiline);
		value = RunBlockGamut(value);
		value = Regex.Replace(value, "^", "  ", RegexOptions.Multiline);
		value = Regex.Replace(value, "(\\s*<pre>.+?</pre>)", BlockQuoteEvaluator2, RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		return $"<blockquote>\n{value}\n</blockquote>\n\n";
	}

	private string BlockQuoteEvaluator2(Match match)
	{
		return Regex.Replace(match.Groups[1].Value, "^  ", "", RegexOptions.Multiline);
	}

	/// <summary>
	/// Turn angle-delimited URLs into HTML anchor tags
	/// </summary>
	/// <remarks>
	/// &lt;http://www.example.com&gt;
	/// </remarks>
	private string DoAutoLinks(string text)
	{
		if (Options.AutoHyperlink)
		{
			text = _autolinkBare.Replace(text, "$1<$2$3>$4");
		}
		text = Regex.Replace(text, "<((https?|ftp):[^'\">\\s]+)>", HyperlinkEvaluator);
		if (Options.LinkEmails)
		{
			string pattern = "<\r\n                      (?:mailto:)?\r\n                      (\r\n                        [-.\\w]+\r\n                        \\@\r\n                        [-a-z0-9]+(\\.[-a-z0-9]+)*\\.[a-z]+\r\n                      )\r\n                      >";
			text = Regex.Replace(text, pattern, EmailEvaluator, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		}
		return text;
	}

	private string HyperlinkEvaluator(Match match)
	{
		string value = match.Groups[1].Value;
		return string.Format("<a href=\"{0}\">{0}</a>", value);
	}

	private string EmailEvaluator(Match match)
	{
		string text = Unescape(match.Groups[1].Value);
		text = "mailto:" + text;
		text = EncodeEmailAddress(text);
		text = string.Format("<a href=\"{0}\">{0}</a>", text);
		return Regex.Replace(text, "\">.+?:", "\">");
	}

	/// <summary>
	/// Remove one level of line-leading spaces
	/// </summary>
	private string Outdent(string block)
	{
		return _outDent.Replace(block, "");
	}

	/// <summary>
	/// encodes email address randomly  
	/// roughly 10% raw, 45% hex, 45% dec 
	/// note that @ is always encoded and : never is
	/// </summary>
	private string EncodeEmailAddress(string addr)
	{
		StringBuilder stringBuilder = new StringBuilder(addr.Length * 5);
		Random random = new Random();
		foreach (char c in addr)
		{
			int num = random.Next(1, 100);
			if ((num > 90 || c == ':') && c != '@')
			{
				stringBuilder.Append(c);
			}
			else if (num < 45)
			{
				stringBuilder.AppendFormat("&#x{0:x};", (int)c);
			}
			else
			{
				stringBuilder.AppendFormat("&#{0};", (int)c);
			}
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// Encode/escape certain Markdown characters inside code blocks and spans where they are literals
	/// </summary>
	private string EncodeCode(string code)
	{
		return _codeEncoder.Replace(code, EncodeCodeEvaluator);
	}

	private string EncodeCodeEvaluator(Match match)
	{
		return match.Value switch
		{
			"&" => "&amp;", 
			"<" => "&lt;", 
			">" => "&gt;", 
			_ => _escapeTable[match.Value], 
		};
	}

	/// <summary>
	/// Encode any ampersands (that aren't part of an HTML entity) and left or right angle brackets
	/// </summary>
	private string EncodeAmpsAndAngles(string s)
	{
		s = _amps.Replace(s, "&amp;");
		s = _angles.Replace(s, "&lt;");
		return s;
	}

	/// <summary>
	/// Encodes any escaped characters such as \`, \*, \[ etc
	/// </summary>
	private string EscapeBackslashes(string s)
	{
		return _backslashEscapes.Replace(s, EscapeBackslashesEvaluator);
	}

	private string EscapeBackslashesEvaluator(Match match)
	{
		return _backslashEscapeTable[match.Value];
	}

	/// <summary>
	/// swap back in all the special characters we've hidden
	/// </summary>
	private string Unescape(string s)
	{
		return _unescapes.Replace(s, UnescapeEvaluator);
	}

	private string UnescapeEvaluator(Match match)
	{
		return _invertedEscapeTable[match.Value];
	}

	/// <summary>
	/// escapes Bold [ * ] and Italic [ _ ] characters
	/// </summary>
	private string EscapeBoldItalic(string s)
	{
		s = s.Replace("*", _escapeTable["*"]);
		s = s.Replace("_", _escapeTable["_"]);
		return s;
	}

	/// <summary>
	/// hex-encodes some unusual "problem" chars in URLs to avoid URL detection problems 
	/// </summary>
	private string EncodeProblemUrlChars(string url)
	{
		if (!Options.EncodeProblemUrlCharacters)
		{
			return url;
		}
		StringBuilder stringBuilder = new StringBuilder(url.Length);
		for (int i = 0; i < url.Length; i++)
		{
			char c = url[i];
			bool flag = Array.IndexOf(_problemUrlChars, c) != -1;
			if (flag && c == ':' && i < url.Length - 1)
			{
				flag = url[i + 1] != '/' && (url[i + 1] < '0' || url[i + 1] > '9');
			}
			if (flag)
			{
				stringBuilder.Append("%" + $"{(byte)c:x}");
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// Within tags -- meaning between &lt; and &gt; -- encode [\ ` * _] so they 
	/// don't conflict with their use in Markdown for code, italics and strong. 
	/// We're replacing each such character with its corresponding hash 
	/// value; this is likely overkill, but it should prevent us from colliding 
	/// with the escape values by accident.
	/// </summary>
	private string EscapeSpecialCharsWithinTagAttributes(string text)
	{
		List<Token> list = TokenizeHTML(text);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (Token item in list)
		{
			string text2 = item.Value;
			if (item.Type == TokenType.Tag)
			{
				text2 = text2.Replace("\\", _escapeTable["\\"]);
				text2 = Regex.Replace(text2, "(?<=.)</?code>(?=.)", _escapeTable["`"]);
				text2 = EscapeBoldItalic(text2);
			}
			stringBuilder.Append(text2);
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// convert all tabs to _tabWidth spaces; 
	/// standardizes line endings from DOS (CR LF) or Mac (CR) to UNIX (LF); 
	/// makes sure text ends with a couple of newlines; 
	/// removes any blank lines (only spaces) in the text
	/// </summary>
	private string Normalize(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		StringBuilder stringBuilder2 = new StringBuilder();
		bool flag = false;
		for (int i = 0; i < text.Length; i++)
		{
			switch (text[i])
			{
			case '\n':
				if (flag)
				{
					stringBuilder.Append((object?)stringBuilder2);
				}
				stringBuilder.Append('\n');
				stringBuilder2.Length = 0;
				flag = false;
				break;
			case '\r':
				if (i < text.Length - 1 && text[i + 1] != '\n')
				{
					if (flag)
					{
						stringBuilder.Append((object?)stringBuilder2);
					}
					stringBuilder.Append('\n');
					stringBuilder2.Length = 0;
					flag = false;
				}
				break;
			case '\t':
			{
				int num = 4 - stringBuilder2.Length % 4;
				for (int j = 0; j < num; j++)
				{
					stringBuilder2.Append(' ');
				}
				break;
			}
			default:
				if (!flag && text[i] != ' ')
				{
					flag = true;
				}
				stringBuilder2.Append(text[i]);
				break;
			case '\u001a':
				break;
			}
		}
		if (flag)
		{
			stringBuilder.Append((object?)stringBuilder2);
		}
		stringBuilder.Append('\n');
		return stringBuilder.Append("\n\n").ToString();
	}

	/// <summary>
	/// this is to emulate what's evailable in PHP
	/// </summary>
	private static string RepeatString(string text, int count)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length * count);
		for (int i = 0; i < count; i++)
		{
			stringBuilder.Append(text);
		}
		return stringBuilder.ToString();
	}
}
public class MarkdownOptions : IMarkdownOptions
{
	/// <summary>
	/// when true, (most) bare plain URLs are auto-hyperlinked  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	public bool AutoHyperlink { get; set; }

	/// <summary>
	/// when true, RETURN becomes a literal newline  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	public bool AutoNewLines { get; set; }

	/// <summary>
	/// use "&gt;" for HTML output, or " /&gt;" for XHTML output
	/// </summary>
	public string EmptyElementSuffix { get; set; }

	/// <summary>
	/// when true, problematic URL characters like [, ], (, and so forth will be encoded 
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	public bool EncodeProblemUrlCharacters { get; set; }

	/// <summary>
	/// when false, email addresses will never be auto-linked  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	public bool LinkEmails { get; set; }

	/// <summary>
	/// when true, bold and italic require non-word characters on either side  
	/// WARNING: this is a significant deviation from the markdown spec
	/// </summary>
	public bool StrictBoldItalic { get; set; }

	public MarkdownOptions()
	{
		AutoHyperlink = false;
		AutoNewLines = false;
		EmptyElementSuffix = " />";
		EncodeProblemUrlCharacters = false;
		LinkEmails = true;
		StrictBoldItalic = false;
	}
}

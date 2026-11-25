# Legacy text rendering parity (Markdown + HtmlSanitizer)

Corpus `corpus.tsv`: 55829 cases; byte-equal **55810**, different **19**, missing in the legacy output 0; 0 cases fail identically in both (exception type and message)

| Kind | Byte-equal | Different |
|---|---:|---:|
| event comments (baseline data) | 45649 | 0 |
| golden journey texts | 44 | 0 |
| ticket details (baseline data) | 10000 | 0 |
| tricky inputs | 117 | 19 |

Rendering components of the new app:

- MarkdownSharp 1.13: ported source (`src/modern/Modules/TicketDesk.Tickets/Rendering/MarkdownSharp.cs`)
- `HtmlSanitizer.dll` 11.0.0.0 (package), AngleSharp 1.10.0.0

Differences by class (`differences.tsv` lists each):

| Class | Cases |
|---|---:|
| same DOM: URL serialization (System.Uri form: trailing slash on a bare host, percent-encoded non-ASCII and <>) | 11 |
| same DOM: style serialization (colors as rgba(), zero lengths without unit) | 8 |

Outside the known classes: **0**

First differences:

- `x011-md` (isHtml=0): legacy OK `<p><a href="http://example.com/" title="Title">link</a> <a href="http://example.com/ref" title="Ref title">ref</a> <a href="https://example.org/">implicit</a></…` / new OK `<p><a href="http://example.com" title="Title">link</a> <a href="http://example.com/ref" title="Ref title">ref</a> <a href="https://example.org">implicit</a></p>…`
- `x018-md` (isHtml=0): legacy OK `<p>URL with problem chars: [x](http://example.com/a b?c=d e) and <a href="http://example.com/%C3%BC">y</a></p>\n` / new OK `<p>URL with problem chars: [x](http://example.com/a b?c=d e) and <a href="http://example.com/ü">y</a></p>\n`
- `x033-md` (isHtml=0): legacy OK `<p><a href="http://example.com/" target="_blank" rel="nofollow" title="t">ok</a></p>\n` / new OK `<p><a href="http://example.com" target="_blank" rel="nofollow" title="t">ok</a></p>\n`
- `x033-html` (isHtml=1): legacy OK `<a href="http://example.com/" target="_blank" rel="nofollow" title="t">ok</a>` / new OK `<a href="http://example.com" target="_blank" rel="nofollow" title="t">ok</a>`
- `x035-md` (isHtml=0): legacy OK `<div style="color: red">styled</div>\n` / new OK `<div style="color: rgba(255, 0, 0, 1)">styled</div>\n`
- `x035-html` (isHtml=1): legacy OK `<div style="color: red">styled</div>` / new OK `<div style="color: rgba(255, 0, 0, 1)">styled</div>`
- `x036-md` (isHtml=0): legacy OK `<p style="font-weight: bold; text-align: center; margin: 0px 0px 0px 40px">p</p>\n` / new OK `<p style="font-weight: bold; text-align: center; margin: 0 0 0 40px">p</p>\n`
- `x036-html` (isHtml=1): legacy OK `<p style="font-weight: bold; text-align: center; margin: 0px 0px 0px 40px">p</p>` / new OK `<p style="font-weight: bold; text-align: center; margin: 0 0 0 40px">p</p>`
- `x037-md` (isHtml=0): legacy OK `<p><span style="color: rgb(255, 0, 0); font-family: &quot;Arial&quot;">rgb</span></p>\n` / new OK `<p><span style="color: rgba(255, 0, 0, 1); font-family: &quot;Arial&quot;">rgb</span></p>\n`
- `x037-html` (isHtml=1): legacy OK `<span style="color: rgb(255, 0, 0); font-family: &quot;Arial&quot;">rgb</span>` / new OK `<span style="color: rgba(255, 0, 0, 1); font-family: &quot;Arial&quot;">rgb</span>`
- `x040-md` (isHtml=0): legacy OK `<form action="http://evil/"><input name="a" value="b"><button>go</button><textarea>t</textarea><select><option>o</option></select></form>\n` / new OK `<form action="http://evil"><input name="a" value="b"><button>go</button><textarea>t</textarea><select><option>o</option></select></form>\n`
- `x040-html` (isHtml=1): legacy OK `<form action="http://evil/"><input name="a" value="b"><button>go</button><textarea>t</textarea><select><option>o</option></select></form>` / new OK `<form action="http://evil"><input name="a" value="b"><button>go</button><textarea>t</textarea><select><option>o</option></select></form>`
- `x053-md` (isHtml=0): legacy OK `<p></p><p>Summernote <b>bold</b> <i>it</i> <u>u</u> <strike>s</strike> <sup>1</sup><sub>2</sub></p><p><span style="background-color: rgb(255, 255, 0)">hl</span>…` / new OK `<p></p><p>Summernote <b>bold</b> <i>it</i> <u>u</u> <strike>s</strike> <sup>1</sup><sub>2</sub></p><p><span style="background-color: rgba(255, 255, 0, 1)">hl</s…`
- `x053-html` (isHtml=1): legacy OK `<p>Summernote <b>bold</b> <i>it</i> <u>u</u> <strike>s</strike> <sup>1</sup><sub>2</sub></p><p><span style="background-color: rgb(255, 255, 0)">hl</span></p>` / new OK `<p>Summernote <b>bold</b> <i>it</i> <u>u</u> <strike>s</strike> <sup>1</sup><sub>2</sub></p><p><span style="background-color: rgba(255, 255, 0, 1)">hl</span></p…`
- `x056-md` (isHtml=0): legacy OK `<p><a href="http://example.com/?q=%3Cscript%3E">enc</a><img src="http://example.com/x.png?a=1&amp;b=2"></p>\n` / new OK `<p><a href="http://example.com/?q=&lt;script&gt;">enc</a><img src="http://example.com/x.png?a=1&amp;b=2"></p>\n`
- `x056-html` (isHtml=1): legacy OK `<a href="http://example.com/?q=%3Cscript%3E">enc</a><img src="http://example.com/x.png?a=1&amp;b=2">` / new OK `<a href="http://example.com/?q=&lt;script&gt;">enc</a><img src="http://example.com/x.png?a=1&amp;b=2">`
- `x060-md` (isHtml=0): legacy OK `<p><a href="http://example.com/"><img src="http://example.com/i.png"></a></p>\n` / new OK `<p><a href="http://example.com"><img src="http://example.com/i.png"></a></p>\n`
- `x060-html` (isHtml=1): legacy OK `<a href="http://example.com/"><img src="http://example.com/i.png"></a>` / new OK `<a href="http://example.com"><img src="http://example.com/i.png"></a>`
- `x067-md` (isHtml=0): legacy OK `<p>Mixed <b>html</b> and <strong>markdown</strong> with <a href="http://example.com/">http://example.com</a></p>\n` / new OK `<p>Mixed <b>html</b> and <strong>markdown</strong> with <a href="http://example.com">http://example.com</a></p>\n`

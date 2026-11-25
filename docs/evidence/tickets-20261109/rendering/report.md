# Legacy text rendering parity (Markdown + HtmlSanitizer)

Corpus `corpus.tsv`: 55829 cases; byte-equal **55829**, different **0**, missing in the legacy output 0; 0 cases fail identically in both (exception type and message)

| Kind | Byte-equal | Different |
|---|---:|---:|
| event comments (baseline data) | 45649 | 0 |
| golden journey texts | 44 | 0 |
| ticket details (baseline data) | 10000 | 0 |
| tricky inputs | 136 | 0 |

Assemblies used by the new app (SHA-256):

- MarkdownSharp 1.13: ported source (`src/modern/Modules/TicketDesk.Tickets/Rendering/MarkdownSharp.cs`)
- `HtmlSanitizer.dll` 4252ee0a8795ee777b774aa3955d731af7b2800edb3e0c852eedc6365fccd5a9
- `AngleSharp.dll` 5ed2683bed6007c3831c57e80c44027db4d9a9a4859ac6b53354c74a1a0e402e

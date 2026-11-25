# Ticket text sanitizer switch (plan/DECISIONS.md 2026-11-10)

The new app no longer ships HtmlSanitizer 3.2.103 / AngleSharp 0.9.5 (legacy parity, 4 advisories, hidden from NuGet audit by
`PackageDownload`). It uses **HtmlSanitizer 11.0.26** (AngleSharp 1.10.0, AngleSharp.Css 1.4.0) as a normal package,
configured with the 3.2.103 default allow-lists (`LegacyTextRenderer.LegacyAllowList`, read from the old DLL). MarkdownSharp
stays the byte-equal port.

- `report.md`, `differences.tsv`: old (legacy DLLs on the VM, `../../tickets-20261109/rendering/legacy-out.tsv.xz`) vs new over the
  same 55,829-text corpus: **55,810 byte-equal, 19 different** (all tricky inputs; 0 of 10,000 ticket details, 45,649 event
  comments, 44 golden texts). Every difference keeps the DOM (same elements, attribute names, text): 11 URL attribute values
  (3.2 wrote the System.Uri form: trailing slash on a bare host, `%C3%BC`, `%3Cscript%3E`; 11.x keeps the value as written), 8
  style values (11.x/AngleSharp.Css: `rgba(255, 0, 0, 1)` for `red` and `rgb(...)`, `0` for `0px`). No construct is newly
  dropped or newly kept. Expected difference **R1**.
- `vulnerable-packages.txt`: `dotnet list package --vulnerable --include-transitive` over the solution: no vulnerable package
  in any of the 23 projects. `packages.txt`: the sanitizer packages the web app resolves.

```bash
xz -dk ../../tickets-20261109/rendering/corpus.tsv.xz ../../tickets-20261109/rendering/legacy-out.tsv.xz
dotnet run ops/dev/render-parity/render-parity.cs -- corpus.tsv legacy-out.tsv OUT     # exit 1 if a difference is outside the known classes
dotnet list src/modern/TicketDesk.Modern.sln package --vulnerable --include-transitive
python3 -I ops/dev/replay-summary.py --self-test                                       # the R1 equivalence rules
```

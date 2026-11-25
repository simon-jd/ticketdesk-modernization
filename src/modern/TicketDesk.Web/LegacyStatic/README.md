# Legacy static files

Byte-identical copies of the plain static files of the legacy web project (`TicketDesk/TicketDesk.Web.Client`) that the
migrated pages still reference: the fonts and images the bundled CSS points at, Dropzone's default thumbnail, `favicon.ico`
and `robots.txt`. They are embedded in `TicketDesk.Web` and served at the legacy URLs by `Legacy/LegacyStaticFiles.cs`.
During coexistence the YARP fallback fetched them from the legacy app; they moved here when the legacy app was retired
(Round 4). Nothing else of the legacy `Content/`, `Scripts/` or `fonts/` folders is referenced by the new app's pages (the
scripts and styles come from the bundle snapshot in `LegacyBundles/`).

| Files | Origin | License |
|---|---|---|
| `fonts/glyphicons-halflings-regular.*` | Bootstrap 3.3 (Glyphicons Halflings) | MIT |
| `fonts/fontawesome-webfont.*` | Font Awesome 4.4.0 (font) | SIL OFL 1.1 |
| `Content/css/select2*.png`, `select2-spinner.gif` | Select2 3.5 | Apache 2.0 |
| `Content/Images/*`, `favicon.ico`, `robots.txt` | TicketDesk 2.5 | MS-PL (`LICENSE.txt`) |

Refresh: copy the same file names from `TicketDesk/TicketDesk.Web.Client/{fonts,Content}` (the legacy tree is frozen).

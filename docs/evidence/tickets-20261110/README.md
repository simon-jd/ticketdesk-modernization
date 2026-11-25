# Round 2b tickets follow-up (2026-11-10): sanitizer switch, container, replay

| Folder | What |
|---|---|
| `sanitizer/` | HtmlSanitizer 11.0.26 with the legacy allow-lists vs the legacy 3.2.103 output over 55,829 texts: 55,810 byte-equal, 19 different (11 URL, 8 style serialization; same DOM) = R1; `dotnet list package --vulnerable --include-transitive`: clean |
| `replay-waves-0-4/` | golden through the gateway, waves 0–4, remote auth, `TD_MODERN_DB=vm`: 262 compared, 199 matched, 64 explained (S3 55, S3+S5 3, G6 3, L2 2, L1 1), **0 unexplained**; R1 not needed (no golden text renders differently); rows written equal to the 2026-11-09 run (`db-after-gateway.json` vs `../tickets-20261109/replay-step2-wave4/`) |
| `docker/` | image `ticketdesk-web:r2b-tickets` 272,732,451 bytes; runs as UID 1654; signed in through it, `/ticket/1` 200 from the new app (`container-smoke.txt`, `ops/dev/container-smoke.sh`) |
| `tests/` | solution tests: 654 passed, 1 failed (identity's V0010 table on the shared td-sql database) of 655 |

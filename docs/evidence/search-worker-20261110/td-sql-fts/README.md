# td-sql with Full-Text Search (round 3, search-worker)

The shared dev/test container td-sql now runs `ticketdesk/td-sql:2022-fts-16.0.4262.2`: the official
`mcr.microsoft.com/mssql/server` 2022 image it ran before (same digest `fc010e5c…`, CU25 16.0.4262.2) plus the
`mssql-server-fts` package of the same build from packages.microsoft.com (`ops/dev/td-sql-image/Dockerfile`; the package is
unpacked, not apt-installed, because apt would also pull the newest `mssql-server` over `/opt/mssql`).

    ops/dev/td-sql.sh build-image     # docker build of ops/dev/td-sql-image
    ops/dev/td-sql.sh recreate        # stop, keep the old container as td-sql-previous, same name/port/volume/env, check

| | before (`before.txt`) | after (`after.txt`) |
|---|---|---|
| image | mcr 2022-latest `fc010e5c…` | ticketdesk/td-sql:2022-fts-16.0.4262.2 `a4671988…` (3.22 GB) |
| engine | 16.0.4262.2 CU25 Developer | same |
| full-text installed | 0 | 1 |
| databases | TicketDesk, TicketDeskL, TicketDeskSynth (ONLINE) | same, same row counts and journals |

Name `td-sql`, port 127.0.0.1:14333, volume `td-sql-data`, env file, 10 GB memory limit and restart policy unchanged
(`after.txt`). Recreate took 65 s (`recreate.txt`; the clean stop waits for SQL Server's checkpoint). After the swap the
whole solution test suite passed against it: 685/685 (`$W/logs/search-worker/fulltest-1.txt`, run 2026-11-10 17:25Z).
Rollback: `docker rm -f td-sql && docker rename td-sql-previous td-sql && docker start td-sql`.

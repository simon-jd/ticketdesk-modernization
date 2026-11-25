# Restore drill on kind against td-sql (Round 6)

Full backup at +20 s, log backups every 30 s, loss (all sessions killed, last committed write read, DROP DATABASE) 17 s after the 4th log backup, restore to the point of the loss (STOPAT), app switched through a values file, server-page smoke every 2 s until it passes. Times in seconds.

| Run | Loss (UTC) | Loss after the last log backup | Restore | Switch (helm --wait) | Smoke attempts | RTO (loss -> smoke passed) | RPO (last committed - last restored write) | Last event committed / restored | Writes lost (events) |
|---|---|---:|---:|---:|---|---:|---:|---|---:|
| run-1 | 2026-11-14T05:50:49.439Z | 17.0 | 1.3 | 4.0 | 1 (FAILED) | not measured | 17.1 | 46609 / 46491 | 118 |
| run-2 | 2026-11-14T05:58:05.704Z | 17.0 | 1.2 | 4.2 | 2 (passed) | 16.7 | 17.4 | 46611 / 46492 | 119 |
| run-3 | 2026-11-14T06:05:20.866Z | 17.0 | 1.3 | 4.0 | 2 (passed) | 19.3 | 17.3 | 46613 / 46492 | 121 |
| run-4 | 2026-11-14T06:12:37.410Z | 17.0 | 1.3 | 4.0 | 2 (passed) | 16.9 | 17.1 | 46608 / 46487 | 121 |

RTO over 3 measured runs: median **16.9 s** (min 16.7, max 19.3); RPO over 4 runs: median **17.2 s** (min 17.1, max 17.4); writes lost 118-121 events.

run-1: RTO not measured in run 1: the one smoke attempt, 4.0 s after helm upgrade --wait returned, signed in on the old pod (still on the dropped database during its preStop sleep) and got HTTP 500; the drill retries the smoke from run 2 on. 17.9 s = loss -> that failed smoke. The restore and the RPO are measured.

# Round 6 load: capacity, failure drills, restore drill (2026-11-14, all local)

Branch `ws/load` from `modernize` @ `9675f3e`. No Azure. Scripts: `ops/load/` (capacity.sh, sample.py, timeline.py,
analyze.py, diag.sh, scale-l.sh, drills.sh, drill_report.py, drills_summary.py, restore_summary.py, signin_all.py,
vm-counters.ps1, trim-evidence.sh); the k6 mix `tests/perf/ticketdesk-mix.js` (new options TD_PRE_VUS / TD_MAX_VUS /
TD_REQ_TIMEOUT / TD_SETUP_SIGNIN / TD_LIST_API, defaults unchanged); `ops/dev/serve-modern.sh --app-server`,
`ops/dev/smtp-sink.py --headers-only --hold-file`.
Raw request data: `requests.csv.xz` (one line per measured request: ms since start, operation, status, duration,
expected) for the drills, the restore runs and capacity repeat 3 of each system; every step keeps `timeline.csv` (per
second), `summary.json` (k6's summary) and `samples.jsonl.xz` (resources every 2 s).

## Headline (conditions below)

| Measurement | Result |
|---|---|
| Capacity at scale S, 4 E-cores each for app + SQL Server Express, k6 open model, Round 1 mix | sustained in every repeat: legacy **10 ops/s** (19.5 req/s, p95 100–108 ms), new app **150 ops/s** (291 req/s, p95 26–57 ms); legacy passed 20–30 ops/s only in some repeats, the new app failed 200 in all 3 |
| Knee, legacy | CLR thread-pool starvation stalls (CPU ~5 %, threads climbing, 30 s timeouts) from 20 ops/s; with more minimum threads 40 ops/s passes at 89 % CPU |
| Knee, new app | page-lock deadlocks/convoys on dbo.Tickets (server-page ticket center's COUNT vs ticket writes) filling the 100-connection pool at 200 ops/s with 60 % CPU; with READ_COMMITTED_SNAPSHOT 200 passes and 250 ends on CPU |
| New app alone at scale L (1M tickets) | server-page ticket center fails at 5 ops/s (5 s lists, memory-grant waits); through `/api/tickets` (the Angular page's list) 50 ops/s passes, 100 ends on CPU |
| Drills on kind at 75 ops/s | pod kill: 34 failed, recovered 6.0 s (2 replicas: 0 failed); DB egress cut 60 s: last 5xx 20 s after the cut ended; SQL restart: 690 × 500 over 5 s, back 36 s after the command; worker killed mid-send ×3: 0 lost, 3 duplicated (at least once) |
| Restore drill on kind against td-sql (full + 30 s log backups, DROP DATABASE) | RTO **16.9 s** median (16.7–19.3, 3 runs), RPO **17.2 s** median (17.1–17.4, 4 runs) = time since the last log backup |

## Load and mix

k6 v2.3.0, `constant-arrival-rate` (open model: the arrival rate does not slow down when the system does; a late
iteration that finds no free VU is counted as *dropped*). The operation mix is the Round 1 baseline mix (read-heavy
help-desk day, `tests/perf/ticketdesk-mix.js`): list 35 %, view 35 % (page + 2 AJAX), filter 15 %, comment 10 %
(form + post), create 5 % (form + post + view); 1.9 requests per operation; 16 % of the requests are POSTs. Writes:
comment and create (15 % of operations), and the filter post, which in both apps saves the user's list filter in
`dbo.UserSettings` (another 15 %). For comparison the golden recording (a coverage
recording, not traffic) has 262 exchanges, 24 % POST (18 % without sign-ins). There is no production traffic to
derive a mix from. Comments go to the tickets of the VU's last "open tickets" page (the 20 most recently updated):
the write load concentrates on ~20 hot tickets, as in the earlier rounds.

VUs: 3 × rate + 20 (max 1,500), all allocated at the start; the 37 seeded help-desk accounts are shared round-robin
(one sign-in per account in k6 `setup()`, each VU starts with its account's authentication cookie; antiforgery
cookies stay per VU). Sharing accounts makes per-user rows hot (`dbo.UserSettings`, written by every filter post) — more
contention than 3 × rate real users would cause, for both apps alike.

Pass rule per step: over the measured 120 s (after a 30 s warm-up, not counted): p95 of all mix requests ≤ 500 ms,
failed requests ≤ 0.5 % (status other than the expected 200/302, or the 30 s request timeout), dropped iterations
≤ 0.5 % of the scheduled ones. Each curve starts from the same database state (restored backup) and a 60 s prime at
10 ops/s after the app restart; steps run in ascending order; a curve stops after a step far past breaking (p95 > 5 s
or > 20 % failed/dropped).

## The two systems under test (same CPU budget, same data)

| | Legacy (as deployed on the lab VM) | New app |
|---|---|---|
| Build | TicketDesk 2.5 net48 + System.Web adapters + shared cookie (live VM state = snapshot `baseline-s-search` + the 11-12 seed-password rotation) | `TicketDesk.Web` Release from this branch (= `9675f3e` app code), Kestrel, server pages for every route (registry all `New`) |
| Web + DB | IIS 10 + SQL Server 2022 **Express** RTM 16.0.1000.6 inside one Windows Server 2025 VM (KVM, 4 vCPU, 8 GB) | Kestrel on the host + SQL Server 2022 **Express** CU25 16.0.4262.2 in a container (`td-load-sql`, `MSSQL_PID=Express`, 8 GB limit) |
| CPU | the 4 vCPU threads pinned 1:1 to CPUs 28-31 (one E-core cluster) | app (`taskset`) and SQL container (`--cpuset-cpus`) together on CPUs 24-27 (the neighbouring E-core cluster) |
| Not in the budget | qemu's emulator threads (user-mode network = the TCP path from k6, disk I/O) on CPUs 20-23, measured: 0.11-0.18 cores | the host kernel's loopback/bridge networking (charged to whichever CPU runs it) |
| Data | the VM's live TicketDesk (scale S: 10,001 tickets, 45,651 events, 251 users) restored from one COPY_ONLY backup before every curve | the same backup restored, then `td-db migrate --contract --legacy-retired` (V0013 snapshot isolation for `usp_TicketList`, C0001), one sign-in per help-desk user (V2 → V3 hash), saved as `new-base.bak`, restored before every curve |
| DB login | the legacy app's own connection string | `td_modern_app` (least privilege), TLS to SQL |
| Load generator | k6 on CPUs 16-19 (another E-core cluster), samplers on 0-1 | same |

Fairness, as far as the lab allows: the same 4 E-cores of the same kind for app + database on both sides, the same SQL
Server edition and its limits (Express: 4 cores, 1,410 MB buffer pool), the same data, the same mix, the same client.
Not equal: Windows + IIS vs Linux + Kestrel (that difference is part of the migration), SQL Server RTM vs CU25, the
VM's virtual disk (qcow2 on the host SSD) vs the container's volume on the same SSD, and qemu's user-mode network
(its CPU is outside the legacy budget, its latency is not). The 4 E-cores (4.3 GHz) are slower than the unpinned
cores of the earlier rounds, so absolute latencies here are higher than the earlier p95 pairs.

## 1. Capacity, legacy vs new (scale S, 4 cores each)

Runs 2026-11-14 01:11–02:17Z, curves interleaved: legacy r1, new r1, new r2, legacy r2, legacy r3, new r3 (order
alternating), plus legacy r4 (25, 30) because r3 stopped after its 20 step. Host load average before the steps
2.9–13.2 (other projects' builds); the CPU other processes took on the 4 SUT cores is in `steps.csv`
(`foreign_on_sut_cores`, 0.01–1.36 cores); the breaking steps broke in quiet and busy repeats alike.

### Legacy: knee at 20 ops/s, cause: ASP.NET thread-pool starvation (metastable), not CPU

The legacy app passes every repeat only at 10 ops/s; from 20 ops/s on, a step either passes with p95 ~100–125 ms or
falls into a stall that lasts 20–60 s: the VM's CPU drops to ~5 %, SQL Server has no running request, ASP.NET queues
45–146 requests while 15–185 "execute", the w3wp thread count climbs ~1.5–2 threads/s (54 → 84, 89 → 190), and the
stalled requests end at k6's 30 s timeout; the step then fails on dropped iterations and timeouts. That is the
signature of CLR thread-pool starvation (the pool adds threads slowly when every worker thread is blocked), not of
saturation: at 30 ops/s the vCPUs were 63–75 % busy, SQL 0.35 cores. A plausible trigger is in the legacy code:
`_Layout.cshtml` renders the child action `NavigationController.ProjectMenu` on every full page (5 projects in the lab
data), and it calls `AsyncHelper.RunSync` (`TaskFactory.StartNew(...).Unwrap().GetAwaiter().GetResult()`): every page
blocks one pool thread while the async EF6 query needs another; with the default minimum of 1 worker thread per CPU
(4 here) a burst can block them all. The diagnostic below (minimum threads raised) tests the starvation part.

### New app: knee between 150 and 200 ops/s, cause: lock convoys on dbo.Tickets filling the connection pool, not CPU

150 ops/s passes 3/3 (p95 25.8–56.5 ms, 291 req/s, app 0.96 + SQL 1.22 cores of the 4); 200 ops/s fails 3/3 the same
way: after 1–2 minutes of normal service (p95 ~30–90 ms) every SQL request is blocked (`requests = blocked = 100` =
SqlClient's default Max Pool Size), app and SQL CPU fall to ~0.1 cores, requests wait until k6's 30 s timeout, and the
stall comes back every few seconds. At 200 ops/s the 4 cores were 60 % busy (app 0.92 + SQL 1.50). The SQL side shows
why (`new-locks.md`): 1,915 deadlocks over the new-app curves (counter), all on **page locks of dbo.Tickets** (S vs
IX); the 1,287 retained graphs pair the comment write (EF Core `SaveChanges`: INSERT TicketEvents, the notification rows,
UPDATE Tickets, one transaction over several round trips) and the create write with the ticket center's
`SELECT COUNT(*) FROM dbo.Tickets WHERE TicketStatus <> @s` (EF Core, READ COMMITTED, a scan that takes page S locks).
Every deadlock holds its chain until the deadlock monitor picks a victim, the 20 hot tickets keep the chains forming,
and the chains take all 100 pooled connections. The head blockers the sampler caught were comment transactions between
two round trips (session sleeping, 1 open transaction), log flushes (WRITELOG / PREEMPTIVE_OS_FLUSHFILEBUFFERS) and a
filter post's UPDATE of dbo.UserSettings.

Not the shipped path: the measured ticket center is the server page (registry `New`, the mix reads its HTML); the
shipped registry serves `tickets.index` as the Angular page, whose list comes from `/api/tickets` →
`dbo.usp_TicketList`, which switches itself to SNAPSHOT isolation (td-db V0013, the AKS-session deadlock fix) and takes
no S locks. The count above is the server-page path's own query. Azure SQL Database runs READ_COMMITTED_SNAPSHOT ON by
default, the lab's SQL Server does not: the RCSI diagnostic below measures that difference.

### Diagnostics (one curve each, not the as-deployed configurations; `capacity/diag/`)

| Variant | What changed | Result |
|---|---|---|
| legacy, more pool threads | VM `machine.config`: `<processModel autoConfig="true" minWorkerThreads="50" minIoThreads="50"/>` (200 each on 4 vCPUs), app pool recycled; put back afterwards (sha256 checked) | 20, 25, 30, **40 ops/s pass** (p95 106.9 / 103.6 / 109.0 / 141.6 ms, 0 failed, 0 dropped); w3wp keeps 234–257 threads, ASP.NET queue 0; at 40 ops/s the VM is 89 % busy (vCPU 3.68 cores): the next limit is CPU, just above 40 |
| new app, READ_COMMITTED_SNAPSHOT ON | the scale-S base with `ALTER DATABASE … SET READ_COMMITTED_SNAPSHOT ON` (Azure SQL Database's default) | 150 and **200 ops/s pass** (p95 27.9 / 161.7 ms, 0.03 % failed at 200); 250 fails (p95 3.9 s, 17 % dropped) with app 1.31 + SQL 2.48 = 3.79 of the 4 cores and 58.6 runnable SQL tasks: the next limit is CPU |

So the two knees are not where the hardware ends: the legacy one is thread-pool starvation (gone with more minimum
threads), the new app's is reader/writer page-lock contention on dbo.Tickets in the server-page path (gone with row
versioning for reads). With both causes removed, both systems end at the 4 cores: legacy just above 40 ops/s,
the new app between 200 and 250 ops/s.

### The new app alone at scale L (1,000,000 tickets, 4,538,818 events; one run; `capacity/scale-l*/`)

Same setup as the S curves (app + SQL Server Express on CPUs 24-27): `TicketDeskL` from td-sql (COPY_ONLY, compressed,
1.51 GB) restored as TicketDesk in td-load-sql (7,697 MB of data in Express's 10 GB cap; 1,410 MB buffer pool),
`td-db migrate --contract --legacy-retired` (V0013, usp_TicketList; C0001 was already applied), td_modern_app
without its 8 permissions on the notifications.* tables (TicketDeskL was seeded before td-seed created them; nothing
in the mix reads them with Service Bus off), one sign-in per help-desk user; the view operation picks ids over all 1M
tickets (`TD_TICKETS=1000000`).

| Path | 5 | 10 | 25 | 50 | 100 ops/s |
|---|---|---|---|---|---|
| server pages (the S curves' path) | p95 23.2 s, fail | p95 30 s (timeouts), 6.0 % failed, fail | – | 23 % failed, fail | – |
| ticket center through `/api/tickets` (`TD_LIST_API=1`) | – | – | **p95 106.3 ms, pass** | **p95 187.6 ms, pass** | p95 5.5 s, 28 % dropped, fail |

The server-page ticket center (`TicketCenterService.GetPageAsync`: EF Core count + sorted page over the open tickets)
does not hold even 5 ops/s at 1M tickets on this setup: list and filter take 5.2 / 5.5 s at the median, SQL burns
2.8–3.0 cores, and the dominant wait is RESOURCE_SEMAPHORE (sorts queuing for memory grants in Express's 1,410 MB);
view, comment and create stay at 28 / 31 / 131 ms. Read through the API (`usp_TicketList`, the shipped Angular page's
list, which the Round 1 tuning made 146 → 51 ms) the same app sustains **50 ops/s** and ends at 100 ops/s on CPU (app
0.88 + SQL 2.83 = 3.71 of 4 cores, ~97 runnable SQL tasks, no blocking). The API run started on a warm buffer pool
(the server-page steps had just read the data); the server-page prime at 10 ops/s was the cold start. With TD_LIST_API
the filter post's UserSettings write is not made, and "filter" is the API's `unassigned` or `opentickets` list: the
mix is lighter on writes than the S curves'. One run each.

## 2. Failure drills on kind under load (`drill-*/`)

Setup (`ops/load/drills.sh`): kind v0.33.0 (Kubernetes 1.37.0, cached node image) cluster `td-load`, node container on
CPUs 24-27; the chart `ops/k8s/ticketdesk` with `values-kind.yaml` and images `ticketdesk-{web,worker}:ci-42` (CI 42 =
`9675f3e`, this branch's app code), one web replica (the AKS shape), readiness `/health/ready` every 10 s, timeout 8 s,
3 failures (the AKS-session fix), preStop 10 s; migrate job off (the database is migrated); every page a server page.
Database: td-load-sql (SQL Server 2022 Express) on the kind network, CPUs 20-23, restored from the capacity base before
each drill, reached by name; Service Bus emulator on the network; the worker sends to an SMTP sink on the network
gateway that stores the headers of each mail. Push notifications on (SMTP, consolidation and broadcast off, the
subscriber's own events excluded), an e-mail destination for each of the 37 help-desk users.

Load: the k6 mix at **75 ops/s** (half of the new app's sustained 150 ops/s; ~142 req/s), 360 s, the fault at +120 s.
Failed = a request that got no expected answer (status 0 = connection refused/reset or the 30 s timeout). Recovered =
the end of the last bad second after the fault (a failure, or p95 > 500 ms), with at least 30 clean seconds after it. Error budget (SLO 99.5 %
availability): the run's own budget is 0.5 % of its requests; the 30-day share assumes the run's request rate all month.
Readiness: the web pod's Ready condition and the service's ready endpoints sampled every second (`pods.tsv.xz`).

Results (`drills.md`, each drill's `result.json`, `marks.txt`, `pods.tsv.xz`, `timeline.csv`, `requests.csv.xz`):

- **Web pod kill** (`kubectl delete pod --grace-period=0 --force`, 1 replica): the ReplicaSet's new pod was Ready
  3.4 s later; 34 of 34,450 requests failed (32 refused/reset, 2 × 500), all within 1.1–6.4 s; recovered 6.0 s after
  the kill; 3 s without a ready endpoint; 13.3 % of the run's error budget, 0.0018 % of a 30-day budget at this rate.
  With 2 replicas: see below.
- **Database egress cut** (iptables DROP in the node from the web pod to SQL:1433, established connections included,
  60 s): requests hung until SqlClient's timeouts, first failure 15.6 s after the cut; readiness went NotReady ~30 s
  after the cut (3 probe failures: 8 s timeouts, then HTTP 503 from the database check) and Ready again ~30 s after the
  cut ended; the last 5xx came **20.0 s after the cut ended** (80.4 s after it began); 5,365 × 500 + 3 refused. NotReady did not
  stop the traffic: k6's keep-alive connections stay on the pod after its endpoint goes (kube-proxy only steers new
  connections), so requests kept reaching it and got 500 instead of being refused. 981 × 400 followed (until 132.6 s):
  POSTs of VUs whose form page had failed, sent without the antiforgery token — the client's state, not the server.
  27.6 × the run's error budget, 0.38 % of a 30-day budget.
- **SQL Server restart** (`docker restart -t 30 td-load-sql`): SQL Server kept serving for the 30 s stop timeout, logged
  no shutdown and was killed (a crash restart), and accepted connections again 36.3 s after the command (5.6 s after its
  container started); 690 × 500 between 30.4 and 35.4 s, then 111 × 400 of the same client-side kind; the web pod never
  left Ready (one 503 probe; the outage was shorter than 3 probe periods), so the service kept sending traffic to it
  and the errors were 500s, not refusals; 3.2 × the run's budget, 0.044 % of a 30-day budget.
- **Worker kill mid-message** (3 × `kill -9` of the worker's dotnet process in the node, 60 s apart; before each kill the
  SMTP sink accepted a mail of the message in progress and held back its `250` reply, so the worker was provably inside
  a send — accepted by the server, not yet acknowledged): every kill restarted the container (3 restarts, worker not
  Ready for 2–12 s); the web app did not notice (0 of 51,262 requests failed). Counting rows: the drill's writes created
  **2,633** expected mails (`dbo.TicketEventNotifications` rows of the drill's events, the subscriber's own event
  excluded, for subscribers with an e-mail destination); the sink got 2,668 mails = 2,665 distinct (ticket, event,
  recipient): **0 lost, 3 duplicated** — exactly the 3 held mails, each sent again 60 s later (the queue's lock duration
  PT1M, then redelivery of the whole message); 32 more mails were for events written before the drill (the reset's
  prime), not counted. So delivery is at least once, as `NotificationWorker` documents: a crash between a send and the
  message's completion re-sends that message's mails; nothing is lost. "No duplicate" does not hold; an idempotency key
  per (message, destination) would be needed for that (not built: outside this round).

## 3. Restore drill on kind against td-sql (`restore-drill/`)

The AKS session's restore path, fixed: the app is pointed at a database through a **values file**
(`helm upgrade --reuse-values -f`, `database.connectionString`), never `--set`; `set-vs-values.txt` shows the
difference on the chart (`--set` renders `Server=172.18.0.1`, the values file the whole string with `,14336`).
Per run (`ops/load/drills.sh restore-drill N`): a fresh source database `TicketDesk_load_rdN` on the shared td-sql
(SQL Server 2022 Developer CU25, reached from kind through a TCP relay on the network gateway) from the capacity base,
FULL recovery, its own login `td_load_app` in `td_modern_app_role`; the app switched to it through the values file; the
k6 mix at 40 ops/s (~6 writes/s) for 420 s; a full backup at +20 s, log backups every 30 s (4); the **loss** 17 s after
the last log backup: every session killed (`SINGLE_USER WITH ROLLBACK IMMEDIATE`), the last committed write read in
that session, `DROP DATABASE` (data and log files gone with it); restored at once to the point of the loss (full + 4 logs
`WITH STOPAT`, then RECOVERY) as `TicketDesk_load_rdN_pitr`; its last write read before the app writes to it; the app
switched through the values file (`helm upgrade --wait`); then the server-page smoke (sign in, new ticket, view, comment:
9 checks) every 2 s until it passes. RTO = loss → smoke passed; RPO = last write committed before the loss − last write in
the restored database (`dbo.TicketEvents.EventDate`); writes lost = events committed − events restored. Detection is
instant (the drill knows the moment of the loss): a real operator adds the time to notice and decide.

Results (`restore-drill/summary.md`, each run's `drill.txt`, `loss.txt`, `restore.txt`, `restored.txt`, `result.json`,
`smoke*.txt`, `values-pitr.redacted.yaml`):

| Run | Restore (full + 4 logs, STOPAT) | Switch (helm --wait) | Smoke | RTO (loss → smoke passed) | RPO | Writes lost |
|---|---:|---:|---|---:|---:|---:|
| 1 | 1.3 s | 4.0 s | 1 attempt, failed (500) | not measured | 17.1 s | 118 events |
| 2 | 1.2 s | 4.2 s | passed at attempt 2 | **16.7 s** | 17.4 s | 119 |
| 3 | 1.3 s | 4.0 s | passed at attempt 2 | **19.3 s** | 17.3 s | 121 |
| 4 | 1.3 s | 4.0 s | passed at attempt 2 | **16.9 s** | 17.1 s | 121 |

RTO median **16.9 s** (16.7–19.3, 3 runs); RPO median **17.2 s** (17.1–17.4, 4 runs) = the time since the last log
backup (17.0 s in every run): with the data and log files gone there is no tail-log backup, so everything after the
last log backup is lost (118–121 ticket events, plus tickets and settings written in those 17 s). RTO breakdown (runs 2–4): 3.3–5.5 s
to kill the sessions, read the last write and drop (the drill's own loss step), ~1.3 s restore, ~4 s for the helm switch
(new pod Ready), then 8.1–8.5 s until the smoke passed (its second attempt): right after `helm upgrade --wait` returns, the old pod — still on
the lost database — serves for its 10 s preStop sleep, and k6's keep-alive connections and the first smoke reached it
(run 1's only smoke attempt got its HTTP 500, so its RTO is not measured; from run 2 on the smoke retries every 2 s). In
run 1 the load's own requests failed from the loss until the old pod exited 22 s later (`timeline.csv`).

## What this does not show, and what is not fair

- **Both curves use server pages.** The k6 mix parses server HTML, so the new app ran with every route on its server
  page (registry `New`); the shipped configuration serves the ticket center, the ticket page and the new-ticket page as
  Angular pages over `/api/tickets*`. The capacity comparison is legacy MVC pages vs the new app's server pages; the
  Angular path was only measured at scale L, for list and filter (`TD_LIST_API=1`), not compared with legacy.
- **Legacy at scale L was not loaded.** TicketDeskL (7.7 GB of data) fits under Express's 10 GB cap, but loading it would
  have replaced the shared VM's live database with a 9 GB restore; the comparison is at S, the data set both systems
  hold as their maintained baseline, and the new app ran L alone, as the brief asks.
- **The legacy state is the live VM**, not the Round 1 snapshot: net48 + System.Web adapters + shared cookie (the Round 2b
  attribution put that build ~12 ms p95 above the Round 1 build at light load). A snapshot reset would have brought the
  old seed password back, so the VM was never reset; every curve restored a COPY_ONLY backup of its live database instead.
- **Different SQL Server builds and OS**: the VM runs SQL Server 2022 Express RTM on Windows, the container 2022 Express
  CU25 on Linux; RCSI is off on both (the lab default; Azure SQL Database has it on).
- **The network path differs**: k6 reaches legacy through qemu's user-mode network (its CPU, 0.11–0.21 cores, is outside
  the legacy budget; its latency is in the numbers), the new app over the host's loopback.
- **Shared accounts**: 37 accounts for up to 920 VUs make the per-user `UserSettings` row hot for both apps.
- **Repeats**: capacity 3 repeats per system (legacy 4 curves); the diagnostics, the scale-L runs and each drill ran
  once; the restore drill 4 runs (3 with an RTO). The host was shared (load average 2.9–13.2); other processes' CPU on
  the SUT cores is recorded per step.
- **Drills on kind, not AKS**: one node, NodePort through Docker's port mapping, no cloud load balancer; the database is a
  container on the same host. The worker drill's "mid-message" is made deterministic by the sink holding its reply;
  a random kill would hit fewer in-flight sends. Detection time is not in the restore drill's RTO.
- **Not measured**: attachments, search, Lighthouse, the AKS readiness fix on Azure, SQL Server's own HA (one instance).

## Files

- `capacity/` — `environment.txt`, `run-plan.sh` / `run-diag.sh` / `run-scale-l*.sh` (the exact runs), `prepare/` (VM
  backup and the new base; `vm-after.txt`: the VM's database back to the pre-round backup, row counts equal, Lucene index
  rebuilt, machine.config hash back, qemu affinity back), `runs/<system>-r<N>/rate-<R>/` (summary.json, timeline.csv,
  samples.jsonl.xz, host-load.txt, waits-before/after.txt, vm-counters.csv for legacy, k6.txt), `steps.csv`
  and `curves.md` (`ops/load/analyze.py`), `new-locks.md`, `diag/`, `scale-l/`, `scale-l-api/`, `scale-l-curves.md`.
- `drill-*/`, `drills.md`, `run-drills.sh`, `run-drills-2.sh` — the drills (the first worker-kill attempt died at its PID
  lookup before any kill and was run again with the fixed script).
- `restore-drill/` — `set-vs-values.txt`, `run-1..4/`, `summary.md`, `run-restore-4.sh`.
- Not in the repository (size, AGENT-RULES ~5 MB per round): k6's own CSV output of every step
  (`$TD_W/logs/load/raw-k6/` on lab-host), the per-request files of capacity repeats 1, 2 and 4, the diagnostics and
  scale L (`$TD_W/logs/load/raw-requests/`), and the exploratory sweep before the curves (`$TD_W/logs/load/explore/`),
  which only chose the step rates.

#!/usr/bin/env python3
"""sample.py - host-side resource samples while a load step runs (Round 6 load).

  sample.py --out FILE.jsonl --duration S [--interval 2]
            [--cpus NAME=LIST]...        busy % of logical CPUs (LIST like 24-27 or 4-15,0), averaged over the group
            [--proc NAME=PID[:TIDS]]...  CPU % of a process (100 = one core; all threads, or only TIDS, comma list) + threads
            [--proc-rest NAME=PID:TIDS]  CPU % of the process's threads NOT in TIDS (e.g. qemu's emulator threads)
            [--cgroup NAME=PATH]...      CPU % of a cgroup v2 directory (cpu.stat usage_usec; a Docker container's scope)
            [--sql NAME=SERVER:ENVFILE:KEY:DB] every --sql-every samples: sessions, requests, blocked, runnable tasks of DB on
                                         SERVER (sqlcmd as sa; the password is read from ENVFILE's KEY, passed by environment)
One JSON object per sample. Nothing here prints a secret.
"""
import argparse, json, os, subprocess, sys, time, datetime

SQLCMD = os.path.expanduser("~/Desktop/ticketdesk-modernization/.tools/bin/sqlcmd")
CLK = os.sysconf("SC_CLK_TCK")


def cpulist(s):
    out = []
    for part in s.split(","):
        if "-" in part:
            a, b = part.split("-"); out += range(int(a), int(b) + 1)
        elif part:
            out.append(int(part))
    return out


def stat_cpus():
    d = {}
    for line in open("/proc/stat"):
        if line.startswith("cpu") and line[3].isdigit():
            p = line.split(); v = list(map(int, p[1:9]))
            d[int(p[0][3:])] = (sum(v), v[3] + v[4])
    return d


def task_ticks(pid, tids=None):
    total, n = 0, 0
    try:
        for t in os.listdir(f"/proc/{pid}/task"):
            if tids is not None and int(t) not in tids:
                continue
            try:
                f = open(f"/proc/{pid}/task/{t}/stat").read().rsplit(")", 1)[1].split()
                total += int(f[11]) + int(f[12]); n += 1
            except (FileNotFoundError, ProcessLookupError):
                pass
    except FileNotFoundError:
        return None, 0
    return total, n


def proc_total(pid):
    # the whole process, exited threads included (summing /proc/PID/task/* would lose them: thread-pool threads come and go)
    try:
        f = open(f"/proc/{pid}/stat").read().rsplit(")", 1)[1].split()
        return int(f[11]) + int(f[12]), int(f[17])
    except FileNotFoundError:
        return None, 0


def all_tids(pid):
    try:
        return {int(t) for t in os.listdir(f"/proc/{pid}/task")}
    except FileNotFoundError:
        return set()


def cg_usage(path):
    try:
        for line in open(os.path.join(path, "cpu.stat")):
            if line.startswith("usage_usec"):
                return int(line.split()[1])
    except FileNotFoundError:
        return None


SQL_Q = ("SET NOCOUNT ON; SELECT "
         "(SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1 AND database_id = DB_ID(N'{db}')), "
         "(SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id "
         " WHERE s.is_user_process = 1 AND r.session_id <> @@SPID), "
         "(SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0), "
         "(SELECT ISNULL(SUM(runnable_tasks_count), 0) FROM sys.dm_os_schedulers WHERE status = 'VISIBLE ONLINE'), "
         "(SELECT ISNULL(SUM(work_queue_count), 0) FROM sys.dm_os_schedulers WHERE status = 'VISIBLE ONLINE'), "
         "(SELECT TOP 1 r.wait_type FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id "
         " WHERE s.is_user_process = 1 AND r.session_id <> @@SPID AND r.wait_type IS NOT NULL GROUP BY r.wait_type ORDER BY COUNT(*) DESC)")


HEAD_Q = ("SET NOCOUNT ON; SELECT TOP 3 CAST(s.session_id AS varchar(6)) + '|' + s.status + '|' + CAST(s.open_transaction_count AS varchar(4)) + '|' "
          "+ CAST(DATEDIFF(ms, s.last_request_start_time, GETDATE()) AS varchar(12)) + '|' + CAST(DATEDIFF(ms, s.last_request_end_time, GETDATE()) AS varchar(12)) + '|' "
          "+ ISNULL(r.wait_type, '') + '|' + CAST((SELECT COUNT(*) FROM sys.dm_exec_requests b WHERE b.blocking_session_id = s.session_id) AS varchar(6)) + '|' "
          "+ LEFT(REPLACE(REPLACE(REPLACE(ISNULL(t.text, ''), CHAR(13), ' '), CHAR(10), ' '), '|', '/'), 300) "
          "FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id = s.session_id "
          "LEFT JOIN sys.dm_exec_connections c ON c.session_id = s.session_id OUTER APPLY sys.dm_exec_sql_text(c.most_recent_sql_handle) t "
          "WHERE s.session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests WHERE blocking_session_id <> 0) "
          "AND s.session_id NOT IN (SELECT session_id FROM sys.dm_exec_requests WHERE blocking_session_id <> 0) "
          "ORDER BY (SELECT COUNT(*) FROM sys.dm_exec_requests b WHERE b.blocking_session_id = s.session_id) DESC")


def head_blockers(server, env):
    # who heads the blocking chains: status 'sleeping' with an open transaction = the client holds locks between requests
    try:
        r = subprocess.run([SQLCMD, "-S", server, "-U", "sa", "-C", "-h", "-1", "-W", "-l", "3", "-t", "4", "-Q", HEAD_Q],
                           env=env, capture_output=True, text=True, timeout=6)
        out = []
        for line in r.stdout.strip().splitlines():
            v = line.split("|")
            if len(v) >= 8 and v[0].strip().isdigit():
                out.append({"spid": int(v[0]), "status": v[1], "open_tran": int(v[2]), "since_request_start_ms": int(v[3]),
                            "since_request_end_ms": int(v[4]), "wait": v[5], "blocking": int(v[6]), "last_sql": v[7].strip()})
        return out
    except Exception as e:
        return [{"error": type(e).__name__}]


def sql_sample(server, envfile, key, db):
    pw = ""
    for line in open(envfile):
        if line.startswith(key + "="):
            pw = line.rstrip("\n").split("=", 1)[1]
    env = dict(os.environ, SQLCMDPASSWORD=pw)
    try:
        r = subprocess.run([SQLCMD, "-S", server, "-U", "sa", "-C", "-h", "-1", "-W", "-s", "|", "-l", "3", "-t", "4",
                            "-Q", SQL_Q.format(db=db)], env=env, capture_output=True, text=True, timeout=6)
        v = [x.strip() for x in r.stdout.strip().splitlines()[-1].split("|")]
        res = {"sessions": int(v[0]), "requests": int(v[1]), "blocked": int(v[2]), "runnable": int(v[3]),
               "work_queue": int(v[4]), "top_wait": None if v[5] in ("", "NULL") else v[5]}
        if res["blocked"] >= 5:
            res["head_blockers"] = head_blockers(server, env)
        return res
    except Exception as e:  # a missed sample is recorded, not fatal
        return {"error": type(e).__name__}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--duration", type=float, required=True)
    ap.add_argument("--interval", type=float, default=2.0)
    ap.add_argument("--cpus", action="append", default=[])
    ap.add_argument("--proc", action="append", default=[])
    ap.add_argument("--proc-rest", action="append", default=[])
    ap.add_argument("--cgroup", action="append", default=[])
    ap.add_argument("--sql", action="append", default=[])
    ap.add_argument("--sql-every", type=int, default=2)
    a = ap.parse_args()
    groups = {n: cpulist(v) for n, v in (x.split("=", 1) for x in a.cpus)}
    procs = {}
    for x in a.proc:
        n, v = x.split("=", 1); pid, _, tids = v.partition(":")
        procs[n] = (int(pid), {int(t) for t in tids.split(",")} if tids else None)
    for x in a.proc_rest:
        n, v = x.split("=", 1); pid, _, tids = v.partition(":")
        excl = {int(t) for t in tids.split(",") if t}
        procs[n] = (int(pid), ("rest", excl))
    cgs = dict(x.split("=", 1) for x in a.cgroup)
    sqls = {n: v.split(":", 3) for n, v in (x.split("=", 1) for x in a.sql)}

    def proc_ticks(pid, sel):
        if isinstance(sel, tuple):  # the process minus the listed (long-lived) threads
            tot, n = proc_total(pid); part, k = task_ticks(pid, sel[1])
            return (None, 0) if tot is None or part is None else (tot - part, n - k)
        if sel is None:
            return proc_total(pid)
        return task_ticks(pid, sel)

    prev_c = stat_cpus(); prev_p = {n: proc_ticks(*v) for n, v in procs.items()}
    prev_g = {n: cg_usage(p) for n, p in cgs.items()}; prev_t = time.monotonic()
    end = prev_t + a.duration; i = 0
    with open(a.out, "w") as out:
        while time.monotonic() < end:
            time.sleep(a.interval); i += 1
            now = time.monotonic(); dt = now - prev_t
            c = stat_cpus()
            rec = {"t": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")}
            rec["cpu"] = {}
            for n, cl in groups.items():
                busy = [100 * (1 - (c[k][1] - prev_c[k][1]) / max(1, c[k][0] - prev_c[k][0])) for k in cl]
                rec["cpu"][n] = round(sum(busy) / len(busy), 1)
                rec["cpu"][n + "_max"] = round(max(busy), 1)
            rec["proc"] = {}
            for n, v in procs.items():
                t, k = proc_ticks(*v)
                if t is not None and prev_p[n][0] is not None:
                    rec["proc"][n] = round(100 * (t - prev_p[n][0]) / CLK / dt, 1)
                    rec["proc"][n + "_threads"] = k
                prev_p[n] = (t, k)
            rec["cg"] = {}
            for n, p in cgs.items():
                u = cg_usage(p)
                if u is not None and prev_g[n] is not None:
                    rec["cg"][n] = round(100 * (u - prev_g[n]) / 1e6 / dt, 1)
                prev_g[n] = u
            if sqls and i % a.sql_every == 0:
                rec["sql"] = {n: sql_sample(*v) for n, v in sqls.items()}
            prev_c, prev_t = c, now
            out.write(json.dumps(rec) + "\n"); out.flush()


if __name__ == "__main__":
    main()

import re, sys, statistics
lines = open(sys.argv[1]).read().split('\n')
plan = tag = None; res = {}
for l in lines:
    m = re.match(r'=== forced plan (\d+)', l)
    if m: plan, tag = m.group(1), None; continue
    if l.startswith('--- common'): tag = 'common tag (printer, 188,919 tickets)'; res.setdefault((plan, tag), []).append([]); continue
    if l.startswith('--- rare'): tag = 'rare tag (mobile-108, 1,967 tickets)'; res.setdefault((plan, tag), []).append([]); continue
    m = re.search(r'CPU time = (\d+) ms,\s+elapsed time = (\d+) ms', l)
    if m and plan and tag: res[(plan, tag)][-1].append((int(m.group(1)), int(m.group(2))))
print('forced plan | tag | n | procedure CPU ms (median, runs) | procedure elapsed ms (median, runs)')
for (p, t), v in res.items():
    cpu = [max(x[0] for x in r) for r in v if r]; el = [max(x[1] for x in r) for r in v if r]
    print(f'{p} | {t} | {len(cpu)} | {statistics.median(cpu)} {sorted(cpu)} | {statistics.median(el)} {sorted(el)}')

"""Phase 1 follow-up: the non-standard attachment labels (Support Weapon, Loyally Protecting, Retainers) and unlabeled ones."""
import collections
from bsd import load, conditions

db = load()
rows = collections.defaultdict(list)
for f, r in db.roots.items():
    if r.get('type') == 'gameSystem':
        continue
    for l in r.get('entryLinks', []):
        t = db.byid.get(l.get('targetId'))
        if not t or t[0].get('type') not in ('unit', 'model'):
            continue
        u = t[0]
        if '[Legends]' in u['name'] or '[Crucible]' in u['name']:
            continue
        for a in u.get('associations', []):
            lab = a.get('label') or ''
            if lab in ('Support Weapon', 'Loyally Protecting', 'Retainers', ''):
                targets = []
                for g in a.get('conditionGroups', []):
                    for c in conditions(g):
                        targets.append(db.name(c.get('childId')))
                rows[lab].append((f[:22], u['name'], a.get('name'), a.get('action'), a.get('min'), a.get('max'), targets[:4]))
for lab, rs in rows.items():
    print(f'\n[{lab or "(unlabeled)"}] {len(rs)} associations')
    seen = set()
    for r in rs:
        k = (r[1], r[2])
        if k in seen: continue
        seen.add(k)
        if len(seen) <= 8: print('  ', r)

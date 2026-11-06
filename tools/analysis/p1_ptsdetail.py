"""Phase 1 item 6: in-scope pts modifiers (not under [Crucible]/[Legends] units), with the actual condition targets for the vague classes."""
import collections
from bsd import load, conditions, all_modifiers, PTS

db = load()
cnt = collections.Counter(); ex = collections.defaultdict(list)
total = 0
for node, f, path in db.walk(lambda o: 'modifiers' in o or 'modifierGroups' in o):
    if f == 'Warhammer 40,000.json':
        continue
    names = [n or '' for n, t in path]
    if any('[Crucible]' in n or '[Legends]' in n for n in names):
        continue
    for m in all_modifiers(node):
        if m.get('field') != PTS:
            continue
        total += 1
        cs = conditions(m)
        desc = []
        for c in cs:
            cid = c.get('childId')
            t = db.byid.get(cid)
            tn = (t[0].get('name'), t[0].get('type'), c.get('scope')) if t else (cid, '?', c.get('scope'))
            if cid in ('model', 'any', 'unit', 'upgrade'):
                desc.append(f'{cid}:{c["type"]}')
            else:
                desc.append(f'target[{tn[1]}|{tn[2]}]')
        key = (m['type'],) + tuple(sorted(set(desc)))
        cnt[key] += 1
        if len(ex[key]) < 2:
            ex[key].append((node.get('name'), [db.name(c.get('childId')) for c in cs][:3]))
print('in-scope pts modifiers:', total)
for k, v in cnt.most_common(30):
    print(f'  {v:4d} {k}  e.g. {ex[k]}')

"""Phase 1 items 6 and 8: classify ALL 'pts' and 'hidden' modifiers by what their conditions depend on."""
import collections, sys
from bsd import load, conditions, all_modifiers, PTS

db = load()
MODES = {'Boarding Actions', 'Crusade Force', 'Crusade Army', 'Order of Battle', 'Current Campaign'}


def under(path, name):
    return any(n == name for n, t in path)


def cls(c):
    cid = c.get('childId'); scope = c.get('scope')
    if cid in ('model', 'unit', 'any', 'upgrade'):
        return f'count:{cid}/{c.get("type") if c.get("type") == "before" else c.get("field")}'
    if scope == 'primary-catalogue':
        return 'primary-catalogue'
    t = db.byid.get(cid)
    if not t:
        return 'unresolved'
    node, f, path = t
    nm = node.get('name', '')
    if nm in MODES or c.get('field') == 'forces':
        return 'MODE:' + nm
    if nm.startswith('Show '):
        return 'TOGGLE:' + ('Legends' if 'Legends' in nm else 'Crucible' if 'Crucible' in nm else 'other')
    if 'Crusade' in [n for n, _ in path][:6]:
        return 'MODE:crusade-content'
    if under(path, 'Detachment'):
        return 'detachment'
    ty = node.get('type')
    if ty is None and 'hidden' in node and 'characteristicTypes' not in node and 'targetId' not in node and not node.get('selectionEntries'):
        return 'category/keyword'
    if ty in ('unit', 'model'):
        return 'entry:' + ty
    if ty == 'upgrade':
        return 'option/upgrade'
    return 'other:' + str(ty)


def run(field_pred, label, limit=30):
    combos = collections.Counter(); ex = {}
    for node, f, path in db.walk(lambda o: 'modifiers' in o or 'modifierGroups' in o):
        if f == 'Warhammer 40,000.json':
            continue
        if node.get('type') in ('unit', 'model', 'upgrade') or True:
            for m in all_modifiers(node):
                if not field_pred(m):
                    continue
                cs = conditions(m)
                key = tuple(sorted({cls(c) for c in cs})) or ('unconditional',)
                combos[key] += 1; ex.setdefault(key, (f, node.get('name')))
    total = sum(combos.values())
    out = sum(v for k, v in combos.items() if any(x.startswith(('MODE', 'TOGGLE:Legends', 'TOGGLE:Crucible')) for x in k))
    print(f'\n=== {label}: {total} modifiers in {len(combos)} condition classes; {out} depend on an out-of-scope mode/toggle')
    for k, v in combos.most_common(limit):
        flag = ' [OUT]' if any(x.startswith(('MODE', 'TOGGLE:Legends', 'TOGGLE:Crucible')) for x in k) else ''
        print(f'  {v:5d}{flag} {k}  e.g. {ex[k]}')
    return combos


run(lambda m: m.get('field') == PTS, 'pts modifiers')
run(lambda m: m.get('field') == 'hidden', 'hidden modifiers', 40)

# item 8: nonzero costs on non-unit entries (per-model / wargear costs)
cnt = collections.Counter()
for node, f, path in db.walk(lambda o: o.get('type') in ('model', 'upgrade') and any(c.get('typeId') == PTS and c['value'] for c in o.get('costs', []))):
    if f == 'Warhammer 40,000.json': continue
    inside_unit = any(t == 'unit' for n, t in path[:-1])
    inside_det = any(n == 'Detachment' for n, t in path)
    kind = node['type'] + (' in-unit' if inside_unit else ' top-level') + (' in-Detachment-group' if inside_det else '')
    cnt[kind] += 1
print('\nentries carrying a non-zero pts cost:', dict(cnt))

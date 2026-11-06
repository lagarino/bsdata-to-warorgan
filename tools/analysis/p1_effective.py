"""Phase 1: effective roster = own root entryLinks + root entryLinks of directly imported catalogues (importRootEntries).
Classifies entries as own-faction vs ally by their 'Allies: X' category. Excludes [Legends] and Unaligned Forces (D8/D9).
Pass a catalogue file name to list its entries."""
import collections, sys
from bsd import load

db = load()
by_id = {r['id']: f for f, r in db.roots.items()}


def root_units(f):
    r = db.roots[f]
    out = []
    for l in r.get('entryLinks', []):
        t = db.byid.get(l.get('targetId'))
        if t and t[0].get('type') in ('unit', 'model'):
            out.append(t[0])
    return out


def effective(f):
    r = db.roots[f]
    ents = [(e, f) for e in root_units(f)]
    for l in r.get('catalogueLinks', []):
        tf = by_id.get(l['targetId'])
        if tf and l.get('importRootEntries') and tf != 'Unaligned Forces.json':
            ents += [(e, tf) for e in root_units(tf)]
    seen = set(); res = []
    for e, src in ents:
        if e['id'] in seen or '[Legends]' in e['name']:
            continue
        seen.add(e['id']); res.append((e, src))
    return res


if len(sys.argv) > 1:
    f = sys.argv[1]
    for e, src in sorted(effective(f), key=lambda x: x[0]['name']):
        cats = [c['name'] for c in e.get('categoryLinks', [])]
        print(f"{e['name']:45s} [{e.get('type')}] from {src[:28]:28s} {[c for c in cats if c.startswith(('Faction:', 'Allies:'))]}")
    sys.exit()

for f, r in db.roots.items():
    if r.get('type') == 'gameSystem' or r.get('library'):
        continue
    ents = effective(f)
    ally = collections.Counter(); own = 0
    for e, src in ents:
        a = [c['name'] for c in e.get('categoryLinks', []) if c['name'].startswith('Allies: ')]
        if a:
            for c in a: ally[c[8:]] += 1
        else:
            own += 1
    print(f'{f:42s} effective entries {len(ents):3d} | no Allies: category {own:3d} | allies {dict(ally)}')

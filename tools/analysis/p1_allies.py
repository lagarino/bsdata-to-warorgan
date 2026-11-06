"""Phase 1: for each faction catalogue, classify the units/models it root-links by Faction: / Allies: categories.
Legends ([Legends] in the name) and Unaligned Forces are excluded as per decisions D8/D9."""
import collections
from bsd import load

db = load()
for f, r in db.roots.items():
    if r.get('type') == 'gameSystem' or r.get('library'):
        continue
    own = collections.Counter(); ally = collections.Counter(); n = 0; legends = 0; fac = collections.Counter()
    for l in r.get('entryLinks', []):
        t = db.byid.get(l.get('targetId'))
        if not t or t[0].get('type') not in ('unit', 'model'):
            continue
        node = t[0]
        if '[Legends]' in node['name']:
            legends += 1
            continue
        cats = [c['name'] for c in node.get('categoryLinks', [])]
        a = [c for c in cats if c.startswith('Allies: ')]
        fk = [c for c in cats if c.startswith('Faction: ')]
        n += 1
        for c in fk: fac[c] += 1
        if a:
            for c in a: ally[c] += 1
        else:
            own['(no ally category)'] += 1
    print(f'{f:42s} roster entries (non-Legends) {n:3d} | Legends dropped {legends:2d}')
    print('      Faction: categories on entries:', dict(fac.most_common(6)))
    print('      Allies: categories on entries :', dict(ally))

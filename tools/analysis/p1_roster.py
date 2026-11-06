"""Phase 1: how is a faction's roster assembled? Resolves root entryLinks (+ own root shared entries) per catalogue."""
import collections, sys
from bsd import load

db = load()
ROLE = {'Character', 'Epic Hero', 'Battleline', 'Infantry', 'Swarm', 'Mounted', 'Beast', 'Monster', 'Vehicle', 'Drone',
        'Dedicated Transport', 'Fortification', 'Pilot', 'Retinue'}
rows = []
for f, r in db.roots.items():
    if r.get('type') == 'gameSystem':
        continue
    own = collections.Counter(e.get('type') for e in r.get('sharedSelectionEntries', []))
    link_types = collections.Counter()
    link_targets = collections.Counter()
    unresolved = 0
    for l in r.get('entryLinks', []):
        t = db.byid.get(l.get('targetId'))
        if not t:
            unresolved += 1
            continue
        node, tf, _ = t
        link_types[(l.get('type'), node.get('type'))] += 1
        link_targets[tf] += 1
    fac = [c['name'] for c in r.get('categoryEntries', []) if c['name'].startswith('Faction: ')]
    rows.append((f, dict(own), dict(link_types), dict(link_targets), unresolved, fac))
for f, own, lt, tt, un, fac in rows:
    print(f'\n## {f}')
    print('   own root shared entries by type:', own)
    print('   root entryLinks (link type, target node type):', lt, '| target files:', tt, '| unresolved:', un)
    print('   Faction categories defined here:', fac)

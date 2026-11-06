"""Phase 1 item 4: catalogue import graph (catalogueLinks) and library usage. Writes catalogue-graph.json."""
import json, sys
from bsd import load

db = load()
by_catalogue_id = {r['id']: f for f, r in db.roots.items()}
graph = {}
for f, r in db.roots.items():
    links = []
    for l in r.get('catalogueLinks', []):
        links.append({'target': by_catalogue_id.get(l['targetId'], l.get('name')), 'importRootEntries': l.get('importRootEntries')})
    graph[f] = {'type': 'gameSystem' if 'gameSystem' not in r.get('type', '') and r.get('type') == 'gameSystem' else r.get('type'),
                'library': r.get('library'), 'links': links,
                'units': sum(1 for o, _, _ in db.walk(lambda o: o.get('type') == 'unit', {f})),
                'detachments': 0}
used_by = {}
for f, g in graph.items():
    for l in g['links']:
        used_by.setdefault(l['target'], []).append(f)
for f, g in sorted(graph.items()):
    print(f"{f:55s} lib={str(g['library']):5s} units={g['units']:3d} imports={[l['target'] for l in g['links']]}")
print('\nImported by:')
for t, users in sorted(used_by.items()):
    print(f'  {t}: {users}')
out = sys.argv[1] if len(sys.argv) > 1 else None
if out:
    json.dump({'catalogues': graph, 'usedBy': used_by}, open(out, 'w'), indent=1)
    print('wrote', out)

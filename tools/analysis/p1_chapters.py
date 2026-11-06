"""Phase 1 item 5: Space Marine chapters. Which Space Marines entries are gated by the primary catalogue, and what do chapters add?"""
import collections
from bsd import load, conditions, all_modifiers

db = load()
sm = db.roots['Imperium - Space Marines.json']
gated = collections.defaultdict(list)
for l in sm.get('entryLinks', []):
    t = db.byid.get(l.get('targetId'))
    if not t or t[0].get('type') not in ('unit', 'model') or '[Legends]' in t[0]['name']:
        continue
    node = t[0]
    for m in all_modifiers(node) + list(l.get('modifiers', [])):
        for c in conditions(m):
            if c.get('scope') == 'primary-catalogue':
                gated[(m['field'], m['type'], str(m['value']), c['type'], db.name(c['childId']))].append(node['name'])
print('Space Marines entries with primary-catalogue conditions:')
for k, v in sorted(gated.items(), key=lambda kv: -len(kv[1])):
    print(f'  {len(v):3d} x {k}  e.g. {v[:3]}')

print('\nChapter catalogues: own entries and detachments')
for f, r in db.roots.items():
    if f.startswith('Imperium - ') and any(db.roots.get(x, {}).get('id') == l['targetId'] for l in r.get('catalogueLinks', []) for x in ['Imperium - Space Marines.json']):
        own = [e['name'] for e in (db.byid[l['targetId']][0] for l in r.get('entryLinks', []) if l.get('targetId') in db.byid and db.byid[l['targetId']][1] == f) if e.get('type') in ('unit', 'model') and '[Legends]' not in e['name']]
        det = []
        for g, gf, p in db.walk(lambda o: o.get('name') == 'Detachment' and 'defaultSelectionEntryId' in o, {f}):
            det = [d['name'] for d in g.get('selectionEntries', []) if d.get('name') != 'None']
        print(f'  {f:36s} own units/models {len(own):2d} | detachments {len(det):2d} {det[:8]}')
smdet = []
for g, gf, p in db.walk(lambda o: o.get('name') == 'Detachment' and 'defaultSelectionEntryId' in o, {'Imperium - Space Marines.json'}):
    smdet = [d['name'] for d in g.get('selectionEntries', []) if d.get('name') != 'None']
print(f'  Space Marines: detachments {len(smdet)}: {smdet}')

"""Phase 1: SM army rule location, enhancement eligibility encoding, leader associations, full core-ability list."""
import collections, re
from bsd import load, conditions, all_modifiers, text_of

db = load()
gs = db.roots['Warhammer 40,000.json']


def roster_units(f):
    out = []
    for l in db.roots[f].get('entryLinks', []):
        t = db.byid.get(l.get('targetId'))
        if t and t[0].get('type') in ('unit', 'model') and '[Legends]' not in t[0]['name'] and '[Crucible]' not in t[0]['name']:
            out.append(t[0])
    return out


print('=== (a) Space Marines: ability profiles / rules shared by most units')
sm = roster_units('Imperium - Space Marines.json')
abil = collections.Counter(); rl = collections.Counter()
for u in sm:
    seen = set()
    for p in u.get('profiles', []):
        if p.get('typeName') == 'Abilities': seen.add(p['name'])
    for l in u.get('infoLinks', []):
        t = db.byid.get(l.get('targetId'))
        if t and l.get('type') in ('rule', 'profile'): seen.add(('link:' + (t[0].get('name') or '')))
    for s in seen:
        abil[s] += 1
print(f'{len(sm)} units/models; most common abilities/links:', abil.most_common(8))

print('\n=== (b) enhancement eligibility: how many root units link an Enhancements group, and which groups?')
grp = collections.Counter(); none = []
for f in ('Orks.json', 'Necrons.json', 'Imperium - Space Marines.json', "T'au Empire.json"):
    for u in roster_units(f):
        names = [l['name'] for l in u.get('entryLinks', []) if 'nhancement' in l['name']]
        for n in names: grp[(f[:12], n)] += 1
        if not names: none.append((f[:12], u['name'], [c['name'] for c in u.get('categoryLinks', []) if c.get('primary')]))
print('groups linked:', dict(grp))
print('units without an Enhancements link:', len(none), none[:12])

print('\n=== (c) leader associations on root units (non-Legends/Crucible)')
labels = collections.Counter(); condk = collections.Counter(); n_leaders = 0
for f in db.roots:
    if db.roots[f].get('type') == 'gameSystem': continue
    for u in roster_units(f):
        for a in u.get('associations', []):
            labels[a.get('label')] += 1
            for g in a.get('conditionGroups', []):
                for c in conditions(g):
                    t = db.byid.get(c.get('childId'))
                    condk[(c['type'], c.get('field'), (t[0].get('type') or 'category') if t else '?')] += 1
        if u.get('associations'): n_leaders += 1
print('leader/support units:', n_leaders, '| association labels:', dict(labels))
print('association condition kinds (type, field, target kind):', dict(condk))

print('\n=== (d) full core-ability link list (game-system shared rules)')
gs_rules = {r['id']: r['name'] for r in gs.get('sharedRules', [])}
core = collections.Counter()
for node, f, p in db.walk(lambda o: any(l.get('targetId') in gs_rules for l in o.get('infoLinks', []))):
    for l in node['infoLinks']:
        if l.get('targetId') in gs_rules: core[gs_rules[l['targetId']]] += 1
print(dict(core.most_common()))
print('shared rules never linked:', sorted(set(gs_rules.values()) - set(core)))

"""Phase 1 items 1-3: Force Disposition, Warlord, army rules, detachment tag rule text."""
import json, re, collections
from bsd import load, conditions, all_modifiers, text_of

db = load()
gs = db.roots['Warhammer 40,000.json']


def trim(o, depth=0):
    if isinstance(o, dict):
        return {k: trim(v, depth + 1) for k, v in o.items() if k not in ('id', 'typeId', 'shared', 'import', 'includeChildSelections', 'includeChildForces', 'publicationId', 'page', 'costs', 'hidden', 'sortIndex', 'collective')}
    if isinstance(o, list):
        return [trim(x, depth + 1) for x in o]
    return o


print('=== 1. Force Disposition (group eece-6d68-6710-7a7f)')
g = db.byid['eece-6d68-6710-7a7f'][0]
print(json.dumps(trim({k: v for k, v in g.items() if k in ('name', 'constraints', 'selectionEntries', 'selectionEntryGroups', 'modifiers')}), indent=1)[:3500])

print('\n=== 2. Warlord entry (8b5-898c-f79b-5a9c)')
w = db.byid['8b5-898c-f79b-5a9c'][0]
print(json.dumps(trim({k: v for k, v in w.items() if k in ('name', 'constraints', 'modifiers', 'rules', 'profiles', 'categoryLinks')}), indent=1)[:2500])

print('\n=== 3. Army rules: root rules / sharedRules per catalogue (names only)')
for f in ('Orks.json', 'Necrons.json', 'Imperium - Space Marines.json', "T'au Empire.json", 'Leagues of Votann.json'):
    r = db.roots[f]
    print(f, '| rules:', [x['name'] for x in r.get('rules', [])], '| sharedRules:', [x['name'] for x in r.get('sharedRules', [])][:12])

print('\n=== rule text mentioning detachment tags')
for key in ('Take and Hold', 'Purge the Foe', '3DP', 'Detachment Points', 'detachment point'):
    n = 0
    for node, f, path in db.walk(lambda o: ('description' in o or 'characteristics' in o) and key.lower() in (o.get('description', '') + text_of(o)).lower()):
        if n < 3:
            print(f'[{key}]', f, '|', node.get('name'), '|', (node.get('description') or text_of(node))[:260].replace('\n', ' '))
        n += 1
    print(f'   ... {n} hits for "{key}"')

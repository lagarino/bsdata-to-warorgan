import json, sys
d = json.load(open('Warhammer 40,000.json', encoding='utf-8'))['gameSystem']
print("costTypes:", [(c['name'], c.get('defaultCostLimit')) for c in d['costTypes']])
print("profileTypes:", [(p['name'], [c['name'] for c in p['characteristicTypes']]) for p in d['profileTypes']])
print("publications:", d['publications'])
def show_force(f, ind=0):
    print(' '*ind, 'FORCE', f['name'], 'constraints:', [(c['type'], c['field'], c['value']) for c in f.get('constraints', [])],
          'cats:', len(f.get('categoryLinks', [])))
    for sf in f.get('forceEntries', []):
        show_force(sf, ind+2)
    for c in f.get('categoryLinks', []):
        print(' '*(ind+4), 'cat', c['name'], [(x['type'], x['field'], x['value']) for x in c.get('constraints', [])])
for f in d['forceEntries']:
    show_force(f)
print()
print("sharedRules:")
for r in d['sharedRules']:
    print(' -', r['name'], '|', (r.get('description') or '')[:90].replace('\n', ' '))
print()
print("sharedSelectionEntries:", [s['name'] for s in d['sharedSelectionEntries']])
print("sharedSelectionEntryGroups:", [s['name'] for s in d['sharedSelectionEntryGroups']])
print("entryLinks:", [s['name'] for s in d['entryLinks']])
print("categories (sample):", [c['name'] for c in d['categoryEntries']][:144])

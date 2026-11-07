import json, os, collections
src = os.environ.get('BSDATA_SRC', '../bsdata')
u = json.load(open(src + '/Unaligned Forces.json', encoding='utf-8'))['catalogue']
print('sharedSelectionEntries:')
for s in u['sharedSelectionEntries']:
    cats = [c['name'] for c in s.get('categoryLinks', [])]
    pts = [c['value'] for c in s.get('costs', []) if c['name'] == 'pts']
    print('  -', s['name'], '| type', s.get('type'), '| pts', pts[:1], '|', cats[:5], '| children', len(s.get('selectionEntries', [])))
print('entryLinks:', [(l['name'], l.get('type')) for l in u['entryLinks']])
print('categories:', [c['name'] for c in u['categoryEntries']])
print('groups:', [g['name'] for g in u['sharedSelectionEntryGroups']])
# the entry links of the Orks catalogue that point into Unaligned Forces
tids = {s['id']: s['name'] for s in u['sharedSelectionEntries']}
o = json.load(open(src + '/Orks.json', encoding='utf-8'))['catalogue']
hits = []
def walk(x):
    if isinstance(x, dict):
        if x.get('targetId') in tids and 'name' in x: hits.append(x['name'])
        for v in x.values(): walk(v)
    elif isinstance(x, list):
        for y in x: walk(y)
walk(o)
print('Orks entries linking into Unaligned Forces:', sorted(set(hits)))

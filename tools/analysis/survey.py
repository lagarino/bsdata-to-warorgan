import json, sys, collections, glob, os
os.chdir(''+__import__('os').environ.get('BSDATA_SRC','../bsdata')+'')
files = [f for f in glob.glob('*.json')]
tot = collections.Counter()
ex = collections.defaultdict(list)
unit_types = collections.Counter()
profile_names = collections.Counter()
assoc_examples = []
for f in files:
    root = json.load(open(f, encoding='utf-8')).get('catalogue')
    if not root: continue
    def walk(o, path):
        if isinstance(o, dict):
            n = o.get('name', '')
            t = o.get('type')
            if o.get('typeName') in ('Abilities',):
                pass
            if t in ('unit', 'model', 'upgrade'):
                unit_types[t] += 1
            if 'associations' in o and len(assoc_examples) < 3 and isinstance(o['associations'], list):
                assoc_examples.append((f, n, o['associations']))
            for key in ('Detachment', 'Enhancement', 'Stratagem'):
                if key.lower() in n.lower() and len(ex[key]) < 8 and (f, n) not in ex[key]:
                    ex[key].append((f, n, t))
            for k, v in o.items():
                walk(v, path + [n])
        elif isinstance(o, list):
            for x in o: walk(x, path)
    walk(root, [])
print(unit_types)
for k, v in ex.items():
    print(k)
    for x in v: print('   ', x)
print(json.dumps(assoc_examples[:2], indent=1)[:3000])

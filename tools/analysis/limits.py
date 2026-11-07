import json, collections, glob, os
os.chdir(''+__import__('os').environ.get('BSDATA_SRC','../bsdata')+'')
res = collections.Counter()
examples = collections.defaultdict(list)
for f in glob.glob('*.json'):
    root = json.load(open(f, encoding='utf-8')).get('catalogue')
    if not root: continue
    def walk(o):
        if isinstance(o, dict):
            if o.get('type') == 'unit':
                cats = {c['name'] for c in o.get('categoryLinks', [])}
                mx = [c['value'] for c in o.get('constraints', []) if c.get('field') == 'selections' and c['type'] == 'max' and c.get('scope', 'parent') in ('parent', 'force', 'roster') and 'childId' not in c]
                kind = 'EpicHero' if 'Epic Hero' in cats else 'Battleline' if 'Battleline' in cats else 'DedTransport' if 'Dedicated Transport' in cats else 'Other'
                key = (kind, tuple(mx))
                res[key] += 1
                if len(examples[key]) < 3: examples[key].append((f, o['name']))
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(root)
for k, v in sorted(res.items(), key=lambda kv: -kv[1]): print(k, v, examples[k][:2])

import json, collections, glob, os, sys
os.chdir(''+__import__('os').environ.get('BSDATA_SRC','../bsdata')+'')
idx = {}
roots = {}
for f in glob.glob('*.json'):
    j = json.load(open(f, encoding='utf-8'))
    root = j.get('catalogue') or j.get('gameSystem')
    roots[f] = root
    def walk(o, parents):
        if isinstance(o, dict):
            if 'id' in o and 'name' in o and isinstance(o['id'], str):
                idx.setdefault(o['id'], (o['name'], o.get('type'), parents[-1] if parents else None))
            for v in o.values(): walk(v, parents + [o.get('name')] if 'name' in o else parents)
        elif isinstance(o, list):
            for x in o: walk(x, parents)
    walk(root, [])
def conds_of(m, out):
    for c in m.get('conditions', []): out.append(c)
    for g in m.get('conditionGroups', []): conds_of(g, out)
cls = collections.Counter(); ex = collections.defaultdict(list)
for f, root in roots.items():
    def walk(o):
        if isinstance(o, dict):
            if o.get('type') == 'unit':
                for m in o.get('modifiers', []):
                    if m.get('field') == 'hidden' and m.get('value') is True:
                        cs = []; conds_of(m, cs)
                        parts = []
                        for c in cs:
                            n, t, par = idx.get(c.get('childId'), (c.get('childId'), '?', None))
                            parts.append(f"{c.get('scope','self')}:{par if t=='upgrade' else n}:{c['type']}")
                        key = tuple(sorted(set(parts)))
                        cls[key] += 1
                        if len(ex[key]) < 2: ex[key].append((f, o['name']))
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(root)
for k, v in cls.most_common(22): print(v, k, ex[k][:1])

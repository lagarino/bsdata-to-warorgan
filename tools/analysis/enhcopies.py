import json, glob, os, collections
src = os.environ.get('WH40K_SRC', '../wh40k-11e')
ENH = 'f759-1bc4-cb3a-f0d2'
tot = collections.Counter(); ex = collections.defaultdict(list)
for f in sorted(glob.glob(src + '/*.json')):
    root = json.load(open(f, encoding='utf-8')).get('catalogue')
    if not root: continue
    def walk(o):
        if isinstance(o, dict):
            if o.get('type') == 'upgrade' and any(c.get('typeId') == ENH and c.get('value') == 1 for c in o.get('costs', [])):
                caps = tuple(sorted({(c['scope'], c['value']) for c in o.get('constraints', [])
                                     if c.get('type') == 'max' and c.get('field') == 'selections' and c.get('scope') in ('force', 'roster')}))
                divide = any(m.get('field') == ENH and m.get('type') == 'divide'
                             for mg in o.get('modifierGroups', []) for m in mg.get('modifiers', [])) or \
                         any(m.get('field') == ENH and m.get('type') == 'divide' for m in o.get('modifiers', []))
                tot[(caps, divide)] += 1
                if len(ex[(caps, divide)]) < 2: ex[(caps, divide)].append((os.path.basename(f), o['name']))
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(root)
print('enhancement entries (cost Enhancements=1):', sum(tot.values()))
for k, v in tot.most_common(): print(v, k, ex[k])

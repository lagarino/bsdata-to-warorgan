import json, glob, os, collections
src = os.environ.get('WH40K_SRC', '../wh40k-11e')
roots = {}; names = {}; parent_type = {}
def idx(o):
    if isinstance(o, dict):
        if 'id' in o and 'name' in o and isinstance(o['id'], str): names.setdefault(o['id'], (o['name'], o.get('type')))
        for v in o.values(): idx(v)
    elif isinstance(o, list):
        for x in o: idx(x)
for f in sorted(glob.glob(src + '/*.json')):
    j = json.load(open(f, encoding='utf-8')); r = j.get('catalogue') or j.get('gameSystem'); roots[os.path.basename(f)] = r; idx(r)
wd_ids = {i for i, (n, t) in names.items() if 'White Dwarf' in n}
print('entries named *White Dwarf*:', [(names[i], i) for i in wd_ids])
def conds(m, out):
    for c in m.get('conditions', []): out.append(c)
    for g in m.get('conditionGroups', []): conds(g, out)
hits = collections.defaultdict(list)
for f, r in roots.items():
    def walk(o, stack):
        if isinstance(o, dict):
            here = stack + [(o.get('name'), o.get('type'))] if 'name' in o else stack
            mods = list(o.get('modifiers', []))
            for mg in o.get('modifierGroups', []): mods += mg.get('modifiers', [])
            for m in mods:
                if m.get('field') == 'hidden':
                    cs = []; conds(m, cs)
                    if any(c.get('childId') in wd_ids for c in cs):
                        hits[f].append((o.get('name'), o.get('type'), [k for k in o.get('categoryLinks', [])][:0], [c['name'] for c in o.get('categoryLinks', [])][:5], len(o.get('profiles', []))))
            for v in o.values(): walk(v, here)
        elif isinstance(o, list):
            for x in o: walk(x, stack)
    walk(r, [])
tot = 0
for f, hs in hits.items():
    print('\n##', f, len(hs))
    for h in hs[:40]: print('   -', h[0], '|', h[1], '|', h[3])
    tot += len(hs)
print('\ntotal entries hidden behind the toggle:', tot)

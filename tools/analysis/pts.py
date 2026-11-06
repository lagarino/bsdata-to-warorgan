import json, collections, glob, os, sys
os.chdir(''+__import__('os').environ.get('WH40K_SRC','../wh40k-11e')+'')
PTS = '51b2-306e-1021-d207'
# index every entry id -> (name, type, file) across all catalogues + game system
idx = {}
roots = {}
for f in glob.glob('*.json'):
    j = json.load(open(f, encoding='utf-8'))
    root = j.get('catalogue') or j.get('gameSystem')
    roots[f] = root
    def walk(o):
        if isinstance(o, dict):
            if 'id' in o and 'name' in o and isinstance(o['id'], str):
                idx.setdefault(o['id'], (o['name'], o.get('type'), f))
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(root)
detach_group = {'8b85-ad3b-752f-6502'}
classes = collections.Counter()
ex = collections.defaultdict(list)
def classify(c, scope_hint):
    cid = c.get('childId')
    if cid in ('model', 'any', 'unit', 'upgrade', 'selections'):
        return f'child={cid}/{c.get("field")}/{c.get("type")}'
    name, typ, f = idx.get(cid, ('?', '?', '?'))
    return f'entry type={typ}/{c.get("field")}/scope={c.get("scope")}'
def conds_of(m, out):
    for c in m.get('conditions', []): out.append(c)
    for g in m.get('conditionGroups', []): conds_of(g, out)
    for g in m.get('localConditionGroups', []): conds_of(g, out)
for f, root in roots.items():
    def walk(o, uname=None):
        if isinstance(o, dict):
            if o.get('type') == 'unit': uname = o['name']
            mods = list(o.get('modifiers', []))
            for mg in o.get('modifierGroups', []):
                mods += mg.get('modifiers', [])
            for m in mods:
                if m.get('field') == PTS:
                    cs = []
                    conds_of(m, cs)
                    for mg in o.get('modifierGroups', []):
                        conds_of(mg, cs)
                    keys = tuple(sorted({classify(c, None) for c in cs})) or ('unconditional',)
                    k = (m['type'],) + keys
                    classes[k] += 1
                    if len(ex[k]) < 2: ex[k].append((f, uname or o.get('name'), [idx.get(c.get('childId'), ('?',))[0] for c in cs][:3]))
            for v in o.values(): walk(v, uname)
        elif isinstance(o, list):
            for x in o: walk(x, uname)
    walk(root)
for k, v in classes.most_common(25):
    print(v, k, ex[k][:2])

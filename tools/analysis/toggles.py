import json, glob, os, collections
src = os.environ.get('WH40K_SRC', '../wh40k-11e')
roots = {}
names = {}
def idx(o):
    if isinstance(o, dict):
        if 'id' in o and 'name' in o and isinstance(o['id'], str): names.setdefault(o['id'], o['name'])
        for v in o.values(): idx(v)
    elif isinstance(o, list):
        for x in o: idx(x)
for f in sorted(glob.glob(src + '/*.json')):
    j = json.load(open(f, encoding='utf-8')); r = j.get('catalogue') or j.get('gameSystem'); roots[os.path.basename(f)] = r; idx(r)
legend_units = collections.Counter(); all_units = collections.Counter()
roster_scope_conds = collections.Counter()
def conds(m, out):
    for c in m.get('conditions', []): out.append(c)
    for g in m.get('conditionGroups', []): conds(g, out)
for f, r in roots.items():
    def walk(o):
        if isinstance(o, dict):
            if o.get('type') == 'unit':
                all_units[f] += 1
                if '[Legends]' in o.get('name', ''): legend_units[f] += 1
            for m in o.get('modifiers', []):
                if m.get('field') == 'hidden' and m.get('value') is True:
                    cs = []; conds(m, cs)
                    for c in cs:
                        if c.get('scope') == 'roster' and c.get('type') == 'lessThan':
                            roster_scope_conds[names.get(c.get('childId'), c.get('childId'))] += 1
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(r)
print('units defined:', sum(all_units.values()), '| of which name contains [Legends]:', sum(legend_units.values()))
print('catalogues with most Legends units:', legend_units.most_common(8))
print('\nroster-level toggles that hide things (entry name: number of hidden-modifiers that depend on it being OFF):')
for k, v in roster_scope_conds.most_common(25): print('  ', v, k)

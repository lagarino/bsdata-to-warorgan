import json, collections
root = json.load(open(''+__import__('os').environ.get('BSDATA_SRC','../bsdata')+'/Orks.json', encoding='utf-8'))['catalogue']
def trim(o):
    if isinstance(o, dict):
        return {k: trim(v) for k, v in o.items() if k not in ('id', 'typeId', 'shared', 'import', 'includeChildSelections', 'includeChildForces', 'publicationId', 'page', 'costs', 'hidden', 'sortIndex')}
    if isinstance(o, list): return [trim(x) for x in o]
    return o
def find(o, pred, out):
    if isinstance(o, dict):
        if pred(o): out.append(o)
        for v in o.values(): find(v, pred, out)
    elif isinstance(o, list):
        for x in o: find(x, pred, out)
    return out
g = find(root, lambda o: o.get('id') == 'ebea-74c4-f3db-183d', [])
print(len(g))
if g:
    s = json.dumps(trim(g[0]), indent=1)
    print(s[:5500])
# unit-level 'selections' constraints on type unit with scope roster/force
cnt = collections.Counter()
for u in find(root, lambda o: o.get('type') == 'unit', []):
    for c in u.get('constraints', []):
        if c.get('field') == 'selections' and c.get('scope') in ('roster', 'force'):
            cnt[(c['type'], c['value'], c['scope'])] += 1
print(cnt)

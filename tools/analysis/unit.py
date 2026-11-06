import json, sys
path, name = sys.argv[1], sys.argv[2]
d = json.load(open(path, encoding='utf-8'))['catalogue']
def find(o, pred, out):
    if isinstance(o, dict):
        if pred(o): out.append(o)
        for v in o.values(): find(v, pred, out)
    elif isinstance(o, list):
        for x in o: find(x, pred, out)
    return out
hits = find(d, lambda o: o.get('name') == name and ('profiles' in o or 'selectionEntries' in o or 'selectionEntryGroups' in o or 'entryLinks' in o), [])
print(len(hits), "hits")
def trim(o, depth=0):
    if isinstance(o, dict):
        return {k: trim(v, depth+1) for k, v in o.items() if k not in ('id', 'typeId', 'xmlns', 'publicationId', 'page', 'shared', 'import', 'includeChildSelections', 'includeChildForces', 'scope')}
    if isinstance(o, list):
        return [trim(x, depth+1) for x in o]
    return o
print(json.dumps(trim(hits[0]), indent=1)[:int(sys.argv[3]) if len(sys.argv) > 3 else 9000])

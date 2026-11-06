import json, sys
d = json.load(open(''+__import__('os').environ.get('WH40K_SRC','../wh40k-11e')+'/Warhammer 40,000.json', encoding='utf-8'))['gameSystem']
def trim(o):
    if isinstance(o, dict):
        return {k: trim(v) for k, v in o.items() if k not in ('id', 'typeId', 'shared', 'import', 'includeChildSelections', 'includeChildForces', 'publicationId', 'page', 'costs', 'hidden')}
    if isinstance(o, list): return [trim(x) for x in o]
    return o
for nm in sys.argv[1:]:
    for s in d['sharedSelectionEntries'] + d['sharedSelectionEntryGroups']:
        if s['name'] == nm:
            print('#####', nm)
            print(json.dumps(trim(s), indent=1)[:4500])
            break

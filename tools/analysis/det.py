import json, collections, glob, os
os.chdir(''+__import__('os').environ.get('WH40K_SRC','../wh40k-11e')+'')
rules_n = collections.Counter(); cost_n = collections.Counter(); total = 0
multi = []; with_profiles = 0; with_infolinks = 0
for f in glob.glob('*.json'):
    j = json.load(open(f, encoding='utf-8')); root = j.get('catalogue')
    if not root: continue
    def walk(o):
        global total, with_profiles, with_infolinks
        if isinstance(o, dict):
            if o.get('name') == 'Detachment' and o.get('selectionEntries') is not None and 'defaultSelectionEntryId' in o:
                for d in o['selectionEntries']:
                    if d.get('name') in ('None',): continue
                    total += 1
                    n = len(d.get('rules', []))
                    rules_n[n] += 1
                    dp = [c['value'] for c in d.get('costs', []) if c['name'] == 'Detachment Points']
                    cost_n[tuple(dp)] += 1
                    if n > 1: multi.append((f, d['name'], [r['name'] for r in d['rules']]))
                    if d.get('profiles'): with_profiles += 1
                    if d.get('infoLinks'): with_infolinks += 1
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(root)
print('detachments', total, 'rules per detachment', dict(rules_n), 'DP cost', dict(cost_n), 'with profiles', with_profiles, 'infolinks', with_infolinks)
print(multi[:5])

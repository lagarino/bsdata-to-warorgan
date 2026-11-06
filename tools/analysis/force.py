import json
d = json.load(open(''+__import__('os').environ.get('WH40K_SRC','../wh40k-11e')+'/Warhammer 40,000.json', encoding='utf-8'))['gameSystem']
f = d['forceEntries'][0]
names = {'1d6e-2579-8e7f-1ed4': 'BoardingActions', 'cac3-71d1-ea4b-795d': 'Crusade', 'd62d-db22-4893-4bc0': 'Incursion', 'baf8-997f-e323-a090': 'StrikeForce', '4204-82d0-908c-a62a': 'Onslaught'}
print([ (c['field'], c['type'], c['value']) for c in f['constraints']])
for m in f.get('modifiers', []):
    conds = []
    def gather(o):
        for c in o.get('conditions', []): conds.append((names.get(c['childId'], c['childId']), c['type'], c['value']))
        for g in o.get('conditionGroups', []): gather(g)
    gather(m)
    print(m['field'], m['type'], m['value'], conds)
for mg in f.get('modifierGroups', []):
    for m in mg.get('modifiers', []):
        conds = []
        def gather(o):
            for c in o.get('conditions', []): conds.append((names.get(c['childId'], c['childId']), c['type'], c['value']))
            for g in o.get('conditionGroups', []): gather(g)
        gather(m); gather(mg)
        print('MG', m['field'], m['type'], m['value'], conds)

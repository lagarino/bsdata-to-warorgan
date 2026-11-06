"""Phase 1: categories carried by detachments, hidden/constraint conditions on detachments, army-rule attachment, warlord text."""
import collections, re
from bsd import load, conditions, all_modifiers, text_of

db = load()
dets = []
for g, f, p in db.walk(lambda o: o.get('name') == 'Detachment' and 'defaultSelectionEntryId' in o):
    for d in g.get('selectionEntries', []):
        if d.get('name') != 'None':
            dets.append((d, f))
print('detachments:', len(dets))
cats = collections.Counter()
combos = collections.Counter()
for d, f in dets:
    c = tuple(sorted(x['name'] for x in d.get('categoryLinks', [])))
    combos[c] += 1
    for x in c: cats[x] += 1
print('categories on detachments:', dict(cats))
print('combinations:', dict(combos.most_common(10)))

print('\nconstraints / modifiers on detachment entries (what restricts selecting them):')
kinds = collections.Counter()
ex = {}
for d, f in dets:
    for c in d.get('constraints', []):
        k = ('constraint', c['type'], c['field'] if c['field'] == 'selections' else 'cost:' + db.name(c['field']), c.get('scope'), c['value'])
        kinds[k] += 1; ex.setdefault(k, d['name'])
    for m in all_modifiers(d):
        if m['field'] in ('hidden',) or db.name(m['field']) in ('Detachment Points',):
            for c in conditions(m):
                k = ('mod ' + m['field'][:6] + str(m['value']), c['type'], c.get('scope'), db.name(c.get('childId')))
                kinds[k] += 1; ex.setdefault(k, d['name'])
for k, v in kinds.most_common(18): print(f'  {v:4d} {k}  e.g. {ex[k]}')

print('\narmy-rule attachment: who infoLinks to each catalogue-level rule? (owner node type -> count)')
for f in ('Orks.json', 'Necrons.json', "T'au Empire.json", 'Leagues of Votann.json'):
    r = db.roots[f]
    rules = {x['id']: x['name'] for x in r.get('rules', []) + r.get('sharedRules', [])}
    owners = collections.defaultdict(collections.Counter)
    for node, ff, path in db.walk(lambda o: any(l.get('targetId') in rules for l in o.get('infoLinks', [])), {f}):
        for l in node['infoLinks']:
            if l.get('targetId') in rules:
                is_det = any(n == 'Detachment' for n, t in path)
                owners[rules[l['targetId']]][(node.get('type') or 'group', 'in-Detachment' if is_det else 'elsewhere')] += 1
    root_rules = {x['name'] for x in r.get('rules', [])}
    print(f, '| root rules:', sorted(root_rules))
    for n, c in owners.items(): print('    ', n, dict(c))

print('\nwarlord wording in ability text:')
pat = re.compile(r'(must be (?:your|the|selected as your) [^.]*warlord|cannot be (?:your|selected as your|chosen as your) [^.]*warlord|can be your warlord|even though[^.]*warlord)', re.I)
cnt = collections.Counter(); samples = {}
for node, f, p in db.walk(lambda o: 'characteristics' in o):
    t = text_of(node)
    m = pat.search(t)
    if m:
        k = m.group(1).lower()[:60]; cnt[k] += 1; samples.setdefault(k, (f, node.get('name')))
for k, v in cnt.most_common(12): print(f'  {v:3d} "{k}" e.g. {samples[k]}')

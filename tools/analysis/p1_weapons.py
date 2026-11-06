"""Phase 1 items 7 and 9: distinct weapon keywords / core abilities, and modifiers that touch weapon profiles."""
import collections, re
from bsd import load, conditions, all_modifiers, text_of

db = load()
gs = db.roots['Warhammer 40,000.json']
char_name = {}
ptype_of_char = {}
for pt in gs['profileTypes']:
    for c in pt['characteristicTypes']:
        char_name[c['id']] = (pt['name'], c['name'])

# ---- weapon keywords
kw = collections.Counter(); shapes = collections.Counter(); shape_ex = {}
weapons = 0
for node, f, p in db.walk(lambda o: o.get('typeName') in ('Ranged Weapons', 'Melee Weapons')):
    weapons += 1
    for c in node.get('characteristics', []):
        if c['name'] == 'Keywords':
            t = c.get('$text', '').strip()
            if not t or t == '-':
                continue
            for k in re.split(r',\s*', t):
                k = k.strip().strip('[]').strip()
                if not k: continue
                kw[k.upper()] += 1
                shape = re.sub(r'\d+', 'N', re.sub(r'ANTI-[A-Z/ \-]+?(?= N\+|$)', 'ANTI-X', k.upper()))
                shapes[shape] += 1; shape_ex.setdefault(shape, k)
print(f'weapon profiles: {weapons}; distinct keyword strings: {len(kw)}; distinct shapes (digits -> N): {len(shapes)}')
for s, n in shapes.most_common(70): print(f'  {n:5d}  {s:42s} e.g. {shape_ex[s]}')

# ---- core abilities = infoLinks to game-system shared rules
gs_rules = {r['id']: r['name'] for r in gs.get('sharedRules', [])}
core = collections.Counter()
for node, f, p in db.walk(lambda o: any(l.get('targetId') in gs_rules for l in o.get('infoLinks', []))):
    for l in node['infoLinks']:
        if l.get('targetId') in gs_rules: core[gs_rules[l['targetId']]] += 1
print('\ncore abilities linked from units/models (name: links):')
print(dict(core.most_common()))

# ---- modifiers that touch weapon profile characteristics
print('\nmodifiers whose field is a weapon characteristic id or annotation on weapon profiles:')
wmods = collections.Counter(); ex = {}
for node, f, p in db.walk(lambda o: 'modifiers' in o or 'modifierGroups' in o):
    for m in all_modifiers(node):
        fld = m.get('field')
        if fld in char_name and char_name[fld][0] in ('Ranged Weapons', 'Melee Weapons'):
            k = (m['type'], char_name[fld][1], 'direct' if 'affects' not in m else 'affects:' + m['affects'][-24:])
        elif fld == 'annotation' and 'Weapons' in m.get('affects', ''):
            k = (m['type'], 'annotation', m['affects'][-24:])
        else:
            continue
        wmods[k] += 1; ex.setdefault(k, (f, node.get('name')))
for k, v in wmods.most_common(25): print(f'  {v:5d} {k}  e.g. {ex[k]}')

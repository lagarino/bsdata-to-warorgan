"""Phase 1: how does an allied entry get its 'Allies: X' category / visibility? Shows category-related and hidden modifiers of one entry."""
import sys, json
from bsd import load, conditions

db = load()
fname, ename = sys.argv[1], sys.argv[2]
for node, f, path in db.walk(lambda o: o.get('name') == ename and o.get('type') in ('unit', 'model'), {fname}):
    print('entry', ename, 'in', f, '| categories:', [c['name'] for c in node.get('categoryLinks', [])])
    mods = list(node.get('modifiers', []))
    for mg in node.get('modifierGroups', []):
        mods += mg.get('modifiers', [])
    for m in mods:
        if m.get('field') in ('category', 'hidden') or m.get('type') in ('add', 'remove') and m.get('field') == 'category':
            cs = conditions(m)
            print('  modifier', m['type'], m['field'], repr(m.get('value')), '| scope/conds:',
                  [(c.get('scope'), db.name(c.get('childId')), c['type'], c['value']) for c in cs][:4])
    break

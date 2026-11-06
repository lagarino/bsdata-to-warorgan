import json, sys, collections
path = sys.argv[1]
d = json.load(open(path, encoding='utf-8'))
root = d.get('gameSystem') or d.get('catalogue')
print("top keys:", {k: (len(v) if isinstance(v, (list, dict)) else v) for k, v in root.items()})

# count node types across the whole tree
types = collections.Counter()
profile_types = collections.Counter()
cond_types = collections.Counter()
mod_types = collections.Counter()
def walk(o):
    if isinstance(o, dict):
        if 'typeName' in o and 'characteristics' in o:
            profile_types[o['typeName']] += 1
        if 'type' in o and isinstance(o['type'], str) and 'field' in o:
            mod_types[(o['type'], o.get('field'))] += 1
        for k, v in o.items():
            types[k] += 1
            walk(v)
    elif isinstance(o, list):
        for x in o:
            walk(x)
walk(root)
print("\nprofile types:", profile_types.most_common())
print("\nmodifier (type, field):", mod_types.most_common(30))
print("\nkeys:", types.most_common(80))

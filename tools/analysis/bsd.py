"""Shared loader for the phase-1 analysis scripts.

Usage:  from bsd import load
        db = load()          # BSDATA_SRC env var, default ../bsdata
        db.roots[file]       # catalogue/gameSystem dict per file name
        db.byid[id]          # (node, file, path-of-(name,type)) for every node with an id (first definition wins)
"""
import glob, json, os, collections

PTS = '51b2-306e-1021-d207'
DP = '82ae-1066-5107-6ae0'
ENH = 'f759-1bc4-cb3a-f0d2'


class DB:
    def __init__(self, src):
        self.src = src
        self.roots = {}
        self.byid = {}
        for f in sorted(glob.glob(os.path.join(src, '*.json'))):
            j = json.load(open(f, encoding='utf-8'))
            root = j.get('catalogue') or j.get('gameSystem')
            if root is None:
                continue
            name = os.path.basename(f)
            self.roots[name] = root
            self._index(root, name, [])

    def _index(self, o, f, path):
        if isinstance(o, dict):
            here = path + [(o.get('name'), o.get('type'))] if 'name' in o else path
            if isinstance(o.get('id'), str):
                self.byid.setdefault(o['id'], (o, f, here))
            for v in o.values():
                self._index(v, f, here)
        elif isinstance(o, list):
            for x in o:
                self._index(x, f, path)

    def name(self, i):
        e = self.byid.get(i)
        return e[0].get('name') if e else i

    def walk(self, pred=None, files=None):
        """Yield (node, file, path) for all dict nodes (optionally filtered)."""
        for f, root in self.roots.items():
            if files and f not in files:
                continue
            stack = [(root, [])]
            while stack:
                o, path = stack.pop()
                if isinstance(o, dict):
                    here = path + [(o.get('name'), o.get('type'))] if 'name' in o else path
                    if pred is None or pred(o):
                        yield o, f, here
                    for v in o.values():
                        stack.append((v, here))
                elif isinstance(o, list):
                    for x in o:
                        stack.append((x, path))


def load():
    return DB(os.environ.get('BSDATA_SRC', os.path.join('..', 'bsdata')))


def conditions(m, out=None):
    """Flatten all conditions of a modifier/modifierGroup/conditionGroup."""
    out = [] if out is None else out
    for c in m.get('conditions', []):
        out.append(c)
    for g in m.get('conditionGroups', []) + m.get('localConditionGroups', []):
        conditions(g, out)
    return out


def all_modifiers(o):
    mods = list(o.get('modifiers', []))
    for mg in o.get('modifierGroups', []):
        mods += mg.get('modifiers', [])
        for sub in mg.get('modifierGroups', []):
            mods += sub.get('modifiers', [])
    return mods


def text_of(profile):
    return ' '.join(c.get('$text', '') for c in profile.get('characteristics', []))

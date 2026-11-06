# Analysis scripts

Throwaway Python scripts that produced the numbers in `docs/`. They are exploratory, not part of the
converter. Python 3, standard library only. Run with `python -I script.py` (isolated mode).

Set `WH40K_SRC` to the BSData checkout (default `../wh40k-11e`, relative to the working directory).
Scripts that open files by bare name (`gs.py`, `unit.py`, `explore.py`) must be run **from inside the BSData
checkout**; the others `chdir` there themselves.

| Script | Usage | Produces |
| --- | --- | --- |
| `explore.py <file>` | `cd $WH40K_SRC; python -I <repo>/tools/analysis/explore.py "Orks.json"` | Root keys, profile types, modifier `(type, field)` counts, key frequency |
| `gs.py` | from the BSData dir | Game-system cost types, profile types, force entries, shared rules, shared entries |
| `gs2.py <names...>` | `python -I gs2.py "Battle Size" "Force Disposition"` | Trimmed JSON of game-system shared entries / groups |
| `force.py` | any | Army Roster constraints and their modifiers (battle-size caps) |
| `unit.py <file> <entry name> [chars]` | from the BSData dir | Trimmed JSON of the first entry with that name (e.g. `Boyz`, `Vindicare Assassin`) |
| `survey.py` | any | Entry type counts; samples of detachment/enhancement names; leader `associations` examples |
| `limits.py` | any | Per-unit army copy limits grouped by role (G4) |
| `pts.py` | any | Classification of `pts` modifiers by what they depend on (G1, points tiers) - top 25 classes |
| `hid.py` | any | Classification of unit `hidden` modifiers (what gates a unit's visibility) |
| `det.py` | any | Detachment count, DP cost distribution, rules per detachment (G5) |
| `enh.py` | any | The Orks `Enhancements - Upgrades` group (id `ebea-74c4-f3db-183d`) and unit limit counts |
| `enhcopies.py` | any | Enhancement entries by copy-limit pattern (max 1 vs max 3 + divide), basis of the `Upgrade` flag (G2) |
| `toggles.py` | any | Roster-level toggles that hide content, plus the number of `[Legends]` units |
| `unaligned.py` | any | Contents of `Unaligned Forces.json` |
| `whitedwarf.py` | any | Everything hidden behind `Show White Dwarf content` (all of it is Crusade content) |

Known limitations: `pts.py` and `hid.py` print only the top classes; `gs.py` truncates long rule text;
none of them resolve `entryLinks` (links are counted as separate nodes, not followed), so unit counts are by
defining entries, not by use.

Caveat: `force.py` flattens nested condition groups, so its output hides the and/or structure. The detachment-point
cap rule (Strike Force **or** (Incursion **and** a 3DP detachment) -> 3) was confirmed by reading the raw JSON of
`Warhammer 40,000.json` lines ~1520-1700 (constraint `95c5-effd-74f1-c8f4`).

## Phase 1 scripts

Import `bsd.py` (shared loader, `WH40K_SRC` env var). Run from `tools/analysis` **without `-I`** (the helper module is imported
from the script folder; the BSData folder is a different directory) and set `PYTHONIOENCODING=utf-8` on Windows.

| Script | Answers |
| --- | --- |
| `p1_graph.py [out.json]` | catalogue import graph (`catalogueLinks`) and who imports whom |
| `p1_roster.py` | how each catalogue's root `entryLinks` assemble the roster (unit vs model, target files) |
| `p1_allies.py`, `p1_effective.py [file]` | effective roster incl. imports; faction keywords per entry; list one catalogue's entries |
| `p1_allymods.py <file> <entry>` | category/hidden modifiers that make an entry an ally |
| `p1_chapters.py` | Space Marine chapter gating, chapter-own entries and detachments |
| `p1_misc.py` | Force Disposition group, Warlord entry, army-rule names, rule text search |
| `p1_detcats.py` | detachment categories, detachment constraints, army-rule attachment, Warlord ability wording |
| `p1_weapons.py` | weapon keyword shapes, core-ability links, modifiers on weapon profiles |
| `p1_modclasses.py` | classification of all `pts` and `hidden` modifiers by condition target |
| `p1_ptsdetail.py` | in-scope `pts` modifiers (without Crucible/Legends) with condition targets |
| `p1_units.py` | Space Marines army rule, enhancement-link eligibility, leader associations, full core-ability list |

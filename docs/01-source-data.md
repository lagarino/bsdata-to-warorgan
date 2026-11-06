# Source data: BSData/wh40k-11e

Cloned at `C:\Users\ruido\DevProjects\wh40k-11e` (sibling of this repo). Commit examined:
`11ad64c81c058e5deeccd1cb23e64a972029c7b0`.

## Files

48 JSON files (BattleScribe `.cat`/`.gst` converted to JSON; the `.gitattributes` still mentions `.cat`/`.gst`).

- `Warhammer 40,000.json` - the **game system** (`gameSystem`, name "Warhammer 40,000 11th Edition",
  id `sys-352e-adc2-7639-d610`, revision 18).
- All others are **catalogues** (`catalogue`), one per faction, e.g. `Orks.json`, `Necrons.json`,
  `Imperium - Space Marines.json`, `Imperium - Ultramarines.json`.
- `... Library.json` and `Library - ....json` catalogues (`"library": true`) hold shared units imported by
  faction catalogues: Aeldari, Chaos Daemons, Chaos Knights, Astra Militarum, Imperial Knights,
  Tyranids, Titans, Astartes Heresy Legends.
- Space Marine chapters (Ultramarines, Blood Angels, ...) are catalogues that link to
  `Imperium - Space Marines.json`.
- `Unaligned Forces.json` is imported by many catalogues through `catalogueLinks`.

Root-level shape of a catalogue: `name, id, gameSystemId, library, revision, catalogueLinks,
categoryEntries, entryLinks, profileTypes, rules, sharedProfiles, sharedRules, sharedSelectionEntries,
sharedSelectionEntryGroups`. Text bodies are stored under `"$text"` (characteristics) or `description` (rules).

Counts across all catalogues (entry `type`): **470 units, 2463 models, 8863 upgrades**; 263 detachments.

## Node vocabulary

| Node | Meaning |
| --- | --- |
| `selectionEntry` `type: unit` | A datasheet |
| `selectionEntry` `type: model` | A model type inside a unit |
| `selectionEntry` `type: upgrade` | Everything else: weapons/wargear choices, detachments, enhancements, configuration toggles, battle size |
| `selectionEntryGroup` | A choice group, with min/max `constraints` and optional `defaultSelectionEntryId` |
| `entryLink` | Reference (`targetId`) to a shared entry/group, optionally with its own constraints/modifiers. **Must be resolved**; most weapons are linked, not inline |
| `infoLink` | Reference to a shared `profile` or `rule` |
| `profiles` | Typed stat blocks (`typeName`): `Unit`, `Ranged Weapons`, `Melee Weapons`, `Abilities`, `Transport`, `Psychic Abilities`, plus ad-hoc ones (e.g. Orks `Unpredictable Genius - D6`, `Throttlerokkit Shokka Engine`) |
| `rules` / `sharedRules` | Named rule text (army rules, detachment rules, keyword definitions) |
| `categoryLinks` | Keywords. `primary: true` marks the main role category (Character, Battleline, Infantry, ...). Faction keyword appears as `Faction: <Name>` |
| `costs` | Cost per cost type (see below) |
| `constraints` | min/max on `selections`, or on a cost field. `scope` can be `self, parent, force, roster, primary-catalogue, root-entry` or an entry id; `childId` can be an entry id or the specials `model`, `unit`, `upgrade`, `any` |
| `modifiers` / `modifierGroups` | Conditional changes (see below) |
| `associations` | Leader attachment ("Leading", labels `Leader` / `Support`) |
| `catalogueLinks` | `importRootEntries: true` pulls the target catalogue's root entries in |

### Cost types (game system)

| Name | id |
| --- | --- |
| pts | `51b2-306e-1021-d207` |
| Crusade Points | `b03b-c239-15a5-da55` |
| Crusade: Battle Honours | `75bb-ded1-c86d-bdf0` |
| Crusade: Experience | `a623-fe74-1d33-cddf` |
| Crusade: Weapon Modifications | `716d-91b7-d55a-1022` |
| Blackstone Fragments | `ac6b-ced3-9b5e-9a6e` |
| Detachment Points | `82ae-1066-5107-6ae0` |
| Enhancements | `f759-1bc4-cb3a-f0d2` (1 per enhancement, counts toward the enhancement limit) |

Only `pts`, `Detachment Points` and `Enhancements` matter for our scope.

### Profile types and characteristic names

- Unit: `M, T, Sv, W, LD, OC, InSv, Base`
- Ranged Weapons: `Range, A, BS, S, AP, D, Keywords`
- Melee Weapons: `Range, A, WS, S, AP, D, Keywords`
- Abilities: `Description`; Transport: `Capacity`; Psychic Abilities: `Descriptions`

### Modifiers

Modifier = `{field, type, value, conditions/conditionGroups/localConditionGroups, repeats, affects, ...}`.

Observed `type` values on fields: `set, increment, decrement, divide, append, replace, add, min, max`
(`max`/`min` modify a constraint value; `field` is then the constraint's own id or a cost-type id).
Special fields: `hidden`, `name`, `category`, `annotation` (appends text to matching profiles via
`affects` e.g. `self.entries.group.recursive.profiles.Melee Weapons`), `defaultAmount`.

Condition `type` values: `atLeast, atMost, greaterThan, lessThan, equalTo, notEqualTo, instanceOf,
notInstanceOf, before` (the last is used in unit-count scaling). `repeats` multiplies an increment per
matching selection. `includeChildSelections`, `scope` and `shared` change what is counted.

## Game system contents (`Warhammer 40,000.json`)

- **`forceEntries`**: `Army Roster` (plain 40k - **our scope**), `Boarding Actions`, `Crusade Force` (out of scope).
  `Army Roster` constraints: max `Detachment Points` 2 (id `95c5-effd-74f1-c8f4`, modified, see below),
  max `pts` (id `a00c-6979-992f-046b`, modified to the battle size), max `Enhancements` 2
  (id `606d-06be-c4b4-f877`, message "You cannot have more than {value} Enhancements in your army.").
  Its category links define the unit roles (Character requires min 1, Battleline, Infantry, Swarm, Mounted,
  Beast, Monster, Vehicle, Drone, Dedicated Transport, Fortification, Epic Hero, Unit) and the `Allies: ...`
  categories: Adeptus Titanicus, Astra Militarum, Chaos Knights, Deathwatch, Heretic Astartes, Imperial Agents,
  Imperial Knights, Legiones Daemonica, Titanicus Traitoris, Tyranids, Unaligned Forces.
- **`sharedRules` (49)**: weapon and core abilities with rules text - Pistol, Hazardous, Leader, Support,
  Devastating Wounds, Assault, Extra Attacks, Twin-linked, Anti, Sustained Hits, Heavy, Melta, Feel No Pain,
  Blast, Precision, Indirect Fire, Lance, Lethal Hits, Ignores Cover, Rapid Fire, Torrent, Scouts,
  Infiltrators, Deep Strike, Deadly Demise, Stealth, Super-Heavy Walker, Lone Operative, Hover, Fights First,
  Psychic, Firing Deck, One Shot, Crucible, Cleave, Close-quarters, Damaged, and numbered variants
  (`Feel No Pain 4+`, `Scouts 6"` ...). These are the source for `DataSetDefinition.CoreAbilities` /
  `WeaponAbilities` regexes. Note 11e-specific: `Close-quarters` (identical to Pistol), `Cleave X`, `Support`.
- **`categoryEntries` (144)**: roles, keywords, `Faction: X` entries.
- **Battle Size** (shared entry): `1. Incursion (1000 Point limit)` id `d62d-db22-4893-4bc0`,
  `2. Strike Force (2000 Point limit)` id `baf8-997f-e323-a090`, `3. Onslaught (3000 Point limit)`
  id `4204-82d0-908c-a62a`, plus `Override points limit?` (id `c6ea-562c-984f-6c25`, min 500, step 250).
- **Force Disposition** (shared entry/group, ids `eece-6d68-6710-7a7f` link target): required min 1 / max 1 per roster
  for the Army Roster. Contents not yet extracted.
- `3DP Detachment` is a category (id `7d80-2de5-816e-9d2b`) attached to detachments costing 3 detachment points.

### Army-level limits (derived from the game system modifiers)

| Battle size | Points | Detachment-point cap | Enhancement cap |
| --- | --- | --- | --- |
| Incursion | 1000 | 2, **3 if a `3DP Detachment` is selected** | 2 |
| Strike Force | 2000 | 3 | 4 |
| Onslaught | 3000 | 4 | 4 |

Read from the `Army Roster` modifiers on constraints `95c5...` (DP) and `606d...` (enhancements). The
points cap can also be overridden to any value >= 500 (step 250).

## Unit structure (worked example: Orks `Boyz`)

```
unit "Boyz"  costs: pts 85
  categoryLinks: Infantry, Battleline(primary), Mob, Boyz, Faction: Orks, Explosives
  constraints:  selections max 6   (army-wide copy limit for Battleline, see below)
                Leader association max 1, Support association max 1
  entryLinks:   Crusade group, Warlord, "Enhancements - Upgrades" group
  infoLinks:    rule "Waaagh!"
  profiles:     Abilities x3 (Never Too Busy to Fight, Ammo Runts, Tide of Muscle)
  modifiers:    pts set 170 if model count > 10;  pts +10 under a unit-count 'before' condition
  modifierGroups: Crusade, Green Tide detachment (annotates melee weapons "Sustained Hits 1"),
                  Shoota Boyz detachment (annotates ranged weapons "Assault")
  selectionEntryGroup "9-18 Boyz" (min 9 / max 18)
      model "Boy" (min 6, max 18) -> entryLinks Choppa, Slugga, Shoota (min1/max1 each)
      group "Special Weapons" (max 3, raised by modifier at >= 20 models)
```

Key lessons:

1. Points live as a base `pts` cost plus modifiers conditioned on **model count** (`childId: model`),
   **number of earlier copies of this unit** (`before` + `instanceOf`), or **context** (selected detachment,
   primary catalogue). Each of these must be enumerated/evaluated by the converter.
2. Detachment effects are implemented as modifier groups **on every affected unit**, conditioned on the
   detachment being selected, mostly annotating profiles or appending weapon keywords. The converter must
   invert these into `DetachmentEffect`s (see [03-mapping](03-mapping.md)).
3. Per-unit army copy limits are `selections max N` constraints on the unit (see [04-gaps](04-gaps.md) G4).

## Points-modifier survey (`tools/analysis/pts.py`, whole repo)

Counts of `pts` modifiers by what they depend on (top classes):

| Count | Kind | Maps to |
| --- | --- | --- |
| 405 | `increment` conditioned on `before` + same unit/model (scaling by earlier copies); 40 of these also depend on a selected detachment | `PointsDefinition.UnitCount` (and `PointsOverride` for the 40) |
| 258 | `set` conditioned on model count (`atLeast/greaterThan/equalTo/range`); 36 of these also depend on a selected detachment (Aeldari `Exhibition of Slaughter`, `Armoured Warhost`, `Kabalite Agonysts`) | `PointsDefinition.ModelCount` (and `PointsOverride` for the 36) |
| 12 | `increment` conditioned on **primary catalogue** (Agents of the Imperium assassins +15 pts outside that army) | **Gap G1** |
| >=100 | `increment/decrement` on `[Crucible]` characters' weapon choices (only the top 25 classes were printed, so this is a floor) | `WargearDefinition.Cost` (if Crucible kept; open decision O3) |

The survey printed only the 25 most common modifier classes; the long tail has not been classified.

## Unit-visibility (`hidden`) survey (`tools/analysis/hid.py`)

Why units are hidden in BSData (counts on `type: unit` entries):

- Roster toggles: `Show Legends`, `Boarding Actions`, `Crusade` -> all ignored (out of scope, see "Roster toggles and content sets").
- Library-catalogue / primary-catalogue gating (`Chaos - Daemons Library`, `Astra Militarum`,
  `Deathwatch`, `Space Wolves`, `Agents of the Imperium`) -> faction `Allies` and `UnitIncludeRule`.
- "Detachment selected / not selected" gating, mostly in Aeldari (units that need or exclude a detachment,
  Harlequin force) -> `Detachment.RestrictedUnits` / ally `AllowedDetachments` (to be validated).

## Detachments (`tools/analysis/det.py`)

263 detachments across all catalogues, all as children of a `Detachment` selectionEntryGroup in each
catalogue (e.g. Orks group id `8b85-ad3b-752f-6502`; the group can be per-catalogue).

- Detachment-point costs: 1 DP = 103, 2 DP = 113, 3 DP = 34, none = 13 (the `None` option).
- Rules per detachment: 0 -> 16, 1 -> 216, 2 -> 17, 3 -> 5, 4 -> 7, 5 -> 2. Rules sit in the entry's `rules`
  (some in `profiles` / `infoLinks`). See gap G5.
- Detachment tags are `categoryLinks` such as `Take and Hold`, `Purge the Foe`, `3DP Detachment`.
- Enhancements are in per-detachment groups (e.g. `Green Tide Enhancements`) hidden unless that
  detachment is selected; each enhancement has cost `pts` plus `Enhancements` = 1.
- Enhancement restriction pattern: `hidden` modifier unless the root entry is one of N specific units
  (e.g. `Wimp-kickaz`: NOBZ unit only) -> `Enhancement.RequiredUnitNames` / `RequiredKeywords`.
- Enhancement copy limit pattern: `selections max 3` at `force` scope plus `divide` modifiers on the
  `Enhancements` cost (2 copies -> each counts 1/2, 3 copies -> each counts 1/3): same enhancement may be taken
  up to 3 times, counting once toward the enhancement cap. See gap G2.

## Leaders

Leaders carry `associations` (`action: group`, `label: Leader` or `Support`, `max: 1`, `scope: force`)
with `conditionGroups` of `instanceOf` conditions listing the unit ids they may lead. Bodyguard units carry
association-count constraints (Leader max 1, Support max 1) and sometimes override modifiers.
Leader ids: `Leader` `1556-9b56-fba6-4370`, `Support` `7dcd-7f61-69a7-0294`.

## Per-unit army limits (`tools/analysis/limits.py`)

Unit `selections max` constraint at force/roster scope:

| Role | Max | Count |
| --- | --- | --- |
| Other (non-Battleline, non-Epic) | 3 | 354 |
| Battleline | 6 | 62 |
| Epic Hero | 1 | 12 |
| Other | 1 | 9 (e.g. Chaplain Grimaldus, Kill Team Cassius [Legends]) |
| Other / Battleline / Epic with no constraint | - | 27 / 4 / 2 |

## Roster toggles and content sets (what is in scope)

BSData hides optional content behind roster-level "Show ..." entries and force choices. Counts below are the number of
`hidden` modifiers (all catalogues) that depend on the toggle being **off** (`tools/analysis/toggles.py`).

| Toggle / content set | Hidden-modifier count | Our scope |
| --- | --- | --- |
| `Crusade Force` | 584 | Out (decision) |
| `Show Legends` | 389 | **Out: no Legends at all (D8).** 92 of 473 units are named `[Legends]` (about 19%); every one is dropped, together with anything that only exists for them |
| `Show Crucible Characters` | 62 | Out (D6) |
| `Show Imperial Agents`, `Show Imperial Knights`, `Show Chaos Knights`, `Show Titans`, Daemon god toggles (Khorne, Nurgle, Slaanesh, Tzeentch) | 48 / 23 / ... | **Ally visibility toggles.** Treated as "on": these allies are part of normal army building |
| `Show White Dwarf content` | 28 | **Out (D10)**: only Crusade content: `White Dwarf Battle Traits` and `White Dwarf Crusade Relics` groups in six catalogues, Crusade agendas (e.g. Leagues of Votann), Battle Honours and Crusade relics. No units |
| `Show Unaligned Forces`, `Show Unaligned Fortifications` | 16 (fortifications) | Out (D9), see below |
| `Show Killzone Upgrade: Shadowvaults content` | - | Out: a separate boxed-set mode |
| Detachment-like toggles (`Shadow Legion`, `Iconoclast Fiefdom`, `Devoted of Ynnead`, `Armageddon` ...) | various | Most are detachment selections, handled by the detachment conversion; `Armageddon` is campaign content (out) |

### Unaligned Forces (`Unaligned Forces.json`, library catalogue, revision 2)

"Unaligned" means units that belong to no faction: they carry the keyword `Faction: Unaligned Forces` and the ally
category `Allies: Unaligned Forces`, so any army can ally them when the roster toggle is on. Sixteen catalogues import it
(`catalogueLinks`), which is why it appears in every faction's ally list.

Contents (36 shared entries): **18 `[Legends]` entries** (fortifications: Aegis Defence Line, Bastion, Firestorm Redoubt,
Fortress of Redemption, Macro-cannon Aquila Strongpoint, Plasma Obliterator, Primaris Redoubt, Skyshield Landing Pad,
Vengeance Weapon Battery, Void Shield Generator, Vortex Missile Strongpoint, Wall of Martyrs Bunker/Defence
Emplacement/Defence Line, Imperial Fortress Walls, Castellum Stronghold; and odd creatures/drones: Ambull, Guardian
Drone, Spindle Drones, The Archivist), **3 Shadowvaults entries** (Sentry Gun, Searchlight, Breaching Robot, hidden unless
`Killzone Upgrade: Shadowvaults` is on), and **weapon upgrade entries** used by the fortifications. Nothing in it is
available without either `Show Legends` or the Shadowvaults toggle.

Consequence: with Legends excluded (D8) and Shadowvaults out of scope, **Unaligned Forces contributes no units at all**.
Handling (D9, confirmed): do not create an Unaligned Forces faction or ally entry, and ignore `Allies: Unaligned Forces`.
The 16 catalogues that import it need no special care beyond not following that link.

Also: `Library - Astartes Heresy Legends.json` is a library of Legends-style content and falls under D8 as well
(to confirm when the catalogue graph is built).

## Things confirmed absent from BSData 11e

- **Stratagems**: none (only text mentioning them inside ability descriptions).
- Detachment *Restrictions* text: only inside rule bodies where present.

## Open data questions still to investigate

- Contents of the `Force Disposition` group (what the options are, how they interact with detachments:
  WarOrgan has `Detachment.ForceDispositions` and `DataSetFeatures.Dispositions`).
- Exactly how `Warlord` is encoded and whether some units are forced/forbidden warlords (maps to `UnitFlags`).
- How army rules (faction rules like Waaagh!) are attached (`rules` on catalogue root vs `infoLinks` on units).
- How Space Marine chapter catalogues override Space Marines units (replaced keywords? extra detachments?).
- Whether any weapon stat is modified by wargear choices via modifiers rather than separate profiles.

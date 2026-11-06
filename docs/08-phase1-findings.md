# Phase 1 findings

Results of the data-analysis pass planned in [05-plan](05-plan.md). Scope exclusions (Boarding Actions, Crusade, Unbound
Adversaries, Legends, Crucible, Unaligned Forces, Shadowvaults) are applied throughout. Scripts: `tools/analysis/p1_*.py`
(shared loader `bsd.py`). Counts are from BSData commit `11ad64c...`; they will drift.

## 1. How a faction's roster is assembled

- **A "unit" is a root-level `entryLink` whose target has `type: unit` or `type: model`.** A top-level `model` is a
  single-model datasheet (a character, a vehicle). Libraries hold both: for example the Aeldari Library defines 48 `unit`
  and 86 `model` entries. Counting only `type=unit` undercounts by about half.
- A faction catalogue's roster = its own root `entryLinks` (targets may sit in its own file, a library, another catalogue, or
  the game system, which contributes 2 non-unit entries: battle size and the like) **plus the root entries of every
  catalogue it links with `importRootEntries: true`**. The import is **not transitive**: chapter catalogues list the
  Imperial Knights Library and Agents of the Imperium explicitly even though Space Marines already imports them.
- **The ally graph is the `catalogueLinks` graph.** An imported entry whose owner catalogue differs from the roster's
  primary catalogue gets an `Allies: X` category through a `set-primary category` modifier, and is hidden unless the matching
  roster toggle (`Show Imperial Knights`, `Show Imperial Agents`, Daemon god toggles ...) is on. We treat these toggles as on.

| Catalogue group | Roster comes from | WarOrgan shape |
| --- | --- | --- |
| Self-contained: Necrons, Orks, T'au Empire, Leagues of Votann, Tyranids (+ `Library - Tyranids`), Adepta Sororitas, Adeptus Custodes, Adeptus Mechanicus, Grey Knights, Agents of the Imperium | own entries | one roster per faction |
| Aeldari: `Craftworlds`, `Drukhari` | root links into `Aeldari Library` (Craftworlds 104 entries, Drukhari 49); several entries carry a second `Faction:` keyword (Harlequins, Ynnari, Asuryani) | copy the linked entries into each roster; keep all faction keywords |
| Chaos: Daemons, Knights, CSM, Death Guard, Emperor's Children, Thousand Sons, World Eaters, Titanicus Traitoris | own entries + `Chaos Daemons Library`, `Chaos Knights Library`, `Library - Titans`, `Chaos Space Marines` (Daemons and Knights link 31 and 17 CSM entries) | primary-roster / ally includes |
| Astra Militarum | root links into `Astra Militarum - Library` (139 entries) | copy |
| Genestealer Cults | own 27 + `Astra Militarum - Library` (55 AM entries with `Faction: Astra Militarum`) + `Library - Tyranids` (18) | AM and Tyranid units are part of the GSC list; model as included allies |
| Space Marine chapters (Ultramarines, Blood Angels, Dark Angels, Black Templars, Space Wolves, Deathwatch, Imperial Fists, Iron Hands, Raven Guard, Salamanders, White Scars) | `Space Marines` (import) + a few chapter entries (Space Wolves 22, Black Templars 16, Dark Angels 16, Blood Angels 15, Ultramarines 12, others 0-3) | `IsIncludedInPrimaryRoster` ally on Space Marines |
| Imperial Knights, Adeptus Titanicus | `Imperial Knights - Library`; `Library - Titans` | own roster, ally everywhere |
| Dropped by scope | `Unaligned Forces`, `Library - Astartes Heresy Legends` | not converted |

Effective-roster sizes (non-Legends) are in the 25-160 range; the chapters are large because they include the Space Marines
list plus Knights and Agents allies.

### Space Marine chapters in detail

- The Space Marines catalogue carries **all 35 Space Marine detachments**, including chapter-specific ones (`Champions of
  Fenris`, `Angelic Inheritors`, `Blade of Ultramar`, `Marshal's Household`, ...). Chapter catalogues add **none**.
  Chapter-specific detachments are hidden when the primary catalogue is not that chapter (`primary-catalogue`
  `notInstanceOf`).
- Chapter gating of Space Marine units: Black Templars hide 10 (Gladiator tanks), Space Wolves hide 4 (Tactical Squad,
  Apothecary ...), Deathwatch shows 10 Deathwatch-only units and hides 5 (Scout Squad, Terminators ...); Dark Angels add
  categories to 27 units.
- Faction keywords: chapter units carry `Faction: Adeptus Astartes` plus `Faction: <Chapter>`; Deathwatch units live in the
  Space Marines file with `Faction: Deathwatch`.

**App implications** (see [07-app-behaviour](07-app-behaviour.md)):

- `FilterAndCloneBySubFactionInfo` with `IsIncludedInPrimaryRoster` keeps only units whose `FactionKeywords` has **exactly
  one** keyword equal to the ally's keyword. Space Marine base units must therefore be emitted with a single faction
  keyword, and chapter variants get theirs through `ReplaceFactionKeywords` in an include rule.
- With any `UnitIncludeRules` present, only units matched by some rule are included (allowlist). Excluding a unit for a chapter
  = leave it out of every rule's `AffectedUnitsFilter` (or use `ExcludeKeywordFilter`); `RemainingUnits` adds the rest.
- `GetDetachments(faction)` returns the roster's own detachments **plus the detachments of every roster named in
  `FactionInfo.AdditionalFactionKeywords`**. A chapter can reuse the Space Marine detachments through that mechanism; the
  chapter-only detachments need per-chapter visibility, which the model has no field for. Open: emit chapter-specific
  detachments only in that chapter's own roster (and the shared ones in Space Marines).

## 2. Force dispositions, detachment categories and tags (corrects an earlier note)

- `Force Disposition` is a required min-1/max-1 choice among five options: **Disruption, Priority Assets, Purge the Foe,
  Take and Hold, Reconnaissance.** An option is shown only if some selected detachment carries that category.
- Detachment categories: each ordinary detachment carries exactly **one** disposition (Take and Hold 41, Disruption 37,
  Priority Assets 36, Reconnaissance 34, Purge the Foe 32 as sole category); the 34 `3DP Detachment` entries carry the
  `3DP Detachment` marker plus two or three dispositions; 13 carry none; a handful carry detachment-specific categories
  (`Terminator Detachment`, `Ironstorm Detachment`, `Nightmare`, `Mutant` ...).
- **Mapping:** `Detachment.ForceDispositions` = the five disposition categories (display-only in the app).
  **`Detachment.Tags` must not be filled from these**: in the app, tags are *mutual exclusion* categories (two detachments
  sharing a tag cannot both be selected), and BSData has no such rule. Leave `Tags` empty. The user confirmed (D11) that
  the 11e rules do not forbid two detachments with the same disposition; BSData itself contains no rule text about
  dispositions, so this could not be checked in the data.
- Detachment selection constraints in BSData: each detachment `max 1`; the group's cap is 1 when Incursion + a 3DP
  detachment; otherwise none. Visibility depends only on mode (Boarding Actions) and, for chapters/Drukhari, the primary
  catalogue.
- Detachment points: 1 DP (103), 2 DP (113), 3 DP (34), none (13).

## 3. Warlord and enhancement eligibility

- **Warlord** is an optional `Warlord` entry offered only when the unit has the `Character` category (hidden when
  `Character` is not an instance of the root entry), plus Boarding-Actions logic that we drop. Exceptions are in ability text:
  "**must be your Warlord**" (9 abilities, e.g. `Supreme Commander` on Orks, Grey Knights, Emperor's Children, ...),
  "**cannot be (selected as) your Warlord**" (about 11, e.g. `Wolfkin`, `Sydonian Sentinel`, `Loyal Protector`,
  `Shadow Assignment`, `Manifestation of Destruction`), and the Titans' `Towering Example` (a Titan may be Warlord although not
  a Character, and Titans skip the detachment step). Mapping: `UnitFlags.MustBeWarlord`, `NotWarlord`, `MayBeWarlord`,
  derived from those ability texts; Titans get `MayBeWarlord`.
- **Enhancement eligibility** is expressed by *linking* an enhancement group into the unit. 77 of 83 Space Marine entries link
  `Enhancements`; Orks link either `Enhancements` (16 units) or `Enhancements - Upgrades` (37); Epic Heroes and most
  vehicles link none (90 units across Orks, Necrons, Space Marines, T'au have no link). The app itself requires
  `Character` or `Epic Hero` for ordinary enhancements (`ModelUtils`), so `UnitFlags.NoEnhancements` is needed only for
  Characters that have no link; units linked to `Enhancements - Upgrades` are the eligible bodies for **Upgrade**-flag
  enhancements (the max-3 pattern). The app offers Upgrade enhancements to any unit except Epic Heroes, so each one needs
  explicit requirements; see section 10, answer 2, for the resulting conversion rules.

## 4. Army rules

Army rules are **not** a distinct object in BSData; they are named rules attached by `infoLink` to (nearly) every unit.
Link counts (these include Legends entries, which are dropped later): Orks `Waaagh!` 73 (every Orks unit/model entry),
Necrons `Reanimation Protocols` 67 (every entry), Leagues `Prioritised Efficiency` 25 (every entry), T'au `For The Greater
Good` 44 (about two thirds of the 66 T'au entries; the rest to be investigated), Space Marines `Combat Doctrines` on 81 of
the 83 in-scope entries and `Templar Vows` on 58 (the Black Templars). Detachment rules are rules linked from entries *inside* the `Detachment` group, and unit-specific rules (`Da Boss`,
`Unstable Energies`) are linked from a handful of units. The catalogue root `rules` list also holds some (Orks: `Waaagh!`,
`War Cry`).

**Conversion heuristic:** `FactionRoster.ArmyRules` = rules linked from at least about half of the faction's root units, plus
rules in the catalogue root `rules` list; everything linked only from detachment entries is a detachment rule; the rest are
unit abilities. Space Marines have no catalogue-level rule, the rules sit elsewhere and are found by link target.

## 5. Weapon keywords and core abilities (regex lists)

- **5,568 weapon profiles**, 108 distinct keyword strings in 67 shapes (digits to `N`). The common shapes (counts): Devastating
  Wounds 482, Rapid Fire N 447, Blast 442, Pistol 420, Twin-linked 420, Assault 415, Sustained Hits N 379, Torrent 322,
  Ignores Cover 309, Heavy 285, Psychic 281, Lethal Hits 281, Anti-Infantry N+ 277, Precision 212, Hazardous 200, Melta N 169,
  Extra Attacks 169, Blast N 131, Anti-Vehicle N+ 123, Close-quarters 83, Indirect Fire 80, Lance 68, Anti-Fly N+ 58,
  Cleave N 50, One Shot 30.
- **Qualified keywords** exist: `Lethal Hits: non-MONSTER/VEHICLE` (40), `Sustained Hits 1: non-MONSTER/VEHICLE`,
  `Devastating Wounds: Infantry|Monster/Vehicle|PSYKER`. `Anti-X N+` covers about 15 targets (Infantry, Vehicle, Fly, Monster,
  Psyker, Monster/Vehicle, Character, Tyranids, Daemon, Walker, Titanic, Epic Hero, Chaos, non-Monster/Vehicle).
  Dice values appear (`Sustained Hits D3`, `Rapid Fire D3`, `Rapid Fire D6+3`).
- **Dirty data to normalise before matching**: spelling variants (`Twin Linked` vs `Twin-linked`, `Anti Vehicle 3+`,
  `ANTI-FLY 2` without plus), a non-breaking hyphen U+2011 in at least one keyword, a dangling `LETHAL HITS: non-`, two
  keywords run together without a comma (`Sustained Hits 2: Non-Monster/Vehicle Twin-linked`), and about 20 unit-specific
  keywords (`Conversion`, `Linked Fire`, `Overcharge`, `Hooked`, `Impaled`, `Hive Defences` ...) that need their own
  `WeaponAbilities` entries. The regex list must be generated from the data plus the game-system rule names, not hand-written.
- **Core abilities** are `infoLink`s to the 49 game-system shared rules; 47 occur in the data: Deadly Demise 572, Deep Strike
  398, Leader 359, Feel No Pain (plain 171, variants 3+*, 4+, 4+*, 5+, 5+*, 6+), Stealth 131, Scouts 126 (plus `Scouts 5"`-`8"`),
  Infiltrators 95, Lone Operative 72, Damaged 62, Fights First 54, Support 54, Firing Deck 50, Super-Heavy Walker 46, Hover 30,
  Crucible 26 (dropped), Cleave 21, ... `Feel No Pain 6+*` and `Scouts 9"` are defined but unused.

### Modifiers that change weapon profiles (maps to `WeaponStatAdjustment`)

Structured modifiers exist and are numerous: `append` on `Keywords` (475 ranged, 234 melee), `increment`/`decrement` on `A`, `S`,
`AP`, `D`, `BS`, `WS`, `Range`, and `replace` on `A`/`D`, plus text-only `annotation` appends (about 750) that mirror
keyword additions. So `Add`, `Subtract`, `Set` adjustments with `TargetStatId` in {A, S, AP, D, BS, WS, Range, Keywords} cover
them; `annotation` is a display duplicate and can be ignored when the keyword modifier is present.

## 6. Points and visibility: classification of the long tail

**In-scope points modifiers: 713** (excluding units named `[Crucible]`/`[Legends]`; only 2 of the 1,092 in the whole data
depend on Crusade):

| Count | Kind | WarOrgan |
| --- | --- | --- |
| 403 | `increment` conditioned on earlier copies of the same unit/model (`before`) | `PointsDefinition.UnitCount` tiers |
| ~257 | `set` / `increment` on model count (`atLeast`, `greaterThan`, `equalTo`, ranges, one `multiply`) | `PointsDefinition.ModelCount` tiers |
| 14 | depend on the primary catalogue (Agents of the Imperium entries) | gap G1 |
| ~20 | `set` on the count of a named model *category*/option (Eradicators, Hellblasters, Inceptors, Crusader Squad, Sanguinary Guard, Wolf Guard Headtakers with Hunting Wolves, Aquila / Decimus Kill Team "10 models") | usually equal to a total model count; the Headtakers are a genuine model-mix cost, to review per unit |
| 1 | depends on a detachment and Crusade | dropped |

So **cost depending on the mix of model types is rare** (about 15 units); most "category" conditions count the unit's base
models. The evaluator must treat counting a category as counting the models that carry it.

**Hidden modifiers: 4,801**, of which 2,219 depend on out-of-scope modes/toggles (Boarding Actions 609+157+67+51+18+5+3,
Crusade 516+231+57+15+10+5, Legends 351+24+10, Crucible 62). In scope, what hides things: keyword/category presence (1,421:
enhancement and option eligibility by unit keyword), other options being selected (314+129: wargear dependencies),
primary catalogue (172+71), selected detachment (113+36), ally toggles (83+71).

Non-zero `pts` costs sit on 882 upgrade and 1,016 model entries outside any unit entry (the models are mostly the
single-model datasheets themselves, so this is their base cost; the upgrades are shared wargear/option entries that get
linked into units) and on 53 entries inside units (22 models, 31 upgrades). The inside-unit ones become
`ModelComposition.UpgradeCost`, `WargearDefinition.Cost` and `UnitUpgrade.Cost`. Which shared upgrades carry a cost, and how
they are charged per model, is still to be worked out in the wargear conversion.

## 7. Leaders

316 root units carry `associations`. Labels: `Leader` 155, `Led by` 65, `Supported by` 47, unlabeled 42, a few `Support Weapon`,
`Loyally Protecting`, `Retainers`. The `Leader` association sits on the **leader** and lists the led units as `instanceOf`
conditions on **categories** (1,651 conditions; every unit has a category named after itself, so category id -> unit name
resolves them); `Led by` / `Supported by` sit on the bodyguard. 87 conditions are `atLeast` on an *upgrade* (a leader may
join only if some option is selected) and 32 are `lessThan`. Mapping: `LeaderInfo.UnitNames` for named units,
`AttachableByKeyword` for category-wide conditions, `LeaderType` null / `"Support"`. `Support Weapon`, `Loyally
Protecting` and `Retainers` are non-standard attachments to review individually.

## 8. Data hygiene needed in the converter

- Normalise Unicode (U+2011 non-breaking hyphen, U+00A0 non-breaking space in join strings, typographic apostrophes); the
  Python scripts crash on a Windows console unless `PYTHONIOENCODING=utf-8`.
- Strip `[Legends]`/`[Crucible]` units and every reference to them (leader lists, option lists, ally lists).
- Placeholder entries exist (`PLACEHOLDER_NAME` was seen in the Daemons Library).
- Stale `childName` labels in conditions (`Deepening Madness` etc.): resolve by `childId`, never by name.

## 9. Updates this implies elsewhere

Applied in the same commit: [03-mapping](03-mapping.md) (dispositions/tags, army rules, warlord, units-are-models),
[04-gaps](04-gaps.md) (G6 tag bullet, new item G9), [05-plan](05-plan.md) (phase-1 closed, converter pipeline),
[01-source-data](01-source-data.md) (unit definition).

## 10. Questions raised in phase 1, and their answers

1. **Can two detachments share a disposition?** Yes, no restriction (user decision D11). `Detachment.Tags` stays empty and
   dispositions are display-only.
2. **Which units may take `Upgrade` enhancements?** Read from the app (`ModelUtils.CanEquipEnhancement`):
   - A unit with `UnitFlags.NoEnhancements` never qualifies.
   - An **ordinary** enhancement needs a keyword containing `Character` and the unit must not be an `Epic Hero`.
   - An **`Upgrade`** enhancement needs no `Character`, but `Epic Hero` units are still excluded: so it may go on any other unit.
   - `IgnoreStandardEnhancementRules` lifts both restrictions and always applies the requirement lists.
   - The requirement lists (`RequiredUnitNames`, `RequiredKeywords`, `RequiredOneOfKeywords`, `ExcludedKeywords`, `RequiredAbilities`;
     keyword tests are substring matches) are applied when the user's "filter enhancements" setting is on, or always for
     `IgnoreStandardEnhancementRules`. The app does **not** use BSData's "group linked into the unit" eligibility.

   Conversion rules that follow: (a) a Character that has no `Enhancements` group link gets `NoEnhancements`; (b) for each
   enhancement, derive `RequiredUnitNames` (small sets, e.g. `Wimp-kickaz` -> Nobz) or `RequiredKeywords` /
   `ExcludedKeywords` from the units that link its group and from its own hide-conditions, so the filter reproduces
   BSData's eligibility; (c) every `Upgrade` enhancement **must** carry such a requirement, otherwise it would be offered to
   every non-Epic-Hero unit including vehicles when the filter is off.
3. **The ~20 category-count point conditions.** Not decided by the user ("I don't know"). Plan: the converter reviews each
   during unit extraction (a category count normally equals the number of base models) and reports only those that do not
   reduce to a model-count tier. Known real mix: Wolf Guard Headtakers with Hunting Wolves.
4. **Non-standard attachments are not a problem.** Only 7 associations (plus 42 unlabeled plain leaders, Chaos Daemons "Leading"):
   - `Support Weapon` (3: Aeldari D-Cannon, Shadow Weaver, Vibro Cannon Platforms, 0-1, attach to Guardian Defenders) ->
     `LeaderInfo` with `LeaderType = "Support Weapon"` (the model allows one attached unit per `LeaderType`).
   - `Loyally Protecting` (2: Nork Deddog, Ogryn Bodyguard, exactly 1, attach to Command Squad) -> `LeaderInfo` with a
     distinct `LeaderType`, and a required attachment if the app supports it (check `RequiredLeaderRule`).
   - `Retainers` (2: Necrons Cryptothralls and Canoptek Tomb Crawlers, 0-1, attach to a Cryptek) -> `LeaderInfo` with
     `RequiredLeaderKeywords = [Cryptek]` (the exact case the model comment describes).
   All are expressible; they only need a small per-case mapping table.

## 11. Open after phase 1

- The chapter-only detachment layout (gap G9) needs a check that `AdditionalFactionKeywords` can name a non-ally roster.
- Whether the app can require an attachment (`Loyally Protecting`): read `RequiredLeaderRule`.
- Whether single-faction-keyword emission works for Deathwatch units that also carry `Faction: Adeptus Astartes`.

## 12. Scope rule discovered while checking stratagems: Boarding Actions-only detachments

13 detachments are hidden in the plain game: their `hidden` modifier is `notInstanceOf Boarding Actions` (visible only in
Boarding Actions). Example: Orks `Ramship Raiders` and `Kaptin Killers`. Wahapedia's Orks list (15 detachments) matches
BSData's plain-game Orks detachments exactly. The converter must therefore **drop detachments that are only visible in
Boarding Actions** (and Crusade-only ones), not just ignore the toggle: of 263 detachments, the 13 above are out.

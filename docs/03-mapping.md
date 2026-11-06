# Mapping: BSData concept -> WarOrgan field

Status column: **OK** = representable as is, **WORK** = representable but needs non-trivial conversion logic,
**GAP** = see [04-gaps](04-gaps.md), **?** = not yet investigated.

## Dataset level

| BSData | WarOrgan | Status | Notes |
| --- | --- | --- | --- |
| One catalogue per faction (`Orks.json`, ...) | `FactionInfo` + `Rosters/<Name>.json` | OK | `Category` groups the faction picker (Imperium / Chaos / Xenos ...) - catalogue file prefixes hint at it |
| `Faction: <X>` category | `FactionInfo.FactionKeyword` and `Unit.FactionKeywords` | OK | |
| Library catalogues (`Aeldari - Aeldari Library`, ...) | Units copied into each consuming faction's roster, or modelled as `Allies` with `UnitIncludeRule` | WORK | Prefer copying shared units into each roster that links the library. Which catalogues import which library is not yet mapped (`catalogueLinks` must be read) |
| Space Marine chapters (catalogue imports Space Marines) | Ally with `IsIncludedInPrimaryRoster: true`, chapter keyword via `AdditionalFactionKeywords` / `ReplaceFactionKeywords` | WORK | Only one primary-included ally per faction |
| `Allies: <X>` categories on the Army Roster force | `FactionInfo.Allies[]` | WORK | `ArmyPercentageRestriction`, `AllowedDetachments` need the actual ally rules (not in BSData as numbers - to source) |
| Battle Size entries | `BattleSizeDefinitions` | OK | See table in [01-source-data](01-source-data.md); `PointsTotal` must be unique; DP 2/3/4, enhancements 2/4/4, **`UnitLimit` = 3** (G3 solved, see [04-gaps](04-gaps.md)) |
| `Override points limit?` (>=500, step 250) | none needed | OK | The app maps any points limit to the first battle size with `PointsTotal` >= limit |
| `sharedRules` (Sustained Hits, Anti, ...) | `WeaponAbilities` / `CoreAbilities` regexes | WORK | Convert names with numbers into regexes with a `value` group; one regex must exist for **every** keyword string used in data, see validation notes |
| Category entries used as roles (Character, Battleline, ...) | `CategoryKeywords` | OK | Must occur on a unit keyword |
| Force Disposition group | `Features: Dispositions`, `Detachment.ForceDispositions` | OK | Five options (Disruption, Priority Assets, Purge the Foe, Take and Hold, Reconnaissance), shown when a selected detachment carries the category. Display-only in the app. See [08-phase1-findings](08-phase1-findings.md) |
| Stratagems | `CoreStratagems`, `Detachment.Stratagems` | n/a | Not in BSData, see [06-stratagems](06-stratagems.md) |

## Unit level

| BSData | WarOrgan | Status | Notes |
| --- | --- | --- | --- |
| Root-linked `selectionEntry` of `type=unit` **or `type=model`** (a top-level model is a single-model datasheet) | `Unit` | OK | `Id` = stable slug of the BSData entry name (+ catalogue where names clash); keep BSData `id` in a side map for diffing, not in the dataset |
| `[Legends]` suffix in name | none | OUT | Decision D8: drop these units entirely (no Legends roster, no `IsLegend`). Drop leader/option references to them |
| `Unit` profile `M,T,Sv,W,LD,OC,InSv,Base` | `StatLine.Stats` + `BaseInfo` | WORK | `Base` text ("32mm", "170 x 105mm Oval", "Hull", "Flying base") -> `BaseInfo.Type/Size`; all values must be non-empty |
| Multiple `Unit` profiles on a unit (models with different stats; vehicles with `Damaged`) | multiple `StatLine`s with `StatlineRef` per `ModelComposition` | OK | Each statline must be referenced or `AlwaysShow` |
| `type=model` entries inside unit | `ModelComposition` with `Limit{Min,Max}` | WORK | Model min/max in BSData is often a group + per-model constraints, possibly modified by other selections |
| Model `entryLinks` to weapon upgrades | `ModelWargear.InitialWargear` (min=max=1 links) and `Options` | WORK | The hardest part: replacement groups -> `WargearOption{Replaces, Options, Max, PerXModels}` |
| Weapon `Ranged/Melee Weapons` profiles | `WeaponSet{Id}` containing `Weapon`s | WORK | One weapon-set per BSData weapon entry; its profiles (e.g. blast + frag modes) become `Weapon`s sharing the set id. `Keywords` is a comma string; strip `[ ]` per element |
| Weapon `Range` `A BS/WS S AP D` | `Weapon.Stats` with ids `Range, A, BS or WS, S, AP, D` | OK | |
| Profile type `Abilities` | `Unit.UnitAbilities` (`UnitInfoSection`) | OK | Keep Markdown; the validator warns above 4 lines of `\r\n` text |
| `Transport`, `Psychic Abilities`, ad-hoc profile types | `Unit.Infos` text sections | WORK | Flatten random tables (D6 tables) into text |
| `rules` / `infoLinks` rule on a unit (e.g. Waaagh!, Leader) | `UnitAbilities` or `CoreAbilities` | WORK | Core ability (Deep Strike, Scouts 6", Feel No Pain 5+) -> `CoreAbilities` string matched by regex |
| Category links (non-faction, non-role) | `Unit.Keywords` | OK | Strip `Faction: ` prefix for faction keywords |
| Unit cost `pts` base + modifiers | `Points[]` | WORK | Evaluate modifiers over model counts (model-count tiers) and prior copies (`UnitCount`) |
| Per-model extra cost (model-level `pts`) | `ModelComposition.UpgradeCost` | WORK | Used for add-on models that are not part of base unit size |
| Wargear/upgrade cost | `WargearDefinition.Cost` or `UnitUpgrade` | WORK | |
| Wargear that changes stats or grants abilities | `WargearDefinition{StatAdjustments, WeaponAdjustments, GrantsWeapons, GrantsAbilities, DatasheetAdjustments}` | WORK | Most BSData uses `annotation` text modifiers rather than structured ones - must be parsed or left as ability text |
| Unit copy limit `selections max 3/6/1` | `BattleSizeDefinition.UnitLimit` + keywords `Battleline` / `Epic Hero` | OK, 9 exceptions | App derives 3 / 2x / 1 from keywords (G4) |
| `Warlord` entry (offered on `Character`) plus ability text "must be your Warlord" (9) / "cannot be (selected as) your Warlord" (~11), and Titans' `Towering Example` | `Unit.Flags` (`MustBeWarlord`, `NotWarlord`, `MayBeWarlord` for Titans) | WORK | Derived from ability-text patterns; see [08-phase1-findings](08-phase1-findings.md) section 3 |
| `associations` Leader/Support | `LeaderInfo{UnitNames, AttachableByKeyword, LeaderType}` | WORK | Resolve unit ids in conditions to unit ids. `LeaderType = null` (default Leader), `"Support"`; keyword-based leading from `instanceOf` category conditions |
| Bodyguard association constraints / overrides | `AttachedUnitInfo.MaxLeaders[]` | WORK | |

## Detachments, enhancements, army rules

| BSData | WarOrgan | Status | Notes |
| --- | --- | --- | --- |
| `Detachment` group children | `FactionRoster.Detachments[]` | WORK | Detachments may exist in several catalogues (generic vs chapter) |
| Detachment `Detachment Points` cost (1-3) | `Detachment.Cost` | OK | |
| Detachment `categoryLinks`: the five dispositions | `Detachment.ForceDispositions` | OK | **Not** `Tags`: in the app, tags are mutual-exclusion categories and BSData has no such rule. Leave `Tags` empty. `3DP Detachment` is only a marker (drives `Cost` 3) and the few detachment-specific categories (Terminator Detachment, ...) are dropped |
| Detachment `rules` | `Detachment.Rule` (single) | GAP G5 | Merge multiple rules into one text |
| Detachment effect expressed as modifier groups on units | `Detachment.Effects[DetachmentEffect]` | WORK | **Inverse of how BSData stores it.** For each detachment, scan every unit's modifier groups conditioned on that detachment; collapse units sharing the same effect into one `DetachmentEffect.AffectedUnits` list; or use `AffectedUnitsByKeyword` when it matches a keyword (BOYZ, ORKS INFANTRY) |
| Detachment-conditioned points | `DetachmentEffect.PointsOverride` | WORK | |
| Detachment-conditioned wargear options | `WargearOption.RequiredDetachment` | WORK | |
| Detachment gating units | `Detachment.RestrictedUnits`, `Detachment.AdditionalFactionKeywords` | ? | Semantics of `RestrictedUnits` need confirming (allow-list or deny-list) |
| Enhancement group per detachment | `Detachment.Enhancements[]` | WORK | `Cost` from `pts`; restrictions from hide-modifiers -> `RequiredUnitNames/RequiredKeywords/ExcludedKeywords/...` |
| Enhancement copy limit (max 3, count once) | `EnhancementFeature.Upgrade` | OK | Flag the ~84 entries with the max-3 + divide pattern (G2 solved) |
| Enhancement effects (stat, weapon, keywords, abilities) | `StatAdjustments`, `WeaponStatAdjustments`, `DatasheetAdjustments`, `AddedWeapons` | WORK | Often only prose + annotation modifiers in BSData |
| Army rules: rules linked by `infoLink` from (nearly) every unit, plus catalogue-root `rules` | `FactionRoster.ArmyRules[]` | WORK | Heuristic: rules linked from at least ~half of the faction's units, plus root `rules`; rules linked only from entries inside the `Detachment` group are detachment rules. See [08-phase1-findings](08-phase1-findings.md) section 4 |

## Things that disappear because of scope

Every `Boarding Actions modifiers` group, `Crusade modifiers` group, `Crusade` entry links, Retinue / Pilot /
Order of Battle categories and Crusade cost types are removed by evaluating those forces as unselected.

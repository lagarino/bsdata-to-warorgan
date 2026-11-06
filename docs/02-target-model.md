# Target: WarOrgan.Data model and builder

Read from the local clone `C:\Users\ruido\DevProjects\WarOrgan.Data` (commit
`45f934b8efcf4c988cd2cfd124458d78f09920c7`). Namespaces are `WarOrgan.Model.*`. Needs the .NET 10 SDK.
Run the builder with `dotnet run --project WarOrgan.Data.BuildDataset`; it validates and writes `<DatasetId>.zip`.
The app imports the ZIP directly under **Data Sets -> Import from file**.

> App behaviour was read from the decompiled installed app, see [07-app-behaviour](07-app-behaviour.md);
> statements marked *(inferred)* below predate that and are superseded by it.

## Dataset layout

```
Assets/<Dataset>/
  README.md                  attribution; copied into the archive
  DataSetDefinition.json     DataSetDefinition
  Rosters/<Faction Name>.json   FactionRoster; file name must equal FactionInfo.Name + ".json"
```

`Program.cs` holds `defaultDatasetId`, `defaultVersion` and `assetsFolder`. Use `build` for data changes,
`minor` for format changes; keep the ID stable. `DataSetInfo.CurrentDataSetFormatVersion` = 1.
A roster file named `"{Name} Legends".json` marks its units `IsLegend` *(from the model comment)*.

## DataSetDefinition

| Field | Meaning |
| --- | --- |
| `Features` | flags: `ArmyRules, Detachments, Stratagems, Enhancements, Dispositions` (or `All` / `None`) |
| `Factions[]` (`FactionInfo`) | `Name, FactionKeyword, Category, MainColor, DarkColor, AdditionalFactionKeywords[], Allies[]` |
| `CoreAbilities[]`, `WeaponAbilities[]` (`RegAbility`) | `Regex` (named group `value`), `Description` with `{value}` substitution |
| `CoreStratagems[]` | `Stratagem`: `Name, Category, CPCost, Phase, When, Target, Effect, Restrictions, Options[]` |
| `BattleSizeDefinitions[]` | `Name, PointsTotal, DetachmentPoints, EnhancementLimit, UnitLimit` |
| `CategoryKeywords[]` | `Keyword, CategoryDisplayName` - keywords used to group units (e.g. Character, Battleline) |

`FactionAlly`: `FactionKeyword, AdditionalFactionKeywords, IsIncludedInPrimaryRoster,
ArmyPercentageRestriction, AllowedDetachments, UnitIncludeRules[]`. A faction may have **at most one**
`IsIncludedInPrimaryRoster` ally (`CoreFactionKeyword` uses `SingleOrDefault`). This is the mechanism for
"Ultramarines = Space Marines + chapter-specific units".

`UnitIncludeRule`: filters (`AffectedUnitsFilter, AllKeywordFilter, AnyKeywordFilter, ExcludeKeywordFilter`)
and effects (`AddedKeywords, RemovedKeywords, ReplaceFactionKeywords`) applied when units are cloned into the
primary roster. Rules partition units; add `UnitIncludeRule.RemainingUnits` last to include the rest unmodified.
**There is no points effect.**

## FactionRoster

`Name, Units[], Detachments[], ArmyRules[] (UnitInfoSection{Title,Text})`.

### Unit

`Id` (stable), `DisplayName`, `Faction`, `Flags` (`NotWarlord, MustBeWarlord, NoEnhancements, MayBeWarlord`),
`UnitComposition`, `UnitAbilities[]`, `Infos[]`, `SpecialAbility`, `ArmyEffects[]`, `Keywords[]`,
`CoreAbilities[]` (matched against `CoreAbilities` regexes), `FactionKeywords[]`, `StatLines[]`,
`Weapons[]` (`WeaponSet{Id,DisplayName,Weapons[Weapon]}`), `Upgrades[]` (`UnitUpgrade{Name,Cost}`),
`Points[]` (`PointsDefinition{UnitCount?,ModelCount,Cost}`), `LeaderInfo`, `AttachedUnitInfo`.

- `StatLine{StatName, Stats[Characteristic{Id,Value,Description}], Features(AlwaysShow), BaseInfo{Size,Type,Description}}`;
  `BaseType` = Round, Oval, Hull, SmallFlying, LargeFlying, Unique.
- `Weapon{Id, DisplayName, Keywords (comma string), Flags(NeverFilter), Stats[]}`.
- `UnitComposition{ModelCompositions[], WargearDefinitions[]}`:
  - `ModelComposition{ModelName, Limit{Min,Max}, StatlineRef, UpgradeCost?, Keywords[], Wargear[ModelWargear]}`
  - `ModelWargear{Models? (null=all), InitialWargear[weapon-set ids], Options[WargearOption]}`
  - `WargearOption{Max?, PerXModels?, Replaces[], Options[WargearChoice{Items[]}], RequiredDetachment?}`
  - `WargearDefinition{Key, DisplayName, Cost?, TagName, GrantsAbilities, GrantsWeapons, StatAdjustments, WeaponAdjustments, DatasheetAdjustments}`
- `LeaderInfo{UnitNames[], AttachableByKeyword[], LeaderType (null=default "Leader"; "Support"; "Bodyguard"), Description, Features(MultipleSelection, SoloLeader), RequiredLeaderKeywords, CanAttachAlongsideLeadersInfo, Abilities[LeaderAbilityRef]}`.
- `AttachedUnitInfo{MaxLeaders[MaxLeaderInfo{MaxLeaderOverride, LeaderType, RequiredModels, RequiresOneOfLeaders, PreventDuplicates}], LeaderAttachmentRules[]}`.

### Detachment

`Name, Cost (default 1), Rule (DetachmentRule: UnitInfoSection + Restrictions) - a single rule, Tags[],
ForceDispositions[], Stratagems[], Enhancements[], Effects[DetachmentEffect], AdditionalFactionKeywords[],
RestrictedUnits[]`.

`DetachmentEffect{AffectedUnits[], AffectedUnitsByKeyword[], AffectedUnitsExcludeKeyword[], RemoveAbilities,
AddedAbilities, AddedInfoSections, AddedLeaderAbilityRefs, StatAdjustments, DatasheetAdjustments,
WeaponStatAdjustments, PointsOverride[PointsDefinition]}`.

`Enhancement{Name, Description, Cost, Features(IsLeaderEnhancement, IgnoreStandardEnhancementRules, Upgrade, FreeEnhancementPointCost),
AddedWeapons, StatAdjustments, WeaponStatAdjustments, DatasheetAdjustments, RequiredAttachedUnitKeywords,
RequiredUnitNames, RequiredKeywords, RequiredOneOfKeywords, ExcludedKeywords, RequiredAbilities}`.

### Adjustments

`StatAdjustment{TargetStatId, Type(Set|SetIfLower|Add|Subtract|Double), Scope(Model|ModelWhileLeading|Unit|AttachedUnit|BodyguardUnitOnly|LeaderUnitsOnly), Value, Info, Min, Max, NameFilter}`.
`WeaponStatAdjustment` adds `WeaponSetId, ContainsNameFilter, IncludedKeywords, ExcludedKeywords, CreateRenamedCopy`.
`TargetStatId == "Keywords"` is a recognised convention for adding weapon keywords (see
`Validation/AbilityRegValidation.cs`).
`DatasheetAdjustment{Target(Keywords|UnitFlags|CoreAbilities|LedUnitNames|LeaderType|LeaderDescription|LeaderFeatures), Type, Scope, Values, EnumValues}`.
`ArmyEffect{Condition(None|UnitIsWarlord|UnitIncluded), AffectedUnits*, AllowedDetachments, AddedAbilities, StatAdjustments, DatasheetAdjustments}`.

## Builder validations (what a converter must satisfy)

All seven files in `WarOrgan.Data.BuildDataset/Validation/` have been read. Errors fail the build; warnings do not.
The converter's output must satisfy all of these.

**Dataset / faction level (`FactionInfoValidation`)**
- At least one faction; faction names and faction keywords unique (case-insensitive).
- Each faction needs **exactly one main roster file** `"<Faction Name>.json"`; roster filenames unique, valid
  file names, not `DataSetDefinition`/`DataSetInfo`. Every roster's `Name` must match a registered faction
  (so extra roster files such as a Legends file reuse the faction's `Name`).
- Colours must be `#RRGGBB`. `Features` may only contain known flags.
- Battle sizes: at least one; `PointsTotal > 0`; other fields >= 0; **`PointsTotal` unique**.
- Every unit must carry at least one faction keyword in `faction.AllFactionKeywords` plus any
  `Detachment.AdditionalFactionKeywords` of that faction's rosters, otherwise "unobtainable".
- Allies: the allied faction's roster must exist; `AllowedDetachments` must name existing detachments.
- Core/weapon ability regexes: unique, valid, non-empty description.

**Roster / unit level (`Validation`)**
- Roster not empty; unit IDs unique per roster; ID must not be `UNKNOWN`.
- `LeaderInfo.UnitNames` must be IDs of units that exist **in any roster**; every `LeaderInfo.Abilities[].AbilityId`
  must equal the `Title` of a unit ability/info section on that unit.
- Warning only: any ability text with more than 4 lines (split on `\r\n`).

**Abilities (`AbilityRegValidation`)**
- Every entry in `Unit.CoreAbilities` (plus ones added by `DatasheetAdjustments`) must match a
  `CoreAbilities` regex; every weapon keyword (`Weapon.Keywords` split by comma, plus keywords added by
  `WeaponStatAdjustment` with `TargetStatId == "Keywords"`) must match a `WeaponAbilities` regex after
  trimming `[` `]`. Matching is case-insensitive. **So the definition needs a regex for every keyword the data uses**
  (`SUSTAINED HITS 1`, `ANTI-INFANTRY 4+`, `MELTA 2`, `LETHAL HITS: non-MONSTER/VEHICLE` ...).

**Points (`PointsValidation`)**
- Each unit needs at least one `Points` entry; costs >= 0; `ModelCount > 0`; `UnitCount` must be present in
  **all or none** of a unit's entries; no duplicate `(ModelCount, UnitCount)` with conflicting costs.
- Warning only: a `ModelCount` outside the sum of base-model min/max limits.
- Model composition names unique; limits valid (`0 <= Min <= Max`); no negative upgrade/equipment costs.

**Statlines (`StatlineValidation`)**
- >= 1 statline, names unique, every model needs a `StatlineRef` when the unit has >1 statline, the ref must
  exist, **every statline must be referenced by a model unless it has `AlwaysShow`**.
- Characteristic IDs unique within a statline/weapon and **values must be non-empty** (so `-` or `N/A` placeholders
  are needed, not blanks).
- Weapon-set IDs unique per unit; weapon IDs within a set are validated (`ValidateIds`); firing modes share
  one set id so selecting it enables every profile.

**Wargear (`WarGearValidation`)**
- All wargear IDs non-empty and trimmed; every ID in `InitialWargear` / `Replaces` / `Options` must be a known
  weapon-set ID or `WargearDefinition.Key`; duplicate definition keys and duplicate choices are errors.
- Every weapon that is not `NeverFilter` must be **selectable** (appear in initial wargear, an option, or be
  granted by a selectable definition) - an orphan weapon profile is an error.
- `WargearDefinition.GrantsWeapons/GrantsAbilities` must reference existing weapon / ability titles.
- Options must have choices and positive `Max`/`PerXModels`; `RequiredDetachment` must name a detachment.
- **A model composition with more than one `ModelWargear` set may not have any `Options`** ("not handled in UI anymore").
  Mixed loadouts inside one model type must therefore be expressed with one `ModelWargear` plus `Max`/`PerXModels`
  options, or by splitting into separate `ModelComposition`s.
- Two replacement options with the same `Replaces` set and overlapping choices are an error.
- `DatasheetAdjustment` targets `LeaderDescription` / `LeaderType` need exactly one value and type `Set`.
- Warning: unit with no weapons.

**Category keywords (`KeywordValidation`)**
- Every `CategoryKeywords` entry must occur as a keyword on some unit (or model); a model keyword may not
  duplicate a unit keyword.

## Installed app vs. this model (checked 2026-11-06)

The installed app's embedded `WarOrgan.Data.Model` was decompiled and compared with this repo's model: **same types,
same members, same flag values, `CurrentDataSetFormatVersion = 1`**. A dataset built with this repo's builder is
therefore in the format the installed app reads. App behaviour is documented in
[07-app-behaviour](07-app-behaviour.md); that doc replaces every "(inferred)" statement in these notes.

The data folder in `%APPDATA%\WarOrgan\Data` is a different matter: it is a leftover from an older installation in the
**old format** (`DataInfo.json` v1.2.21, `FactionInfoData.json`, roster keys `Dettachments` and `InitalWargear`, typed
stat fields like `Movement`/`Toughness`/`Range`/`IsMelee`) and the user cannot load it in the current app. Do not
copy conventions from it, and do not use it as a content source (its detachments and stratagems are the
maintainer's own work).

Only .NET Core 3.1.8 runtimes were installed originally; the .NET 10 SDK (10.0.403) has since been installed for
this project.

## What the model shows about its intended game

The model was clearly shaped by 40k: `Support` leader type, Force Dispositions, enhancement points,
Legends, `Warlord` flags, `ModelWhileLeading` scopes, weapon keyword conventions. The Fracture example is
a tabletop game with a different stat vocabulary, so characteristic IDs (`Move`, `Defence`, `Range`,
`Attacks`) are free-form strings; for 40k we will use `M, T, Sv, W, LD, OC, InSv` and `Range, A, BS/WS, S, AP, D`.

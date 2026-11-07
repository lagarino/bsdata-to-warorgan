# WarOrgan app behaviour (from the installed app)

How this was obtained: the installed app (`C:\Program Files\War Organ\War Organ.exe`, an Avalonia .NET
single-file build) was decompiled locally with `ilspycmd` (`dotnet tool install -g ilspycmd`;
`ilspycmd -p -o <dir> --bundle-entry <Assembly>.dll "War Organ.exe"`). Assemblies of interest inside the bundle:
`WarOrgan.dll` (UI, services, validation rules), `WarOrgan.Core.dll` (effective-unit logic),
`WarOrgan.Data.Model.dll` (the model). **The decompiled sources are not kept in this repo**; re-create them with the
commands above when needed. File and class names below refer to the decompiled output.

Version note: the exe reports file version **1.3.0**, while the WarOrgan.Data README says dataset import needs
"WarOrgan 3.0 or later". The version numbers are unexplained, but the app does import ZIP datasets
(`DataSetFile.LoadDataSetFile` reads `DataSetInfo.json`) and its embedded `WarOrgan.Data.Model` is **identical** to
the repo's model (same types and members, `CurrentDataSetFormatVersion = 1`, checked property by property and the
three flag enums by hand). The app accepts only `DataSetFormatVersion == 1`; anything else is "Unsupported".

## 1. The dataset ID is hard-wired (important for decision O7)

The app has built-in behaviour for two dataset IDs: two ids (one per supported edition), called the built-in ids below.

| Behaviour | Where | Any other dataset ID |
| --- | --- | --- |
| Army validation rules (`ArmyValidationService.GetValidationRules`) | returns an **empty rule list** unless the ID is one of the two | **No validation except the points cap** |
| TTS export, YellowScribe export | `ExportPageViewModel.GetSupportedExportTypes` | hidden |
| GW / WTC / WTC-compact text export | `ExportTextViewModel` | hidden (only WarOrgan, WarOrgan with categories, Simple) |
| Import army from text | `CreateNewArmyViewModel.CanImportFromText` (11th only) | hidden |
| `SupportCharacterRule` ("must be attached to a bodyguard") | checks ID == 11th | off |

Rules switched off for other IDs include: max duplicate datasheets, max one Epic Hero, unique enhancements,
warlord rule, required leader, solo leader, allies points limit, Damned units, and **~60 faction-specific rules**
(`WarOrgan.ArmyValidation.<Faction>` namespaces: Tactical Marines, Calgar, Kasrkin, Broadside Battlesuits,
`BloodLegions` ... ). Those faction rules are keyed on **unit IDs/names and detachment names** (for example
`Detachment 14`), so they would apply to any dataset that reuses a built-in ID, correctly or
spuriously depending on how closely our names match.

Everything else (list building, points, leaders, wargear, detachments, PDF/text/share-code export) works for any ID.

## 2. Answers to the open questions

| # | Question | Answer (source) |
| --- | --- | --- |
| Q1 | How are per-unit copy limits computed? | `MaxUnitTypes`: limit = `BattleSizeDefinition.UnitLimit`; **Battleline** (keyword `Battleline`) = 2 x UnitLimit; **Dedicated Transport** = 6 (hard-coded); `MaxOneEpicHero` = 1 per unit id for keyword `Epic Hero`. Only active for the two built-in IDs. No per-unit exceptions exist. |
| Q2 | Does `EnhancementFeature.Upgrade` mean "up to N copies counting once"? | **Yes.** `UniqueEnhancements`: enhancements are grouped by name; the count of *unique* names (excluding those flagged `FreeEnhancementPointCost` = 8) must not exceed `EnhancementLimit`; an enhancement with `Upgrade` (4) may be on **up to 3 units**, others on 1; at most one enhancement per attached unit. This matches the BSData 11e pattern exactly (gap G2 solved, see below). |
| Q3 | What is `UnitLimit`? | Max copies of the same datasheet (default 3 for 11e), see Q1. **Use 3, not 0.** (0 would flag every unit) |
| Q4 | Would you accept model changes? | Not asked; with the findings below only G1 still needs one. |
| Q5 | `Detachment.RestrictedUnits` | **Deny-list** by unit `Id`: `WithDetachmentEffects` drops any unit whose Id appears in `RestrictedUnits` of a selected detachment. Units whose faction keywords are not in faction + detachment `AdditionalFactionKeywords` + allies are also dropped. |
| Q6 | Custom points limits | `GetDefinitionByPoints`: sorts battle sizes by `PointsTotal` and picks the **first with `PointsTotal >= army max points`**, else the last, else a default (2000 pts, 3 DP, 1 enhancement, 1 unit). So any limit works and maps to the tier at or above it. |
| Q7 | Legends roster naming | `FactionManager.LoadRoster`: reads `"<FactionInfo.Name>.json"`, then `"<Name with ' Allies' removed> Legends.json"` and marks those units `IsLegend`. |

## 3. Facts the converter must respect

### Points (`PointsCalculationExtensions.WithCalculatedPoints`)

- `ModelCount` is a **ceiling**: the app orders the unit's `Points` by `Cost` ascending and takes the **first entry whose
  `ModelCount >= models in unit`**; if none, the last. So a tier "9-10 Unit 10 = 85, 11-18 = 170" is written as
  `{ModelCount 10, Cost 85}, {ModelCount 18, Cost 170}`. Costs must be non-decreasing with model count.
- `UnitCount` is also a **ceiling per copy index**: with `n = min(copies already in the list + 1, max UnitCount)` it keeps
  entries with `UnitCount >= n`, then applies the model-count rule. "First two copies 100, later copies 110" is
  `{UnitCount 2, Cost 100}, {UnitCount 3, Cost 110}` (every model-count tier repeated for each UnitCount).
- Models that have `ModelComposition.UpgradeCost` are removed from the model count and charged separately
  (per model); `WargearDefinition.Cost` is charged per model that carries that wargear key; `UnitUpgrade.Cost`
  is added; the enhancement's `Cost` is added.
- A `DetachmentEffect.PointsOverride` replaces the unit's `Points` list.

### Detachments (`MultiDetachmentOptionsViewModel`, `DI.CreateDetachmentOptionsViewModel`)

- If the battle size's `DetachmentPoints < 2`, the user picks exactly one detachment (first in list by default).
  Otherwise multi-select: the **first** detachment can always be picked regardless of cost; further ones need
  `Cost <= remaining points` **and no shared `Tag` with any already selected detachment**. So `Tags` mean
  "mutually exclusive categories". Only tags that the 11e rules treat as exclusive may be emitted.
- `ForceDispositions` are display-only (`DetachmentInfoViewModel.DisplayForceDispositions`).
- Consequence for gap G3: a 3-point detachment can always be chosen first, so an Incursion cap of 2 behaves like the
  conditional 3 (it leaves no points either way). **G3 is not a real problem.**

### Keywords the app compares literally

`"Epic Hero"`, `"Battleline"`, `"Dedicated Transport"`, and a substring match on `"Character"`. They must be present
in `Unit.Keywords` with exactly this spelling and case, as must the god keywords used by the summoned allies rule
(`Khorne`, `Tzeentch`, `Nurgle`, `Slaanesh`).
`LeaderInfo.LeaderType` default is `"Leader"`; `"Support"` is recognised by `SupportCharacterRule`.

### Rosters and allies (`FactionManager.CreateUnitCache`)

- A faction's units = its own roster (`<Name>.json` + legends) plus, for each `FactionAlly`, the allied faction's
  roster loaded **by faction keyword** and passed through `FilterAndCloneBySubFactionInfo` (the `UnitIncludeRule`s).
  Allied factions therefore must be registered in `DataSetDefinition.Factions` with their own roster file.
  `AllowedDetachments` restricts when those allied units are available.
- Allied points caps are enforced by `GeneralAlliesPointsLimit` (only for the built-in IDs) using
  `FactionAlly.ArmyPercentageRestriction`.
- `UnitIncludeRule` has no points effect (gap G1 stands).

## 4. Dataset import, identity and reversibility (`DataSetService`, `DataSetUtils`)

- Importing a ZIP copies it into `%APPDATA%\WarOrgan\Data\DataSets` as `<new guid>.zip`. Existing datasets are never
  overwritten, so several datasets with the same ID and/or version can coexist as separate files.
- A dataset is identified by `DataSetInfo.Id` + `Version`. Selecting by ID (`FindDataSetMatch`) uses the **exact
  requested version if present, otherwise the highest version** with that ID. Lists store the dataset ID they were
  created with (`CreatedWithDataSetId`) and unit IDs, so they need a dataset with that ID and compatible unit IDs.
- The app can also pull datasets from URLs (`CheckDataSourceAsync`, stored in `DataSets\config.json` `Sources`).
- A dataset can be deleted again from the Data Sets page (`DeleteDataSetAsync` by id, by version, or all versions).

Consequences for the dataset-ID choice (decision D7): reusing a built-in id with a **higher version** than any other
dataset of that ID makes ours the one selected for that ID, and it is undone by deleting it (or by importing something
with a higher version). Lists created while using it keep the ID but reference our unit IDs, which is the only truly
sticky part. Use a clearly high `datasetVersion` deliberately, and a different one when you do not want to override.

## 5. What the data folder in `%APPDATA%\WarOrgan` is

A leftover from a previous installation: **old format**, incompatible with the current app and model
(`Dettachments`, `InitalWargear`, typed stat fields, `DataInfo.json` / `FactionInfoData.json`, version 1.2.21).
The user cannot load it in the current app. It is **not** a reference for the format and is not used as a content source.

# Plan

Principle: **generate, don't hand-edit.** The dataset is the output of a deterministic converter, so it can be
regenerated when BSData moves. Hand-written facts that BSData lacks (stratagems, ally percentages, overrides)
live in small curated files next to the converter and are merged in.

## Phase 0 - Repo and decisions  (now)

- [x] Analyse source and target; write these docs.
- [x] Create the GitHub remote (private).
- [x] Decided: C# converter (D4), tool-only repo with local-only output (D5), no Crucible (D6).
- [x] Install the .NET 10 SDK; read the installed app's behaviour ([07-app-behaviour](07-app-behaviour.md)).
- [ ] Remaining open decisions: O4, O5, **O7 (dataset ID, see gap G7)** in [00-scope-and-decisions](00-scope-and-decisions.md).
- [ ] Add a "points" section to the converter spec from [07-app-behaviour](07-app-behaviour.md): tiers are ceilings
      (`ModelCount`, `UnitCount`), sorted by cost, so the emitter must produce **non-decreasing** costs and the evaluator
      must emit `ModelCount = upper bound of each cost run`.
- [ ] Conversion rules fixed by app facts: `UnitLimit` 3; battle sizes 2/3/4 DP and 2/4/4 enhancements; `Upgrade`
      flag for the max-3 + divide enhancements; keywords `Epic Hero`, `Battleline`, `Dedicated Transport` exact;
      `Detachment.Tags` only for exclusive categories (verify against BSData rule text).

## Phase 1 - Finish the analysis  (DONE, see [08-phase1-findings](08-phase1-findings.md))

Close the "open data questions" in [01-source-data](01-source-data.md):

1. Extract the `Force Disposition` group and see how it affects detachments.
2. Find how `Warlord` is encoded and which units are forced / forbidden.
3. Find where army rules live (catalogue `rules`, root `infoLinks`, `sharedRules`) for Faction AE, Faction AF, the parent faction.
4. Map `catalogueLinks` between all 48 files (who imports which library / sub-factions / Unaligned Forces).
   Output: a table in `docs/` plus a machine-readable `catalogue-graph.json` in `tools/`.
5. Sub-faction overrides: diff `Category 1 - Sub-faction a.json` against `Category 1 - the parent faction.json`.
6. Classify the **long tail** of point modifiers and hidden modifiers (only the top 25 classes were printed).
7. Classify weapon-profile modifiers (stats changing by wargear or detachment) vs keyword `annotation` text.
8. Check that no points depend on the *mix* of model types (G6 last bullet).
9. Check all keywords in use: collect the distinct weapon keyword strings and core ability strings (needed
   for the regex lists in `DataSetDefinition`).

Deliverable (delivered): [08-phase1-findings](08-phase1-findings.md), updates to the mapping and gap docs, and `tools/analysis/p1_*.py`. The open items that remain are listed in section 10 of that document.

## Phase 2 - Stratagem source: Wahapedia (decision D12; method O11 open)

Follow [06-stratagems](06-stratagems.md): obtain real Wahapedia HTML, write and verify a parser, define the in-memory stratagem schema and the phase-normalisation table, and match detachments by name against BSData. **Nothing from Wahapedia is stored in this repo** (D5); the converter reads it from a user-supplied location at run time, and the dataset README credits Wahapedia.

## Phase 3 - Converter design and skeleton

Proposed (open decision O1: C# leaning):

```
tools/Converter/            (.NET 10 console, references WarOrgan.Data.Model)
  Load/        BSData JSON -> typed Bsd* classes (only fields we need)
  Resolve/     resolve catalogueLinks + entryLinks + infoLinks into a flattened entry tree
  Eval/        condition & modifier evaluator over a "scenario" (see below)
  Extract/     Units, Weapons, Wargear, Points, Leaders, Detachments, Enhancements, Rules
  Emit/        WarOrgan.Model.* objects -> JSON in the dataset Assets folder
  Overrides/   curated JSON (stratagems, ally rules, id remaps, manual fixes)
  Report/      per-faction conversion report: unconverted modifiers, warnings, stats
```

**Scenario evaluation (core idea).** BSData state is a pile of conditional modifiers. Instead of interpreting
them in the abstract, the converter builds a *scenario* (a set of facts: battle size, selected detachment,
model counts, number of earlier copies, primary catalogue) and evaluates the BSData entry under it; then it
enumerates the scenarios that matter:

- Baseline scenario: Boarding Actions and Crusade **not** selected, no detachment, Strike Force, primary
  catalogue = the faction being converted.
- Points: vary model count over each unit's allowed range and copy index 1..N -> `Points[]` (collapse runs of
  equal cost to the lowest count only if the validator and app expect that - check with the example dataset).
- Detachment effects: for each detachment, evaluate each unit with and without it selected and diff the result
  (points, weapon keywords, abilities, stats) -> `DetachmentEffect`.
- Wargear: evaluate option groups under the baseline and under each detachment that changes them
  (`RequiredDetachment`).

Anything the evaluator cannot interpret goes to the report as "unconverted modifier", never silently dropped.

**Pipeline stages implied by phase 1** (details in [08-phase1-findings](08-phase1-findings.md)):

1. **Load and index** all 48 files; build id -> node maps; resolve `entryLink`, `infoLink` and `catalogueLink` targets by id
   (never by name; `childName` labels are stale).
2. **Scope filter**: drop Legends/Crucible units by name, Unaligned Forces and the Legends library by file, and evaluate with
   Boarding Actions and Crusade unselected. Drop every reference to dropped units.
3. **Effective roster per faction**: own root links + root entries of directly linked catalogues
   (`importRootEntries`, non-transitive); a unit is a root-linked `unit` or `model`.
4. **Faction model**: derive `FactionInfo` entries, `AdditionalFactionKeywords` and `Allies` (primary-roster include for
   sub-factions, plain allies for Knights / Agents / Faction AI) from the catalogue graph; sub-faction gating becomes an allowlist
   `UnitIncludeRule`.
5. **Unit extraction** with the scenario evaluator: statlines, model compositions, weapons (a `WeaponSet` per linked weapon
   entry), wargear options, points tiers (ceilings, non-decreasing, see 07), flags (Warlord from ability text), leaders.
6. **Keyword and core-ability normalisation** (Unicode, spelling variants, run-together keywords) and generation of the
   `WeaponAbilities` / `CoreAbilities` regex lists from the data.
7. **Detachment extraction**: cost, dispositions, rule text (merge several rules), enhancements (flag `Upgrade` for the max-3
   pattern), effects by differencing unit state with and without the detachment; army rules by the link-count heuristic.
8. **Emit, validate, report**: write the dataset, run the builder validation, produce the unconverted-modifier report and
   (D7) the rule-compatibility report.
`annotation` modifiers (text appended to profiles) must be recognised and turned into keyword adjustments
where they carry rules keywords (e.g. SUSTAINED HITS 1), otherwise surfaced as ability text.

## Testing policy

Every converter phase lands together with its tests, and a phase is not done until they pass.

- **Unit tests** (`tests/Converter.Tests`, xUnit) use small synthetic BSData fixtures (`Fixtures.cs`), one test class per
  stage: loader and scope, roster resolution, statlines/weapons/keywords/abilities, compositions and points, wargear. They
  encode each behaviour verified by hand on the real data (for example ceiling tiers, copy-index tiers, wrapper
  choices, unit-level fallbacks) so regressions are caught at build time. No real game text goes into fixtures (D5).
- **Integration tests** (`IntegrationTests.cs`) run only when `BSDATA_SRC` points at a BSData checkout. They assert
  structural invariants on the real catalogues (every unit has statlines, models and points; every model refers to an
  existing statline; wargear ids resolve; every weapon is selectable; in-scope points conditions are understood;
  ids are trimmed). They found two real bugs on first run (missing statline reference for variant models, duplicate
  model names), which is their purpose.
- **Later phases** add: dataset emission and builder validation (run `WarOrgan.Data.BuildDataset` validation on the
  generated dataset in a test), leaders, detachments and enhancements, stratagem parsing (synthetic HTML fixtures),
  downloader and cache (fake HTTP handler, temp folder).
- Run `dotnet test` before every commit; a CI workflow is added with the release workflow.

## Phase 3 - Releases (binary distribution)

Publishing prebuilt executables as GitHub Releases (for example `v0.1.0`) is feasible:

- The tool contains no BSData or Wahapedia content (both are read at run time from the user's machine).
- `WarOrgan.Data` is MIT-licensed, so redistributing a build that embeds the model library is allowed; each release must ship a
  `THIRD-PARTY-NOTICES` file with its MIT text and copyright line (zenchovey, 2026).
- Plan: `dotnet publish` self-contained single-file for win-x64, linux-x64, osx-arm64, built by a GitHub Actions workflow on version tags.
- Embedded data files must contain only our own mappings and names, never rules text from BSData or Wahapedia.
- This repo's own code is MIT-licensed (`LICENSE`); the release package also carries WarOrgan.Data's MIT notice.
- The repo is private, so releases are visible to collaborators only until the repo is made public (a separate decision).
- `dotnet publish` copies the builder project's sample `Assets` folder (an unrelated example dataset) next to the program; exclude it from release packages.
- Wahapedia has no licence: the stratagem step stays optional (`--no-stratagems`), with a first-download notice.

## Phase 3a - Converter settings (decision D7)

Settings come from a JSON file and/or command-line options; none are hard-coded:

| Setting | Notes |
| --- | --- |
| `bsDataPath` | checkout of the BSData repository |
| `outputPath` | where the dataset folder and ZIP are written; must be outside any git repo or ignored |
| `datasetId` | **required, no default.** Either an own ID or the app's hard-wired dataset id (see G7) |
| `datasetVersion` | `major.minor.build.rev`; the app uses minor and build; higher wins for the same ID (07 section 4) |
| `wahapediaCacheDir` | persistent on-disk cache of Wahapedia pages (D13); default under the user's local app data, outside the repo |
| `overridesPath` | optional curated overrides (own facts only: ally percentages, id remaps, manual fixes) |

The scope exclusions (Boarding Actions, Crusade, Unbound Adversaries, Crucible) are fixed behaviour, not settings.

## Phase 3b - Rule-compatibility report

For users who pick the hard-wired ID. The app's ~60 faction-specific rules (`WarOrgan.ArmyValidation.*`) reference unit
IDs/names, detachment names and keywords as string literals. Build a one-off extractor (a script over the decompiled app,
run by the maintainer of this tool, not shipped with the app's code) that produces a **curated list of names the rules
depend on**, stored as our own plain data file (names only, no app code). The converter then reports, per rule, whether
each referenced name exists in the generated dataset and whether the data shape matches (for example a rule limiting a
squad needs matching model names and wargear IDs). Mismatches are warnings, not failures.

## Phase 4 - Proof of concept: one faction end to end

Pick a single self-contained catalogue (no library, imports only an out-of-scope catalogue, so nothing to resolve) with rich
detachments, enhancements with the 3-copy pattern, leaders and Support units. Pick a second, simpler one as the low-risk
alternative. Goal:

1. `DataSetDefinition.json` with the current-edition battle sizes, category keywords, weapon/core ability regexes.
2. A roster that passes `BuildDataset` validation with zero errors.
3. Hand-check five units against BSData and the rules: a squad with points tiers and special weapons, a leader with
   enhancements, a vehicle, an Epic Hero, a Support leader.
4. Import the ZIP into WarOrgan and build a 1000-point list.

Exit criterion: the generated list builds in the app with correct points for the five spot-checks.

## Phase 5 - Scale out

Order: the remaining single-catalogue factions -> factions that share a library catalogue -> the large parent faction and its
sub-factions -> factions that include other factions' units or depend on the primary catalogue (G1) -> the rest.

For each faction record a conversion report; track unconverted modifier counts per faction and drive them
to zero or document them.

## Phase 6 - Detachments, enhancements, army rules, stratagems, allies

Done per faction in phase 4/5 where possible; the stratagem merge is a separate pass once the source is chosen.
Ally rules (percentage restrictions, allowed detachments) come from curated overrides because BSData does not
carry those numbers.

## Phase 7 - Quality and release

- Tests use **small synthetic BSData fixtures written by us** (decision D5: no BSData or generated dataset content
  in the repo). Real-data checks are a local-only script that runs the converter on a user-supplied checkout and
  compares against expectations kept outside the repo.
- Cross-check points for a sample of units/model counts against a second source (printed or official app).
- Conversion reports checked in under `reports/` per BSData commit.
- Dataset README with attribution (BSData contributors, stratagem source), the G1/G3 deviations, and the
  BSData commit used.
- Version policy: `defaultVersion.Build` for data regenerations, `Minor` for format changes; keep the dataset ID
  stable. Decide O7/O8 before the first import.

## Phase 8 - Maintenance

Re-run the converter when BSData changes: `git -C ../bsdata pull`, regenerate, diff the output, review the
report. The diff is the changelog.

## Risks

| Risk | Mitigation |
| --- | --- |
| Modifier logic too varied to evaluate generically | Scenario evaluation + report of unconverted items; per-faction overrides file; accept manual overrides only in `Overrides/` |
| WarOrgan model gaps (G1-G4) change app behaviour | Document deviations; ask maintainer; avoid forking the model |
| 11e BSData still changing (revision 18, active commits) | Pin a commit per dataset version; regenerate often |
| Stratagem source licence blocks publication | Keep stratagems in a separate overlay that can be omitted; check terms before choosing |
| Unit IDs must be stable for saved lists | Derive IDs from names with a deterministic slug and a stored rename map; never use BSData ids |
| Licensing of generated dataset | Open decision O8; resolve before publishing |

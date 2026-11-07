# bsdata-to-warorgan

A command-line tool that converts [BattleScribe](https://battlescribe.net/) data, as maintained by the community in
[BSData](https://github.com/BSData) repositories, into datasets for the army-building app
[WarOrgan](https://github.com/zenchovey/WarOrgan.Data).

**This repo contains tooling and documentation only. It contains no game data and no generated datasets.** You point the
tool at your own local checkout of a BSData repository; the dataset it produces is written to your own machine and is
not meant to be committed or published.

> Unofficial and unaffiliated. This project is not endorsed by or connected to WarOrgan, BSData, or any games publisher.
> Game names and rules belong to their owners. You are responsible for how you use the data you convert and the output
> you generate.

## Status

Early development; there is no release yet. What exists:

- A .NET 10 converter skeleton in [`tools/Converter`](tools/Converter): BSData loader, scope filter, roster resolution,
  extraction of statlines, weapons, keywords, abilities, model compositions, points tiers and wargear, and dataset
  output validated with the WarOrgan builder (`survey`, `extract` and `convert` commands). A dataset for the Orks and
  Necrons catalogues converts, validates with 0 errors and 0 warnings, and is written as a zip; it has not been
  imported into the app yet.
- Written analysis of the BSData structure, the WarOrgan model and the mapping between them, plus the gaps and the plan
  (see below).

Not yet: leaders, detachments and enhancements, allies, stratagems, the other factions, and the settings file and cache commands.

## Design

- **Generic tool, one first target.** The converter is written against the BSData structure and the WarOrgan model,
  not against one game. The first target is the community Warhammer 40,000 11th Edition catalogues
  ([BSData/wh40k-11e](https://github.com/BSData/wh40k-11e)); that work drives the analysis documents and the first
  mappings, and game-specific rules stay in clearly separated mapping and override files so other BSData
  repositories can be supported later.
- **Local data only.** Output goes to a folder you choose and is excluded by `.gitignore`.
- **Optional stratagem source.** BSData has no stratagem entries for the first target, so the plan includes an optional
  step that downloads them from a third-party website into a local cache (see [docs/06-stratagems](docs/06-stratagems.md)).
  That site states no licence, so the step is optional, personal-use only, and nothing it fetches is stored in this repo.
- **Uses the WarOrgan model.** The tool references the `WarOrgan.Data` model project (MIT licence) so that it writes exactly
  what the app expects and can run the same validation.

## Documentation

The analysis was done against the Warhammer 40,000 11th Edition catalogues, so the documents refer to that game.

| Doc | What it holds |
| --- | --- |
| [00-scope-and-decisions](docs/00-scope-and-decisions.md) | Scope, decisions taken, decisions still open |
| [01-source-data](docs/01-source-data.md) | How the BSData files are structured, with ids, counts and examples |
| [02-target-model](docs/02-target-model.md) | The WarOrgan.Data model, its builder and validation rules (as read from source) |
| [03-mapping](docs/03-mapping.md) | BSData concept -> WarOrgan field table |
| [04-gaps](docs/04-gaps.md) | What the WarOrgan model cannot (or may not) represent, evidence and workarounds |
| [05-plan](docs/05-plan.md) | Phased plan and converter design |
| [06-stratagems](docs/06-stratagems.md) | Optional stratagem source and local cache design |
| [07-app-behaviour](docs/07-app-behaviour.md) | What the installed WarOrgan app does (read from its code): points algorithm, limits, hard-wired dataset IDs |
| [08-phase1-findings](docs/08-phase1-findings.md) | Phase 1 results: roster assembly, dispositions, army rules, keywords, modifier classes, leaders |
| [tools/analysis](tools/analysis/README.md) | The throwaway Python scripts that produced the findings |

## Building and trying it

Requirements: .NET 10 SDK, a checkout of the BSData repository you want to convert, and a checkout of
[WarOrgan.Data](https://github.com/zenchovey/WarOrgan.Data) next to this repo (or pass `-p:WarOrganDataPath=<path>`).

```
cd tools/Converter
dotnet build
dotnet run -- survey  --bsdata <path to BSData checkout>
dotnet run -- extract --bsdata <path to BSData checkout> --faction "<catalogue name>"
dotnet run -- convert --bsdata <path to BSData checkout> --output <folder> --dataset-id "<id>" --faction "<name>"
```

`convert` writes `<id>.zip` (plus a README) into the output folder and prints the builder's validation result and a list
of warnings. `--dataset-id` has no default (3-30 characters: letters, digits, space, `- _ ( ) [ ]`); `--faction` can be
repeated and takes a catalogue name, its short name (`Orks`) or its file name, and converts everything when omitted.

## Tests

```
dotnet test tests/Converter.Tests                              # unit tests on small synthetic fixtures, no game data needed
WH40K_SRC=<path to BSData checkout> dotnet test tests/Converter.Tests   # also runs the integration tests on real data
```

Every phase of the converter ships with tests (see [docs/05-plan](docs/05-plan.md), "Testing policy"). The unit tests use
invented fixtures only, so no game content lives in this repo. The integration tests are skipped unless `WH40K_SRC`
is set; they check structural invariants on the real catalogues rather than exact counts.

## Provenance of the analysis

| Input | Version examined |
| --- | --- |
| BSData/wh40k-11e | commit `11ad64c81c058e5deeccd1cb23e64a972029c7b0` (2026-11-06), game system revision 18 |
| Installed WarOrgan app | exe file version 1.3.0, embedded model identical to the repo model, `DataSetFormatVersion` 1 |
| WarOrgan.Data | commit `45f934b8efcf4c988cd2cfd124458d78f09920c7`, `DataSetFormatVersion` = 1 |

App behaviour comes from reading the installed WarOrgan app with a decompiler (the decompiled code is not kept in this
repo); see [docs/07-app-behaviour](docs/07-app-behaviour.md).

## Licensing

- **This repo's own code and documentation:** no licence has been chosen yet (to be decided before the first release).
- **BSData:** the repositories examined carry no LICENSE file. This repo redistributes none of their content.
- **WarOrgan.Data (model library):** [MIT](https://github.com/zenchovey/WarOrgan.Data/blob/main/LICENSE), copyright (c) 2026
  zenchovey. That permits redistributing a build that includes the model library as long as the copyright and permission
  notice travel with it, so release packages of this tool will include a third-party notice. See
  [docs/05-plan](docs/05-plan.md), "Phase 3 - Releases".
- **Generated datasets** contain converted game content. Check the terms of the data source and of the rights holders
  before sharing them; this project does not publish any.

## Resuming work

1. Read [docs/05-plan](docs/05-plan.md) for the phases and the next piece of work.
2. Clone the BSData repository you want to convert and `WarOrgan.Data` as siblings of this repo; check out the commits
   under *Provenance* to reproduce the numbers in the analysis, or re-run `tools/analysis` on newer data.
3. Install the .NET 10 SDK.

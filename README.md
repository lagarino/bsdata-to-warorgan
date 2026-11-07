# bsdata-to-warorgan

A command-line tool that converts [BattleScribe](https://battlescribe.net/) data, as maintained by the community in
[BSData](https://github.com/BSData) repositories, into datasets for the army-building app
[WarOrgan](https://github.com/zenchovey/WarOrgan.Data).

**This repo contains tooling and documentation only. It contains no game data and no generated datasets.** You run the
tool on your own computer, against your own copy of a BSData repository, and the dataset it creates stays on your
computer.

> **Status: early development; release 0.1.0 is published on the Releases page (the repo is private, so collaborators only).** The tool converts BSData catalogues for one tabletop wargame (the first
> target of the project): units, statlines, weapons, wargear, points, army rules, leaders, detachments, enhancements and
> detachment effects. Allies are converted when both catalogues are in the same run; stratagems are not converted yet. Generated datasets
> have been imported into WarOrgan and used to build army lists. See [What works today](#3-what-works-today).

---

## 1. How to run it

### What you need

1. **A copy of the BSData repository you want to convert.** Clone it with git, or download it as a zip from GitHub and
   unpack it:

   ```
   git clone <url of the BSData repository you want to convert>
   ```

   This is the folder you give to `--bsdata`.
2. **The tool.** Either a prebuilt package from the repository's Releases page (Windows, Linux or macOS: unpack it and run
   `warorgan-converter`; no .NET installation needed), or build it yourself (section 2) and run it from the source as shown
   here, which needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). `warorgan-converter --version` shows which
   build you have.
3. **The WarOrgan app**, to import the result.

### Run it

From the root of this repository (replace the paths and names with your own):

```
dotnet run --project tools/Converter -- convert --bsdata "C:\path\to\bsdata-repo" --output "C:\path\to\output" --dataset-id "My Dataset" --faction "<catalogue name>"
```

If you have built the standalone program (section 2) the same command is:

```
warorgan-converter convert --bsdata "C:\path\to\bsdata-repo" --output "C:\path\to\output" --dataset-id "My Dataset" --faction "<catalogue name>"
```

| Option | Required | Meaning |
| --- | --- | --- |
| `--bsdata <folder>` | yes | Your local copy of the BSData repository (the folder with the `.json` catalogue files). Can also be set with the `BSDATA_SRC` environment variable. |
| `--output <folder>` | yes | Where to write the dataset. Created if missing. Keep it **outside** this repository. |
| `--dataset-id <id>` | yes | The name of the dataset inside WarOrgan, 3-30 characters: letters, digits, space and `- _ ( ) [ ]`. No default, you choose. Keep the same id when you regenerate the dataset, so existing army lists keep working. |
| `--dataset-version <a.b.c.d>` | no | Version number, default `1.0.0.0`. Increase it whenever you generate an updated dataset for yourself. |
| `--faction <name>` | no | Which catalogue to convert: its name, its short name (the part after the category prefix) or its file name. Repeat the option for several. Without it, every army catalogue is converted into one dataset (library catalogues are used as sources, not converted; the two titan catalogues, a separate game's armies, are skipped unless you name them). |

Other commands, for looking at the data without writing a dataset:

```
warorgan-converter survey  --bsdata <folder> [--faction <name>]...            # unit count per catalogue
warorgan-converter extract --bsdata <folder> --faction <name> [--brief]       # what would be converted, with warnings
```

### What you get

- `<output>\<dataset-id>.zip`: the dataset, ready to import into WarOrgan.
- `<output>\README.md`: a note about where the dataset came from (also stored inside the zip).
- On screen: the number of units converted, **warnings** about anything that could not be converted exactly, and the result
  of the WarOrgan builder's validation. The tool exits with code `0` only when validation passed with no errors.

### Import into WarOrgan

Import the zip with WarOrgan's dataset import (the app stores imported datasets itself and they can be deleted again).
Choose a `--dataset-id` that differs from any dataset you already have, unless you mean to replace it. The app enables
its built-in army validation rules only for two specific dataset ids; see
[docs/07-app-behaviour](docs/07-app-behaviour.md). With any other id, lists are not checked against those rules.

When you regenerate a dataset, raise `--dataset-version`, and re-import it: the app picks the highest version for an id.

### Keep the result private

The dataset contains converted game content. It is meant for your own use: **do not commit it or publish it.** The folder
names `Output/`, `dataset/`, `datasets/` and zip files are ignored by git in this repo, but an output folder outside the
repository is safest.

---

## 2. How to build it

Requirements:

- the [.NET 10 SDK](https://dotnet.microsoft.com/download);
- a checkout of [WarOrgan.Data](https://github.com/zenchovey/WarOrgan.Data) (MIT licence), placed **next to this
  repository** (so that `..\WarOrgan.Data` exists), or anywhere else if you pass `-p:WarOrganDataPath=<path>`. The tool
  uses its model and its builder, so it writes exactly what the app expects and runs the same validation.

Build and run the tests:

```
git clone https://github.com/zenchovey/WarOrgan.Data          # once, next to this repo
dotnet build tools/Converter
dotnet test tests/Converter.Tests                              # unit tests on synthetic fixtures, no game data needed
```

The integration tests run too when `BSDATA_SRC` points at a BSData checkout (they are skipped otherwise):

```
set BSDATA_SRC=C:\path\to\bsdata-repo                         # PowerShell: $env:BSDATA_SRC = "C:\path\to\bsdata-repo"
dotnet test tests/Converter.Tests
```

Make a standalone program (a single file that needs no .NET installation):

```
dotnet publish tools/Converter -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
publish\warorgan-converter.exe help
```

(Use `-r linux-x64` or `-r osx-arm64` for other systems.) The publish folder also holds the licence texts and this readme; the
sample dataset that the builder project ships is removed from it.

Releases are built by the GitHub Actions workflow `.github/workflows/release.yml` when a tag such as `v0.1.0` is pushed: it runs the
unit tests, publishes the three packages and attaches them to a GitHub release. `ci.yml` builds and tests every push. Both build
against a fixed commit of WarOrgan.Data (the `WARORGAN_DATA_COMMIT` value at the top of the workflows).

Every phase of the converter ships with tests; see [docs/05-plan](docs/05-plan.md), "Testing policy".

---

## 3. What works today

| Area | State |
| --- | --- |
| Load catalogues, apply the scope rules (no legacy, campaign-mode or alternative-mode content) | done |
| Roster per catalogue (own units; units imported from other catalogues are left out) | done |
| Statlines, weapons (firing modes, keywords), keywords, faction keywords | done |
| Core abilities, unit abilities, army rules | done |
| Model compositions and points (model-count tiers, per-copy price changes); loadout variants merged under the group's size cap | done |
| Wargear: starting equipment, replacement choices, additions, costs, granted abilities | done (baseline view, see below) |
| Leaders, support units and retainers (who may join which unit), including leaders that need a specific enhancement | done |
| Detachments (cost, force dispositions, rule text), also when they live in a shared library catalogue | done |
| Enhancements (cost, text, who may take them), including upgrade enhancements limited to one unit | done |
| Detachment effects on datasheets (weapon and unit stat changes, added keywords and abilities and categories, unlocked options) | done; list-building limit changes and a few effects under extra conditions are reported, not converted |
| Dataset definition, ability lists, validation with the WarOrgan builder, zip | done |
| Allies (units imported from another faction's catalogue become an ally entry; include both catalogues in one conversion) | done; allied points caps and ally-only prices are not converted |
| Stratagems (optional download from a third-party website, local cache) | planned |
| Settings file and cache commands | planned |

Every army catalogue of the first target (thirty-four) converts, together or one by one, and passes the builder's validation and the stricter integration checks. Three of
them (Orks, Necrons, Leagues of Votann) have been checked by hand in the app; the others have not been checked unit by unit.

Known limits: options that are only shown under certain conditions (a selected detachment, another option) are not
evaluated beyond the cases above, so the result is the baseline view; "choose N of these" groups become optional
additions with a maximum. Warnings printed by `convert` list what was affected.

---

## 4. Design

- **Generic tool, one first target.** The converter is written against the BSData structure and the WarOrgan model, not
  against one game. The first target is one community catalogue set; it drives the analysis documents and the first
  mappings, and game-specific rules stay in clearly separated mapping and override files so other BSData repositories
  can be supported later.
- **Local data only.** Output goes to a folder you choose. Nothing generated and no game data is stored in this repo.
- **Optional stratagem source.** The first target's BSData has no stratagem entries, so the plan includes an optional
  step that downloads them from a third-party website into a local cache (see [docs/06-stratagems](docs/06-stratagems.md)).
  That site states no licence, so the step is optional, for personal use only, and nothing it fetches is stored in this repo.

---

## 5. Documentation and analysis

The analysis was done against one community catalogue set; the documents describe it in general terms.

| Doc | What it holds |
| --- | --- |
| [00-scope-and-decisions](docs/00-scope-and-decisions.md) | Goal, scope, decisions taken, decisions still open |
| [01-source-data](docs/01-source-data.md) | How the BSData files are structured, with ids, counts and examples |
| [02-target-model](docs/02-target-model.md) | The WarOrgan.Data model, its builder and validation rules (as read from source) |
| [03-mapping](docs/03-mapping.md) | BSData concept -> WarOrgan field table |
| [04-gaps](docs/04-gaps.md) | What the WarOrgan model cannot (or may not) represent, evidence and workarounds |
| [05-plan](docs/05-plan.md) | Phased plan, converter design, testing policy, release plan |
| [06-stratagems](docs/06-stratagems.md) | Optional stratagem source and local cache design |
| [07-app-behaviour](docs/07-app-behaviour.md) | What the installed WarOrgan app does (read from its code): points algorithm, limits, built-in dataset ids |
| [08-phase1-findings](docs/08-phase1-findings.md) | Phase 1 results: roster assembly, dispositions, army rules, keywords, modifier classes, leaders |
| [tools/analysis](tools/analysis/README.md) | The throwaway Python scripts that produced the findings |

### Provenance of the analysis

| Input | Version examined |
| --- | --- |
| BSData catalogue set | commit `11ad64c81c058e5deeccd1cb23e64a972029c7b0` (2026-11-06), game system revision 18 |
| Installed WarOrgan app | exe file version 1.3.0, embedded model identical to the repo model, `DataSetFormatVersion` 1 |
| WarOrgan.Data | commit `45f934b8efcf4c988cd2cfd124458d78f09920c7`, `DataSetFormatVersion` = 1 |

App behaviour comes from reading the installed WarOrgan app with a decompiler (the decompiled code is not kept in this
repo); see [docs/07-app-behaviour](docs/07-app-behaviour.md).

---

## 6. Licensing and disclaimer

- **Unofficial.** This project is not endorsed by or connected to WarOrgan, BSData, or any games publisher. Game names and
  rules belong to their owners. You are responsible for how you use the data you convert and the output you generate.
- **This repo's own code and documentation:** [MIT](LICENSE), the same licence as WarOrgan.Data. This covers only the code and documentation here, not any game data or generated dataset.
- **BSData:** the repositories examined carry no LICENSE file. This repo redistributes none of their content.
- **WarOrgan.Data (model library and builder):** [MIT](https://github.com/zenchovey/WarOrgan.Data/blob/main/LICENSE),
  copyright (c) 2026 zenchovey. That permits redistributing a build that includes the model library as long as the
  copyright and permission notice travel with it, so release packages of this tool will include a third-party notice. See
  [docs/05-plan](docs/05-plan.md), "Phase 3 - Releases".
- **Generated datasets** contain converted game content. Check the terms of the data source and of the rights holders
  before sharing them; this project does not publish any.

---

## 7. Resuming work on the project

1. Read [docs/05-plan](docs/05-plan.md) for the phases and the next piece of work.
2. Clone the BSData repository you want to convert and `WarOrgan.Data` as siblings of this repo; check out the commit
   under *Provenance* to reproduce the numbers in the analysis, or re-run `tools/analysis` on newer data.
3. Install the .NET 10 SDK, then `dotnet test tests/Converter.Tests`.

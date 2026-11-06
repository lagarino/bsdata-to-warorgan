# WarOrgan dataset for Warhammer 40,000 (11th Edition)

Work-in-progress effort to build a [WarOrgan](https://github.com/) dataset for plain Warhammer 40,000
11th Edition games, converted from the community BattleScribe data in
[BSData/wh40k-11e](https://github.com/BSData/wh40k-11e).

**Status: analysis and planning complete; no converter written yet.** Nothing in this repo is a
usable dataset yet.

**This repo contains tooling and notes only.** The converter (C#, planned) is meant for anyone to run against their
own checkout of BSData/wh40k-11e. The dataset it generates stays on the machine of whoever runs it and is never
committed here (see decision D5; `.gitignore` excludes output folders and ZIPs).

## Scope

In: matched-play style 40k army building (units, wargear, points, leaders, detachments,
enhancements, army rules, allies, stratagems from a *different* source).
Out: Boarding Actions, Crusade, Unbound Adversaries, Legends, Crucible characters. See [docs/00-scope-and-decisions.md](docs/00-scope-and-decisions.md).

## Read in this order

| Doc | What it holds |
| --- | --- |
| [00-scope-and-decisions](docs/00-scope-and-decisions.md) | Scope, decisions taken, decisions still open |
| [01-source-data](docs/01-source-data.md) | How the BSData 11e files are structured, with ids, counts and examples |
| [02-target-model](docs/02-target-model.md) | The WarOrgan.Data model, its builder and validation rules (as read from source) |
| [03-mapping](docs/03-mapping.md) | BSData concept -> WarOrgan field table |
| [04-gaps](docs/04-gaps.md) | What the WarOrgan model cannot (or may not) represent, evidence and workarounds |
| [05-plan](docs/05-plan.md) | Phased plan and converter design |
| [06-stratagems](docs/06-stratagems.md) | Stratagem source research (not started) |
| [07-app-behaviour](docs/07-app-behaviour.md) | What the installed WarOrgan app really does (read from its code): points algorithm, limits, hard-wired dataset IDs |
| [tools/analysis](tools/analysis/README.md) | The throwaway Python scripts that produced the findings |

## Provenance of the analysis

| Input | Version examined |
| --- | --- |
| BSData/wh40k-11e | commit `11ad64c81c058e5deeccd1cb23e64a972029c7b0` (2026-11-06, "Fixes #3389"), game system revision 18 |
| Installed WarOrgan app | exe file version 1.3.0, embedded model identical to the repo model, `DataSetFormatVersion` 1 |
| WarOrgan.Data | local clone at commit `45f934b8efcf4c988cd2cfd124458d78f09920c7`, `DataSetFormatVersion` = 1 |

App behaviour comes from decompiling the installed WarOrgan app (kept out of this repo); see
[docs/07-app-behaviour](docs/07-app-behaviour.md).

## Licensing note

BSData repositories carry no LICENSE file at the commit examined (the README says "maintained by
community, in no way endorsed by any company/publisher"). This repo redistributes none of that content and no
generated dataset (decision D5); it contains only analysis and tooling. Check upstream BSData terms and the
Games Workshop IP position for your own use of the generated output.

## Resuming

1. Read [docs/05-plan](docs/05-plan.md), phase 1 is the next piece of work.
2. Clone `BSData/wh40k-11e` next to this repo (`../wh40k-11e`) and `WarOrgan.Data` (`../WarOrgan.Data`); check out the
   commits listed under *Provenance* to reproduce the numbers, or re-run `tools/analysis` on newer data and update the docs.
3. Install the .NET 10 SDK, then settle the remaining open decisions (O4, O5, O7).

# WarOrgan dataset for Warhammer 40,000 (11th Edition)

Work-in-progress effort to build a [WarOrgan](https://github.com/) dataset for plain Warhammer 40,000
11th Edition games, converted from the community BattleScribe data in
[BSData/wh40k-11e](https://github.com/BSData/wh40k-11e).

**Status: analysis and planning complete; no converter written yet.** Nothing in this repo is a
usable dataset yet.

## Scope

In: matched-play style 40k army building (units, wargear, points, leaders, detachments,
enhancements, army rules, allies, Legends, stratagems from a *different* source).
Out: Boarding Actions, Crusade, Unbound Adversaries. See [docs/00-scope-and-decisions.md](docs/00-scope-and-decisions.md).

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
| [tools/analysis](tools/analysis/README.md) | The throwaway Python scripts that produced the findings |

## Provenance of the analysis

| Input | Version examined |
| --- | --- |
| BSData/wh40k-11e | commit `11ad64c81c058e5deeccd1cb23e64a972029c7b0` (2026-11-06, "Fixes #3389"), game system revision 18 |
| WarOrgan.Data | local clone at commit `45f934b8efcf4c988cd2cfd124458d78f09920c7`, `DataSetFormatVersion` = 1 |

The WarOrgan **app** source was not available; any statement about app behaviour is inference and is
flagged as such.

## Licensing note

BSData repositories carry no LICENSE file at the commit examined (the README says "maintained by
community, in no way endorsed by any company/publisher"). Check upstream terms and the Games Workshop
IP position before publishing any generated dataset. This repo contains only analysis and tooling.

## Publishing this repo

The `gh` CLI was not installed when this repo was created, so it exists only locally. To publish it:

```powershell
# option A: GitHub CLI
winget install GitHub.cli ; gh auth login
gh repo create warorgan-wh40k-11e --private --source . --remote origin --push

# option B: create an empty repo on github.com, then
git remote add origin https://github.com/<you>/warorgan-wh40k-11e.git
git push -u origin main
```

Start private: the licensing question above is unresolved.

## Resuming

1. Read [docs/05-plan](docs/05-plan.md), phase 1 is the next piece of work.
2. Clone `BSData/wh40k-11e` next to this repo (`../wh40k-11e`) and `WarOrgan.Data` (`../WarOrgan.Data`); check out the
   commits listed under *Provenance* to reproduce the numbers, or re-run `tools/analysis` on newer data and update the docs.
3. Decide the open decisions (O1-O8).

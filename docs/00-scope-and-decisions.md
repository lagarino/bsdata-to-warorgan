# Scope and decisions

## Goal

Produce a WarOrgan dataset ZIP (via `WarOrgan.Data.BuildDataset`) for Warhammer 40,000 11th Edition,
generated reproducibly from BSData/wh40k-11e by a converter, so it can be regenerated when BSData updates.

## In scope

- Plain 40k games (the `Army Roster` force in the BSData game system).
- Factions in the 11e catalogues, including the Library catalogues and Space Marine chapters.
- Units, models, statlines, weapons, wargear options, points (model-count and unit-count tiers),
  leaders/support attachment, keywords, abilities, Legends.
- Detachments (with detachment-point cost, tags, rules, effects), enhancements, army rules.
- Allies (BSData `Allies: ...` categories).
- Stratagems, **from a different source** (see [06-stratagems](06-stratagems.md)).

## Out of scope (decided by the user)

- **Boarding Actions** (force entry `Boarding Actions`, id `1d6e-2579-8e7f-1ed4`).
- **Crusade** (force entry `Crusade Force`, id `cac3-71d1-ea4b-795d`, plus Order of Battle, Experience,
  Battle Honours, Weapon Modifications, Blackstone Fragments, Crusade Points).
- **Unbound Adversaries** (Greater/Lesser Daemons, Abomination, Daemonic Beasts entries in the game system).

The converter must therefore **evaluate every modifier with Boarding Actions and Crusade treated as
not selected**, and drop all branches that only apply to them. This removes a large share of the modifier
noise (e.g. all "Boarding Actions modifiers" groups on units, `Crusade modifiers` groups).

## Decisions taken

| # | Decision | Reason |
| --- | --- | --- |
| D1 | Keep this analysis/tooling in its own repo, separate from WarOrgan.Data | The WarOrgan.Data README asks that game datasets live in the owner's own repo |
| D2 | Do not change `WarOrgan.Data.Model` for now; work around gaps | The model is coupled to the app; changes must be agreed with the maintainer |
| D3 | Stratagems come from a non-BSData source | BSData 11e contains no stratagem entries |
| D4 | **Converter language: C#**, referencing `WarOrgan.Data.Model` (was O1) | Typed output validated by the same classes as the app. Needs the .NET 10 SDK, which is **not installed** on the dev machine yet (only runtime 3.1.8) |
| D5 | **This repo is a tool anyone can run; the generated dataset is never committed or published** (was O2 and O8) | Only code goes on GitHub; the dataset stays on the machine of whoever runs the converter. Consequences: the converter takes the BSData checkout and the output folder as arguments; `.gitignore` covers output and ZIPs; **no BSData text or generated data in tests or fixtures** (use small synthetic fixtures); curated override files may hold only our own facts, not rules text |
| D6 | **Exclude `[Crucible]` characters** (was O3) | Not clearly plain 40k; roughly 100 weapon-cost modifiers |

## Decisions still open

| # | Question | Notes / leaning |
| --- | --- | --- |
| O4 | Include `Unaligned Forces` and `Unaligned Fortifications`? | They are BSData "Show ..." toggles. Default: include Unaligned Forces as an ally faction, decide fortifications later. |
| O5 | Legends units | Model supports them (`Unit.IsLegend` set from a `"{Name} Legends".json` roster). Plan: split units whose name ends in `[Legends]` into a separate roster file. Confirm. |
| O6 | ~~App-behaviour questions~~ | **Resolved** by decompiling the installed app (the user confirmed it is the latest version): see [07-app-behaviour](07-app-behaviour.md). A few optional questions for the maintainer remain at the end of [04-gaps](04-gaps.md); nothing has been sent. |
| O7 | **Dataset ID** | **Needs a decision.** The app enables validation and several exports only for the IDs `Warhammer 40k 10th` / `Warhammer 40k 11th` (gap G7). Option A: own ID such as `WH40K-11E` (safe, no validation beyond points). Option B: reuse `Warhammer 40k 11th` (validation on, but ~60 name-keyed faction rules fire against our data and the ID collides with the maintainer's/old lists). Recommendation: A first, experiment with B locally later. |
| O8 | ~~Publishing the generated dataset~~ | Resolved by D5. Still worth checking upstream BSData terms for the converter, but this repo redistributes no BSData or Games Workshop content |
| O9 | ~~.NET 10 SDK~~ | **Done**: SDK 10.0.403 installed with winget; `ilspycmd` 11.2 installed as a global dotnet tool (used to read the app) |

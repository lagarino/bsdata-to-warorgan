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

## Decisions still open

| # | Question | Notes / leaning |
| --- | --- | --- |
| O1 | Converter language: C# or Python? | Leaning **C#** referencing `WarOrgan.Data.Model` so output is typed and validated by the same classes. Python stays for exploration. |
| O2 | Repo layout for the dataset itself: fork WarOrgan.Data, git submodule it, or add its project via path reference? | WarOrgan.Data README recommends forking. A submodule keeps this repo small and lets us pull upstream model changes. |
| O3 | Include `[Crucible]` characters? | Crucible is a custom-character system (many weapon-cost modifiers, ~100 point-modifiers). Not obviously "plain 40k". Default: exclude until confirmed. |
| O4 | Include `Unaligned Forces` and `Unaligned Fortifications`? | They are BSData "Show ..." toggles. Default: include Unaligned Forces as an ally faction, decide fortifications later. |
| O5 | Legends units | Model supports them (`Unit.IsLegend` set from a `"{Name} Legends".json` roster). Plan: split units whose name ends in `[Legends]` into a separate roster file. Confirm. |
| O6 | Ask the WarOrgan maintainer about the app-behaviour questions in [04-gaps](04-gaps.md) | Needs the user to decide whether to open an issue / email warorgan@gmail.com. |
| O7 | Dataset ID and name | e.g. `WH40K-11E`. Must stay stable once published because saved lists reference it. |
| O8 | Publishing / licensing of the generated dataset | See README licensing note. |

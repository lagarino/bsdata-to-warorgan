# Scope and decisions

## Goal

Build a reusable command-line tool that converts BattleScribe data (BSData catalogues) into WarOrgan datasets
(validated and zipped via `WarOrgan.Data.BuildDataset`), reproducibly, so a dataset can be regenerated whenever the
BSData source is updated. The tool is generic in design; game-specific rules live in separate mapping and override
files. The first target, and the subject of the analysis in these documents, is the community BSData
Warhammer 40,000 11th Edition catalogues (`BSData/wh40k-11e`). The repository holds tooling and documentation only,
never game data or generated datasets (D5).

The scope sections below describe that first target.

## In scope

- Plain 40k games (the `Army Roster` force in the BSData game system).
- Factions in the 11e catalogues, including the Library catalogues and Space Marine chapters.
- Units, models, statlines, weapons, wargear options, points (model-count and unit-count tiers),
  leaders/support attachment, keywords, abilities.
- Detachments (with detachment-point cost, tags, rules, effects), enhancements, army rules.
- Allies (BSData `Allies: ...` categories).
- Stratagems, **from a different source** (see [06-stratagems](06-stratagems.md)).

## Out of scope (decided by the user)

- **Boarding Actions** (force entry `Boarding Actions`, id `1d6e-2579-8e7f-1ed4`).
- **Crusade** (force entry `Crusade Force`, id `cac3-71d1-ea4b-795d`, plus Order of Battle, Experience,
  Battle Honours, Weapon Modifications, Blackstone Fragments, Crusade Points).
- **Unbound Adversaries** (Greater/Lesser Daemons, Abomination, Daemonic Beasts entries in the game system).
- **Legends** (every unit named `[Legends]`, the `Show Legends` toggle, and the Legends libraries), decision D8.
- **Killzone Upgrade: Shadowvaults**, **Armageddon** campaign content and `[Crucible]` characters (D6).

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
| D7 | **The dataset ID is a converter setting chosen by whoever runs the tool** (was O7) | The user prefers the app's hard-wired `Warhammer 40k 11th` (validation and all exports on) but does not want to commit irreversibly. So: `datasetId` is a **required** setting with no silent default; the docs explain both options (gap G7); the converter must work with either; and it must tell the user, when they pick the hard-wired ID, which of the app's name-keyed rules our output may trigger (see plan phase 3b). Switching later is cheap for the dataset itself (see [07-app-behaviour](07-app-behaviour.md) section 4) but not for lists already saved under an ID |
| D8 | **No Legends support at all** (was O5) | 92 of 473 units and 389 modifiers are Legends-related. Drop every `[Legends]` unit, never write a `"<Name> Legends.json"` roster, drop leader/option references to dropped units, and do not set `Unit.IsLegend`. The `Library - Astartes Heresy Legends` catalogue is dropped too (to confirm) |
| D9 | **Exclude Unaligned Forces** (was O4) | Confirmed by the user. The catalogue holds only `[Legends]` and Shadowvaults content, so nothing is left in scope. No Unaligned Forces faction, no ally entry; ignore `Allies: Unaligned Forces` and the link that 16 catalogues have to it |
| D10 | **`Show White Dwarf content` needs no handling** (was O10) | Everything behind it is Crusade content (Battle Traits, Crusade Relics, Battle Honours, Crusade agendas); it contains no units. It is removed by the Crusade exclusion |
| D11 | **Dispositions are not exclusive** | The user confirmed the 11e rules do not forbid two detachments with the same Force Disposition. `Detachment.Tags` stays empty; `ForceDispositions` carries the dispositions for display |
| D12 | **Stratagem source: Wahapedia 11th edition** (user decision) | Core stratagems from its core-rules page, detachment stratagems from each faction page (they apply only when that detachment is selected). Nothing from it is committed to this repo (D5). How the converter obtains it is open decision O11 in [06-stratagems](06-stratagems.md) |
| D13 | **Built-in downloader with a persistent local cache for Wahapedia content** (was O11, user decision and clarification) | The converter downloads the needed pages once and stores them **on the file system** so later runs reuse them without network access. It must offer commands to **rebuild** (re-download) and to **delete** the cache. Design constraints in [06-stratagems](06-stratagems.md): cache outside the repo, never committed (D5); fetch only the core-rules page and the faction pages; honour robots.txt, rate-limit, identify itself honestly; **never work around a block**: on a 403 it stops, reports, and falls back to HTML files the user places in the cache folder |

## Decisions still open

| # | Question | Notes / leaning |
| --- | --- | --- |
| O6 | ~~App-behaviour questions~~ | **Resolved** by decompiling the installed app (the user confirmed it is the latest version): see [07-app-behaviour](07-app-behaviour.md). A few optional questions for the maintainer remain at the end of [04-gaps](04-gaps.md); nothing has been sent. |
| O7 | ~~Dataset ID~~ | **Resolved by D7**: it is a converter setting, not a project decision. |
| O8 | ~~Publishing the generated dataset~~ | Resolved by D5. Still worth checking upstream BSData terms for the converter, but this repo redistributes no BSData or Games Workshop content |
| O9 | ~~.NET 10 SDK~~ | **Done**: SDK 10.0.403 installed with winget; `ilspycmd` 11.2 installed as a global dotnet tool (used to read the app) |

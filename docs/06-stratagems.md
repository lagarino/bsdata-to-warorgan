# Stratagems

**Status: not started.** BSData wh40k-11e has no stratagem data (only prose in abilities that mentions
stratagems, e.g. "target this unit with the Grenades stratagem for 0CP").

## Target shape

`DataSetDefinition.CoreStratagems[]` for the core stratagems and `Detachment.Stratagems[]` per detachment,
using `Stratagem{Name, Category, CPCost, Phase, When, Target, Effect, Restrictions, Options[]}`.
Enable `DataSetFeatures.Stratagems`.

## What to look for in a source

1. Covers 11th edition: core stratagems **and** per-detachment stratagems for every detachment in BSData.
2. Machine-readable (CSV/JSON/HTML table) with stable names.
3. Licence / terms that allow us to republish the text in a dataset (this is the deciding factor).
4. Detachment names match BSData names, or we can map them with a curated alias table.

## Candidate sources to evaluate (none evaluated yet; I have not verified any of these)

- A community data export that offers downloadable tables (check whether an 11th-edition set exists and its terms).
- Official Games Workshop app/Warhammer Community material (very likely not licensed for redistribution).
- Other community BattleScribe-like or JSON datasets for 11e (search GitHub for "stratagems 11th edition json").
- Manual transcription from owned rules (copyright risk; keep as private overlay only).

## Tasks

- [ ] Search for candidate sources; record URL, format, edition coverage, licence in a table here.
- [ ] Pick one; record the decision in [00-scope-and-decisions](00-scope-and-decisions.md).
- [ ] Define `data/stratagems/` schema and a validator that checks every detachment has stratagems (or is marked as having none).
- [ ] Merge step in the converter (phase 6).
- [ ] Add attribution to the dataset README.

## Open question

Does the app need stratagems for the dataset to be useful? `Features` is a flag set, so the dataset can ship with
stratagems disabled first and gain them later with a minor/build version bump.

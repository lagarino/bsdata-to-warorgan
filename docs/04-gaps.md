# Gaps: what the WarOrgan model/app cannot represent

All items below were checked against the data **and** the installed app's code (see
[07-app-behaviour](07-app-behaviour.md)). Items G2 and G3, listed as problems in the first version of this document,
turned out to be solved by existing app features and are kept here as a record.

| ID | Gap | Status |
| --- | --- | --- |
| G1 | Points that depend on the army context (allies) | **Open**, workaround exists |
| G2 | Enhancement taken up to 3 times, counting once | **Solved**: `EnhancementFeature.Upgrade` |
| G3 | Detachment-point cap depends on the chosen detachment | **Solved / non-issue** |
| G4 | Per-unit army copy limits | **Mostly solved**; 9 exceptions open |
| G5 | Detachments with several rules | **Open**, cosmetic workaround |
| G6 | Validator and app constraints to design around | Design notes |
| G7 | **App rules only run for two hard-wired dataset IDs** | **New, decision needed (O7)** |
| G8 | Faction-specific rules live in app code | **New**, cannot be expressed in a dataset |
| G9 | Sub-faction-only detachments in the shared the parent faction list | **New (phase 1)**, workaround exists |

---

## G1 - Points that depend on the army context (ally / primary catalogue)  [Open, verified]

**Data:** The Faction N assassins (and 11 similar entries; 12 point-modifiers in total) are
`increment pts +15` when the roster's primary catalogue is *not* Faction N, i.e. when they are taken
as allies in another army (e.g. Unit 3, condition `primary-catalogue ... notInstanceOf`).

**Model/app:** `UnitIncludeRule` can only change keywords (`AddedKeywords`, `RemovedKeywords`, `ReplaceFactionKeywords`);
the app's `FilterAndCloneBySubFactionInfo` has no points hook. `PointsOverride` exists only on `DetachmentEffect`.

**Workaround:** Emit a second copy of each affected unit with a distinct ID (e.g. `VINDICARE ASSASSIN (ALLY)`),
register it in an ally roster (an `... Allies` faction roster is how the app handles allied variants: the Legends loader
even strips a trailing `" Allies"` from the roster name) and give it the higher points. A faction whose ally entry
points at that roster then sees the ally-priced copy while the home faction sees the normal one.

**Possible model change:** `UnitIncludeRule.PointsDelta`. Needs the maintainer.

---

## G2 - Enhancement copy limit and slot sharing  [Solved]

**Data:** ~84 of 791 enhancement entries (`selections max 3`, force or roster scope, with `divide` modifiers on the
`Enhancements` cost) may be taken up to 3 times per army and count once towards the enhancement cap. The other
~700 are limited to 1.

**App:** `UniqueEnhancements` counts *unique enhancement names* against `EnhancementLimit`, allows an enhancement
flagged `EnhancementFeature.Upgrade` on up to 3 units and any other on 1, and allows one enhancement per attached unit.

**Conversion rule:** Emit `Features: Upgrade` for entries with the max-3 + divide pattern; plain entries get no flag.
(3 entries have no cap constraint at all; treat as normal and review.)

---

## G3 - Detachment-point cap depends on the chosen detachment  [Solved]

**Data:** cap 2 by default, 3 at Strike Force, 4 at Onslaught, and at Incursion 3 if a `3DP Detachment` is chosen.
Enhancement cap 2 at Incursion, 4 otherwise.

**App:** the **first** detachment can always be selected regardless of cost; later ones need remaining points and no
shared tag. A 3-point detachment chosen first at Incursion (cap 2) leaves no points, exactly like the conditional cap 3.

**Conversion rule:** Incursion `DetachmentPoints = 2`, Strike Force `3`, Onslaught `4`; `EnhancementLimit` 2 / 4 / 4;
`UnitLimit` 3. No deviation from the rules results.

---

## G4 - Per-unit army copy limits  [Mostly solved]

**Data:** 3 default (354 units), 6 Battleline (62), 1 Epic Hero (12), plus 9 non-Epic units limited to 1 (e.g. Chaplain
Grimaldus, Kill Team Cassius [Legends]); 33 units carry no constraint.

**App:** limit = `UnitLimit` (3), Battleline = 2 x UnitLimit, Dedicated Transport = 6, `Epic Hero` = 1. Keywords must be
spelled exactly `Battleline`, `Dedicated Transport`, `Epic Hero`.

**Remaining gap:** the 9 non-Epic units limited to 1 and the 33 unconstrained units cannot be expressed. Workaround:
none in the dataset; mention in the dataset README or ask whether a `Unit` flag would be welcome.

---

## G5 - Detachments with several rules  [Open, cosmetic]

**Data:** 263 detachments: 1 rule (216), 2 (17), 3 (5), 4 (7), 5 (2), 0 (16).
**Model:** `Detachment.Rule` is a single `DetachmentRule`.
**Workaround:** Concatenate the rules into one `DetachmentRule.Text` with Markdown sub-headings, or push extras into
`Effects[].AddedInfoSections` where unit-specific.

---

## G6 - Constraints to design around (not gaps)

- **One `ModelWargear` set per model type when options exist** (builder error otherwise). Different base loadouts in one
  model type need separate `ModelComposition`s.
- **Every weapon profile must be reachable** from initial wargear, an option or a definition (build error otherwise).
- **A regex per keyword.** Every weapon keyword and core ability needs a matching regex in the dataset definition.
- **Non-empty characteristic values.** Placeholders (e.g. `-`) are needed for missing values.
- **Faction keyword reachability.** Each unit needs a faction keyword known to its faction (or a detachment's
  `AdditionalFactionKeywords`).
- **Max one `IsIncludedInPrimaryRoster` ally per faction** (`SingleOrDefault`).
- **Points tiers** are ceilings on total model count and on copy index; costs must not decrease as counts grow
  (the app sorts by cost). See [07-app-behaviour](07-app-behaviour.md) for the exact algorithm. Costs that depend on the
  *mix* of model types cannot be expressed; not yet seen in the data.
- **`Detachment.Tags` are mutual-exclusion categories** in the app (two detachments sharing a tag cannot both be selected). BSData detachment categories are dispositions, not exclusion groups, so `Tags` stays empty (see [08-phase1-findings](08-phase1-findings.md) section 2); the user confirmed that the 11e rules do not forbid two detachments with the same disposition (D11).
  To verify in the BSData rule text for `Take and Hold`, `Purge the Foe` and `3DP Detachment`.

---

## G7 - App rules only run for two hard-wired dataset IDs  [New, decision needed]

`ArmyValidationService` only builds its rule list when the dataset ID is exactly one of the app's two built-in dataset ids. For any other ID the app validates **only the points cap**: no duplicate-datasheet limit, no Epic
Hero limit, no unique-enhancement rule, no warlord / leader / support rules, no allies points cap. The TTS, YellowScribe
and GW / WTC text exports and "import from text" are also limited to those two IDs.

Options for the dataset ID (open decision O7):

| Option | Gain | Cost |
| --- | --- | --- |
| A. Own ID (e.g. `MY-DATASET`) | No clash with the maintainer's datasets or old lists | No validation beyond points, fewer exports |
| B. Reuse a built-in id | Validation and all exports work | The ~60 faction-specific rules fire against our data (keyed on unit and detachment *names*); lists saved under that ID from the old dataset would point at our unit IDs and break; may collide with any official dataset the app downloads |

**Decision (D7):** the choice is left to whoever runs the converter (`datasetId` setting, required, no silent default).
The project's preferred target is B. To make that safe the converter must produce a **rule-compatibility report** (plan
phase 3b): which of the app's name-keyed rules reference unit or detachment names that exist in the generated
dataset, so a user choosing B knows what will fire.

## G8 - Faction-specific rules cannot be expressed in a dataset  [New]

Rules such as Tactical Marines squad limits, Calgar, Cadian Shock Troops weapon limits, Broadside/Crisis/Commander
Battlesuits, Assigned Agents, Killteam rules and 50+ others are C# classes in the app (`WarOrgan.ArmyValidation.*`),
not data. A generated dataset can never carry them. Under option A they are simply absent; under option B they apply
only if unit IDs and names match what the code expects. Consequence: the dataset conveys unit **options** (via
`WargearOption.Max`/`PerXModels`) but the app does not enforce the squad-composition limits the rule classes check.

## Questions for the maintainer that remain

1. Would a points hook on `UnitIncludeRule` (G1) or a per-unit copy-limit field (G4 remainder) be acceptable?
2. Is there an intended way for third-party datasets to get validation (G7), for example a flag on `DataSetInfo`
   or `DataSetDefinition` that enables the general rules?
3. The exe reports version 1.3.0 although the repo README says 3.0+; is that expected?

Nothing has been sent to the maintainer.

---

## G9 - Sub-faction-only detachments in the shared the parent faction list  [New, phase 1]

**Data:** all 35 parent-faction detachments are defined in the parent faction catalogue, including sub-faction-specific ones (Detachment 7, Detachment 8, Detachment 9, ...), each hidden unless the roster's primary catalogue is that sub-faction. Sub-faction catalogues add none.

**App:** `GetDetachments(faction)` = the faction roster's own detachments plus those of the rosters named in `AdditionalFactionKeywords`; there is no per-detachment visibility by faction.

**Workaround:** put generic detachments in the parent faction roster, and each sub-faction-specific detachment only in that sub-faction's own roster; sub-factions list the parent faction roster in `AdditionalFactionKeywords`. Needs a check that `AdditionalFactionKeywords` can name a roster that is not an ally.

## Wider catalogues  [Status: partly closed]

Eleven catalogues of the first target convert and pass the builder's validation, and the strict integration checks now hold for all
eleven (two of them were also checked by hand in the app; the other nine have not been checked unit by unit). Fixed while getting
there: per-model prices (the price sits on the model entries), conditions on campaign-mode entries (never active in a standard
game), a count of the only model type of a unit, groups of loadout variants that substitute for a model next to them, an outer
size cap with a nested minimum, detachments and enhancements in a library the roster's own units come from (and not in an allied
library), hiding conditions written as "count equals zero", and enhancements restricted by a "unit only" sentence.

What is still open:

- **A price that depends on the count of one optional model type** (one unit of one catalogue: two overlapping size ranges
  depending on whether an optional extra model is present). The evaluator reports it and keeps the base price.
- **A shared size cap across different model types.** Where a group caps the total of several differently named models (single
  specials plus a main troop), WarOrgan has no cross-model cap. The unit's size is right (it comes from the points tiers), but
  the model limits can add up to more than the cap.
- **Effects of a detachment under extra conditions** (a few units) and list-building limit changes, as listed for the first catalogues.
- **Allies** [mostly closed]. Units imported from another catalogue are split by faction keyword: units with the catalogue's own
  keyword (a library that belongs to the faction) join its roster; units of another faction become a `FactionInfo.Allies` entry,
  with a `UnitIncludeRule` (`AffectedUnitsFilter`, matched by unit id as the app does) when the faction may take only part of the
  ally's roster. Units a catalogue links itself that belong to another converted faction are treated the same way. The ally's
  catalogue must be part of the same conversion; otherwise the units are left out and `convert` says so. Open: allied points caps
  (`ArmyPercentageRestriction`, not in the source data), the higher points of some units when allied (G1), and a library with no
  matching faction catalogue (for example a titans library).
- **Leader associations** are labelled in several ways ("Leader", "Led by", none, and "Supported by" / "Supported By" / none for support
  units); all are read, which gave the four chaos legion catalogues their leaders (they were missing, not absent from the data).
- **Abilities granted by an attached leader** [mostly closed]. A core ability that a unit has only while a given leader is attached is
  written on the leader as a `LeaderAbilityRef` (adds the ability to the bodyguard unit). The unit is picked by no name when the leader
  can lead only units that get it, by a keyword that exactly those units share, or by a keyword of the unit's own name. Where none of
  those works the ability is left out and `convert` warns (for example one leader whose granted units are a part of the units he leads).
- **Cannot be represented in the WarOrgan model** (so they stay as documented, not as tasks): a price that depends on the count of one
  model group or optional model type (Windriders, Wolf Guard Headtakers), one size cap shared by differently-statlined model types,
  list-building limit changes of a detachment, and ally-only prices (G1). The Titans libraries have no faction of their own.
- **Text markup.** BSData texts use "^^" (keywords) and "**" (emphasis), which the app shows literally. `TextCleaner` removes them from
  every text, description, value and keyword list before the dataset is validated, and turns non-breaking spaces into spaces.
- **Unit-wide options** (an instrument or icon bearer that the unit as a whole may take) are offered through the kind of model that has
  most models in the unit; a datasheet with several kinds of model lost them before.
- **Abilities that a detachment gives** are left out of the unit when the source hides them until the detachment is selected (also when
  the condition is on the whole roster), and the detachment adds them. **Detachment effects** that change several units in the same way
  are one effect that names all of them.
- **Datasheets without a faction category** (some characters) get the keyword of the roster they are listed in.
- **Sub-factions** [closed for the army families of the first target]. A catalogue whose own datasheets carry a parent faction's keyword
  next to its own (a chapter of a parent army), or that is mostly made of the parent's imported units, lists the parent as an ally
  with `IsIncludedInPrimaryRoster` (one per faction) and an include rule naming the units it may take. Units the primary-catalogue
  rules hide for that catalogue (a chapter with its own version of a datasheet) are left out, and when a chapter and its parent list a
  datasheet of the same name, the chapter's own copy is used. The parent's detachments are added to the chapter's.
- **Prices that depend on the count of a model group** (for example "3-6 headtakers" plus an optional wolf pack): reported, base price kept.
- **Other families.** The large parent faction with its sub-factions, the faction families that include other factions, and the
  library-only catalogues are not converted.

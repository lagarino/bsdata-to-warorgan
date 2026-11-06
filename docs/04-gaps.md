# Gaps: what the WarOrgan model cannot (or may not) represent

Confidence: **High** = verified against both the data and the model. **Medium** = depends on app behaviour
that is not visible in `WarOrgan.Data` (inferred). Questions for the app maintainer are collected at the end.

---

## G1 - Points that depend on the army context (ally / primary catalogue)  [High]

**Data:** The Agents of the Imperium assassins (and 11 similar entries; 12 point-modifiers in total) are
`increment pts +15` when the roster's primary catalogue is *not* Agents of the Imperium, i.e. when they are taken
as allies in another army (e.g. Vindicare Assassin, condition `primary-catalogue ... notInstanceOf`).

**Model:** `UnitIncludeRule` can only change keywords (`AddedKeywords`, `RemovedKeywords`, `ReplaceFactionKeywords`).
`PointsOverride` exists only on `DetachmentEffect`. No points change when units are cloned into another
faction's roster.

**Workaround:** Emit a second copy of each affected unit with a distinct ID (e.g. `VINDICARE ASSASSIN (ALLY)`)
and a distinct keyword, include it with `AllKeywordFilter` in the ally rule and exclude it from the
home roster. Costly in dataset size and fragile.

**Possible model change:** `UnitIncludeRule.PointsDelta` (or `PointsOverride[]`).

---

## G2 - Enhancement copy limit and slot sharing  [Medium]

**Data:** The same enhancement can be taken up to 3 times per army (`selections max 3`, force scope), with
`divide` modifiers on the `Enhancements` cost so N copies together count as **one** enhancement towards the
cap (2 copies count 1/2 each, 3 copies count 1/3 each). Points (`pts`) are paid per copy.
Seen in Orks (e.g. `Wimp-kickaz` 15 pts, `'Ardboyz`); the pattern likely repeats in other catalogues
(`Deepening Madness` appears as a stale label in 7 catalogues).

**Model:** `Enhancement` has no `MaxCopies`. `EnhancementFeature.Upgrade` is documented as "'Upgrade'
enhancements that can be equipped multiple times", which may or may not be the same rule. *(inferred)*

**Next step:** Ask whether `Upgrade` implements "up to N, counting once". If not, request `Enhancement.MaxCopies`
and a counting rule.

---

## G3 - Detachment-point cap depends on the chosen detachments  [High, small]

**Data:** Cap is 2 by default, 3 at Strike Force, 4 at Onslaught, and at **Incursion it rises from 2 to 3 if a
`3DP Detachment` is selected**. Enhancement cap: 2 at Incursion, 4 at Strike Force and Onslaught.

**Model:** `BattleSizeDefinition.DetachmentPoints` is one integer per size; `PointsTotal` must be unique,
so a second "Incursion + 3DP" definition is impossible.

**Workaround:** Incursion = 2 (3-point detachments then cannot be taken at Incursion, a rule deviation) or
Incursion = 3 (allows three 1-point detachments, a different deviation). Pick the less harmful and document it
in the dataset README. Preferred: **3**, if the app lets users also see the cost, since the likely player complaint
is not being able to take a 3DP detachment at all - to be decided with the user.

**Possible model change:** `Detachment.RaisesDetachmentPointCapTo` or an "allowance per detachment" field.

---

## G4 - Per-unit army copy limits  [Medium]

**Data:** `selections max N` at force scope on every unit: 3 by default (354 units), 6 for Battleline (62),
1 for Epic Heroes (12), plus 9 non-Epic units limited to 1 (e.g. Chaplain Grimaldus, Kill Team Cassius).
27 + 4 + 2 units carry no constraint.

**Model:** No per-unit max exists. The app probably derives it from keywords (Battleline / Epic Hero /
Dedicated Transport via `CategoryKeywords`) *(inferred)*. The 9 exceptions and the 33 unconstrained units cannot
be expressed.

**Next step:** Ask the maintainer how copy limits are computed; if keyword-based, check `Epic Hero`/`Battleline`
are what the app expects and consider `Unit.MaxPerArmy`.

---

## G5 - Detachments with several rules  [High, cosmetic]

**Data:** 263 detachments: 1 rule (216), 2 (17), 3 (5), 4 (7), 5 (2), 0 (16).
**Model:** `Detachment.Rule` is a single `DetachmentRule`.
**Workaround:** Concatenate the rules into one `DetachmentRule.Text` with Markdown sub-headings (titles `**Rule name**`),
or push extras into `Effects[].AddedInfoSections` where they are unit-specific.

---

## G6 - Not gaps, but model/validator constraints to design around

- **One `ModelWargear` set per model type when options exist.** Models in a unit that have different base
  loadouts must be separate `ModelComposition`s, or use `Models` counts with no `Options`.
- **Every weapon profile must be reachable** from initial wargear, an option or a definition (else build error).
- **A regex per keyword.** Every weapon keyword and core ability needs a matching regex in the dataset definition.
- **Non-empty characteristic values.** Placeholder text is needed for missing values.
- **Faction keyword reachability.** A unit with no faction keyword known to its faction fails the build.
- **Max one `IsIncludedInPrimaryRoster` ally per faction** - fine for chapters, awkward if a faction needs
  to include two libraries (e.g. Imperial Knights with a Knights Library and Agents). Library units may need
  copying rather than inclusion.
- **Per-unit `Points`**: tiers are keyed by total model count and optionally by prior unit count; costs that depend on
  the *mix* of model types (rather than the total) cannot be expressed. Not yet seen in the data, to be checked.

## Out of scope on purpose (not gaps)

Boarding Actions, Crusade, Unbound Adversaries (see [00-scope](00-scope-and-decisions.md)).

## Not in the source (not a model gap)

Stratagems - see [06-stratagems](06-stratagems.md).

## Questions for the WarOrgan maintainer (`warorgan@gmail.com` or a repo issue)

1. How are per-unit army copy limits computed (G4)?
2. Does `EnhancementFeature.Upgrade` implement "up to N copies counting once" (G2)?
3. What does `BattleSizeDefinition.UnitLimit` limit exactly (total units? per datasheet?) (BSData has no equivalent;
   we plan to use 0 = unlimited)?
4. Would you accept `UnitIncludeRule` points adjustments (G1) or a detachment-based DP cap (G3)?
5. What does `Detachment.RestrictedUnits` mean (deny-list of units, or the only units allowed)?
6. How is a custom points limit handled in the app (BSData allows any value >= 500 in steps of 250)?
7. Is a `Legends` roster file matched by name exactly `"<Faction Name> Legends.json"`?

Do not send these until the user decides (open decision O6). The locally installed app (v1.3.0) uses the old data
format and cannot answer them; see [02-target-model](02-target-model.md).

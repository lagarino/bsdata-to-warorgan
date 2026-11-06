# Stratagems

**Status: source chosen, nothing built yet.** BSData wh40k-11e has no stratagem entries (only prose in abilities that mentions
them). The user pointed to **Wahapedia (11th edition)** as the source:

- Core stratagems: `https://wahapedia.ru/wh40k11ed/the-rules/core-rules/#Core-Stratagems` (they apply to every army and detachment).
- Detachment stratagems: on each faction page, `https://wahapedia.ru/wh40k11ed/factions/<faction>/`, in each detachment's section;
  they apply only when the army has selected that detachment.

## Target shape

`DataSetDefinition.CoreStratagems[]` and `Detachment.Stratagems[]`, using
`Stratagem{Name, Category, CPCost, Phase, When, Target, Effect, Restrictions, Options[StratagemOption{Title, Text, CPCost}]}`,
with `DataSetFeatures.Stratagems` enabled. In the app, `Stratagem` text is shown in the viewer; core stratagems are optional
in the viewer settings.

## What a first look at Wahapedia shows

Retrieved by a sub-agent through a fetch tool that returns **model-written summaries** for long pages, so everything marked
*(summary)* is unverified and must be re-checked against the real HTML before relying on it.

| Topic | Finding |
| --- | --- |
| Core stratagems | 10 *(summary)*: Command Re-roll, Epic Challenge, Insane Bravery, Explosives, Crushing Impact, Rapid Ingress, Fire Overwatch, Smokescreen, Heroic Intervention, Counteroffensive; CP cost 1 except Counteroffensive (2); one type label "Core Stratagem" (no Battle Tactic / Epic Deed ... labels) |
| Entry layout | Name; "NCP" line; type label; flavour sentence (not needed); bold **WHEN**, **TARGET**, **EFFECT**; **RESTRICTIONS** where present. Timing uses turn-timing icons explained in a "Stratagems Key" image |
| Variants | A "+1CP" line followed by alternative modes (core Heroic Intervention; Bully Boyz *Hulking Brutes*), and EFFECT bullets with "Or:" alternatives (Orks *Mow 'Em Down*, *Close-Range Dakka*) -> `StratagemOption` |
| Detachment stratagems | 3 to 6 per detachment (verified for Orks: War Horde 6, Green Tide 3, Bully Boyz 3, Runt Swarm 3, Shoota Boyz 3, Taktikal Brigade 3; one Runt Swarm stratagem costs 2CP); type label is "<Detachment> Stratagem" |
| Detachment list | Orks: 15 detachments on Wahapedia **all match BSData names exactly**. BSData lists two more (`Ramship Raiders`, `Kaptin Killers`): these are Boarding Actions detachments and are out of scope |
| Same page also has | each detachment's rule and its enhancements with points (a useful cross-check for the BSData conversion, e.g. "Headwoppa's Killchoppa 15 pts") |
| Anchors | detachment sections are anchored by hyphenated name (`#War-Horde`, `#Green-Tide`); sub-anchors carry per-page numeric suffixes (`#Stratagems-12`) and are not stable |
| 11th-edition data export | not found: `https://wahapedia.ru/wh40k11ed/` answered **403** to the automated fetch; the **10th-edition** export page documents linked CSV files for datasheets, abilities and stratagems with a spec file; whether an 11th-edition export exists is unknown |
| robots.txt | allows `/wh40k11ed/` and `/wh40k10ed/` pages (no crawl delay; it disallows underscore-suffixed work directories, old editions and search); lists `https://wahapedia.ru/wh40k11ed/SiteMap.xml` |
| Terms | **No licence or reuse statement found** in the pages read (10th-edition Author page and footer: "(c) Vyacheslav Maltsev 2013-2026"). The 10th-edition export page recommends mentioning Wahapedia. No 11th-edition terms page could be read |

## Mapping to the WarOrgan model

| Wahapedia | WarOrgan | Notes |
| --- | --- | --- |
| Name | `Stratagem.Name` | keep case as the site shows it (uppercase on faction pages; normalise to title case if the app does so, check) |
| "NCP" | `CPCost` | |
| Type label | `Category` | "Core Stratagem" / "<Detachment> Stratagem" |
| WHEN text | `When`; `Phase` derived from it | `Phase` is required by the model: derive from the WHEN wording ("your Shooting phase", "end of your opponent's Movement phase", ...) and keep a small normalisation table |
| TARGET / EFFECT / RESTRICTIONS | `Target` / `Effect` / `Restrictions` | keep Markdown emphasis; strip icon images and flavour |
| "+1CP" and "Or:" variants | `Options[]` | `Title` from the mode name, `CPCost` the extra cost |
| Detachment section | `Detachment.Stratagems` of the BSData detachment with the same name | detachments only on Wahapedia (or only in BSData, e.g. Boarding Actions) are reported, not guessed |

## Constraints that follow from the project decisions

- **D5:** no text from Wahapedia goes into this repo or onto GitHub. The stratagems stay in the locally generated dataset only.
- Wahapedia states no licence, so treat the content as copyrighted: acquire it only for the user's own local use, credit
  Wahapedia in the dataset README, and do not circumvent blocks (the site answered 403 to an automated fetch of one page).
  A converter that scrapes automatically would also hit that wall and would put load on a hobbyist's site.
- The data must be re-checked against the real HTML; summaries are not a basis for a parser.

## Open decision O11 - how the converter obtains Wahapedia content

| Option | How | Pros | Cons |
| --- | --- | --- | --- |
| A. Parse locally saved pages (recommended) | the user opens the core-rules page and each faction page in a browser and uses "Save page as" into a folder; the converter parses those HTML files | no automated requests, no 403 issue, no load on the site, trivially legal for personal use | about 30 manual saves per update |
| B. Built-in downloader | the converter fetches pages itself (polite rate limit, cache, descriptive User-Agent) | one command | may be blocked, adds load, scraping a site with no stated licence, more code |
| C. Both | A first, B later as an opt-in | | |

## Tasks

- [ ] Decide O11.
- [ ] Obtain real HTML for the core-rules page and the Orks page (one real example of each) and write the parser against it;
      replace every *(summary)* row above with verified facts.
- [ ] Define the stratagem schema in the converter (plain C# records mapping to the model) and a phase-normalisation table.
- [ ] Name matching: Wahapedia detachment name -> BSData detachment name, with a report for mismatches; check faction page
      names against `FactionInfo.Name`.
- [ ] Verify the 10 core stratagems and their costs (the summary reported "Counteroffensive 2CP", all others 1CP).
- [ ] Check whether the 11th-edition export CSVs exist (possible later via the site's export page; not found yet).
- [ ] Attribution text for the dataset README.

# Stratagems

**Status: source and acquisition method chosen (D12, D13), nothing built yet.** BSData wh40k-11e has no stratagem entries (only prose in abilities that mentions
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

- **D5:** no text from Wahapedia goes into this repo or onto GitHub. The cache and the stratagems stay on the local machine.
- Wahapedia states no licence, so treat the content as copyrighted: acquire it only for the user's own local use and credit Wahapedia in
  the dataset README. Do not circumvent blocks: the site answered 403 to an automated fetch of one page, so the downloader must
  stop and report on a block instead of trying to get around it (see D13).
- The data must be re-checked against the real HTML; summaries are not a basis for a parser.

## Decision D13 - built-in downloader with a persistent local cache

The converter downloads the Wahapedia pages it needs **once**, stores them **on the file system**, and reuses them on every
later run (no network access needed after the first successful download). Specification:

| Aspect | Design |
| --- | --- |
| Location | a folder on disk, outside any git repo; setting `wahapediaCacheDir`, default under the user's local application data (for example `%LOCALAPPDATA%\warorgan-wh40k-11e\cache\wahapedia\` on Windows) |
| Persistence | files stay between runs and between tool versions; a run with a populated cache makes **no** requests |
| Contents | the raw HTML of the core-rules page and one page per faction, plus a small index file (URL, fetch date/time, HTTP status, ETag / Last-Modified, content hash) so staleness is visible |
| Commands | `cache status` (what is cached, when, size), `cache rebuild` (re-download everything; conditional requests where the server supports ETag / If-Modified-Since), `cache refresh <faction>` (one page), `cache clear` (delete all cached files and the index, asking for confirmation unless `--yes`) |
| Which pages | only the core-rules page and the faction pages for the factions being converted, derived from the dataset's faction list; never a crawl of the site |
| Manners | honour `robots.txt` (re-read at each rebuild), a fixed delay between requests (at least a couple of seconds), one connection at a time, an honest descriptive `User-Agent` with a project contact, retry with back-off only on 5xx/timeouts |
| Blocks | **never work around a block.** On 403/429 the tool stops fetching, says which page failed, and tells the user they may place the saved page into the cache folder by hand (the parser treats cache files uniformly, however they got there) |
| Offline | if a page is missing and the network or site refuses, the converter still builds the dataset **without** that faction's stratagems and lists the gaps in the report |
| Privacy / licence | cache never committed (`.gitignore` entry for the default folder name, and the folder is outside the repo anyway); the dataset README credits Wahapedia; the tool prints a short notice on first download that the content belongs to its author and is for personal use |
| Parsing | the parser reads the cached HTML only; downloading and parsing are separate stages so the parser can be tested on small synthetic HTML fixtures (no real site text in the repo, D5) |

## Tasks

- [x] Decided (D13): built-in downloader with a persistent file-system cache.
- [ ] Obtain real HTML for the core-rules page and the Orks page (one real example of each) and write the parser against it;
      replace every *(summary)* row above with verified facts.
- [ ] Define the stratagem schema in the converter (plain C# records mapping to the model) and a phase-normalisation table.
- [ ] Name matching: Wahapedia detachment name -> BSData detachment name, with a report for mismatches; check faction page
      names against `FactionInfo.Name`.
- [ ] Verify the 10 core stratagems and their costs (the summary reported "Counteroffensive 2CP", all others 1CP).
- [ ] Check whether the 11th-edition export CSVs exist (possible later via the site's export page; not found yet).
- [ ] Attribution text for the dataset README.

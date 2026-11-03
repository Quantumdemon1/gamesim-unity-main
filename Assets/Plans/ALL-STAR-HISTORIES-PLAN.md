# All-Star player histories: the alumni's past games, rebuilt

Design plan, slice H0 (documents only), 2026-10-25, on `claude/all-star-histories` from main `b208d46d` (role moments
R0 to R6 and R5b, the dialogue V2 foundation, schema 29). No code changes with this slice.

**Revised the same day (H0's fix)** to agree with the lead's master plan for dialogue, props and competitions, and
All-Star (2026-10-25, against main `b208d46d`; "the master plan" below). What changed:
- schema 30 is **pooled**: one bump, built once on this branch, that carries every field the other tracks need, each
  inert until its own slice (section 5). It is its own slice, SCH30, and the histories build on it;
- the order follows the owner's choice of histories first (section 1);
- the parts of the histories that move balance wait for B9, and section 9 names them.

**Revised again after the fix's review** (same day). What changed:
- the bump's digest-observer layer and its full EditMode pin list (8.6), and an "ordinary seasons are unchanged" proof
  that can be written (8.8);
- a rule that closes schema 29 once the frozen reader is wired on main (8.2);
- Will and Janelle's row is held out, because its only source is not a game source (3.3);
- a default if H1b's measurement moves the headline rates (section 9).

**H1b built** (2026-10-26, section 13): the histories' opening state at alumni rules 1, with H1a's edition folded in,
measured against section 9's gate (no headline rate moved), so fresh seasons that seat an alumnus enrol.
Its review fix pins edition 1's words, the week-one rule and the breach rule's types. It adds the "both together" and
pacing rows to the gate, and the `ApplyFresh` merge rule with D1 (8.5). The two calls it left open are settled: the
breach rule's width (7.4) by section 14, and D-H2 (section 12) by the lead on 2026-10-29.

**Evidence base**
- Unity: `D:/GamesimAllStars` at `b208d46d`. Code paths are relative to `Assets/Gamesim/`. Documents at the repository
  root are named bare (`BALANCE_BASELINE.md`).
- Codex's isolated candidate, `origin/codex/allstar-characterization-20261012` (`3a00079e`, `24baba83`, on the frozen
  snapshot `0a1e00b9`), read with `git show` and `git diff`. **It is a reference to port, not a branch to merge**: it
  claims its own schema 29, refuses every older save and writes NPC scores directly. Its files are named by path on
  that branch (`Simulation/AlumniCatalog.cs`, `Assets/Plans/ALLSTAR-RESEARCH.md`).
- Research: `Research/AllStars/README.md`, the content brief and design review of 2026-10-21; the cast templates
  (`Simulation/CastTemplates.cs:177-249`) and the legacy lore sheets (`Simulation/Story/LoreLegacy.cs:8-119`).
- The save gate: `Assets/Plans/OLD-SAVE-ACCEPTANCE.md` (sections 1, 6 and 7); `handoffs/OWNER-DECISIONS-2026-10-22.md`
  §4-§5; `Assets/Plans/ROLE-MOMENTS-PLAN.md` §4.6.
- The master plan's sections 1 to 3 and 6: its SCH30 slice and its Stack 9 row for H1.
- The joint design note, `Assets/Plans/DIALOGUE-PLAN.md` on `claude/dialogue-v2` (the master plan's P0; "the joint
  note" below), at `4f01a559` and its review `eb820bc8`.
  - Its §6, "Schema 30: fields dialogue needs", is marked final and lists none; the review left the list as it was.
  - Its §2 defines dialogue rules 2 and later only at schema 30 (`Rules2Schema` = 30).
  - Its decision 7, as reviewed, has D2 start from the schema-30 bump merged into its branch, with its proofs run at
    30.
  - Its §6 also recommends B9's `tuningRulesVersion` for the pool.
- Code at `b208d46d`: the tests named in 8.6 and 8.8, read for this revision.

**Status words** follow ROLE-MOMENTS-PLAN: *shipped*, *gated*, *intent*, *missing*.

---

## 0. The short version

- **What:** in a fresh season that seats the real alumni, the house starts with what the alumni did to and with each
  other on the show. Old partners start closer, a houseguest starts warier of whoever nominated them, and the Hitmen
  (Derrick and Cody) walk in with their final two. After B9, a winner's record also makes them a bigger
  target.
- **What it is not:** no grudge, act or arc of this season, no romance or personal life, no knowledge a houseguest
  could not have, no guaranteed alliance and no forced result. Everything after week one is played as usual.
- **Facts:** only on-show, public game acts already in the project's research, each with a game-register source
  (section 3). Edition 1 uses five pairs from shared Big Brother seasons and a career record for all twelve.
- **Saved state: one pooled schema 30** (section 5). It carries:
  - the histories' own fields, `alumniRulesVersion` (the fresh-season gate for every alumni behaviour) and
    `alumniCatalogEdition` (the histories' storage);
  - B9's tuning field, `tuningRulesVersion`;
  - no dialogue field: the joint note's final "Schema 30" section lists none.

  Every field is written at its off value until its own slice. There is no per-pair field: the pairs live in the
  existing relationship scores and ledger.
- **Writers:** `RelationshipLedger.Move` for the shared standing of an NPC pair, `RelationshipLedger.RecordOneWay` for
  each directed part, the NPC deal writer for the final two. Never `EpisodeEngine.Change`, never a roll (section 7).
- **When:** once, at the end of `ShippedRules.ApplyFresh`, after agency's first impressions, on an unstarted season and
  before its first save. Never in `SeasonBuilder.Create`, and never on load, migration, recovery or import (section 6).
- **Old saves:** a whole-contract `FrozenEpisodeV29` reader that accepts the widest legal schema-29 state, then
  `UpgradeV29ToV30` adds the pooled off literals. The reader is wired on main at the live 29 first, and from then on
  schema 29 is closed to new values. Old seasons replay byte for byte, and no recorded digest moves (section 8).
- **Balance:** the opening state (standings, trust receipts, the Hitmen row) is measured, not tuned. It ships with
  the histories unless the measurement moves a headline rate beyond seed noise: then H1b lands with fresh seasons not
  enrolled until B9, unless the owner decides otherwise. Every new term in a decision rule waits for B9: career
  reputation, pair risk, the vote shield, Will's throw (through the competition rules), the repertoires and the stat
  overrides (section 9).
- **Slices, histories first:** H1a (the edition, inert), SCH30 (the pooled schema, every writer inert, qualified alone),
  H1b (the histories' opening state, at alumni rules 1), the optional H1c (shown), then after B9 H1d (the parts that
  move balance). The campaigns AS2, AS3 and on follow H1b (section 11).
- **The owner's direction of 2026-10-29** (section 14): All-Star seasons default to pre-existing loyalty that changes
  with the house's acts, and to reputations and known connections the house reads: alumni rules 2 to 4. From 5, by the
  owner's answer of the same day, outsider pacts grow past two while a known pair stands (14.L). S4's report (14.H)
  measures all five values for the owner.

---

## 1. The owner's decisions, 2026-10-25 (binding)

Recorded as given to this work's brief:

1. **Implement now.**
2. **Keep the twelve alumni's real names.**
3. **Keep the alumni photos (`Resources/Portraits/Glamour`) and the UMA resemblance as they are.**
4. **Build the All-Star player histories first, before the strategic campaigns, "rebuilt properly" with their own
   schema.** This is the owner's 15 Oct wish (`handoffs/HANDOFF-2026-10-18-claude.md`, "Later work" and item 3:
   own schema, NPC-to-NPC changes only through RelationshipLedger; `Research/AllStars/README.md`).
5. **Use only on-show, public game facts already present in the project's research** (`Research/AllStars`, the cast
   templates and the Codex candidate branch). **Never add off-show personal-life details about the real people.**

**Added 2026-10-29, D-H11 (section 12): "Close it (Recommended)".** Schema 29 is closed from step 1's landing on
main (PR #36). Every new value in a closed vocabulary is defined only at `schemaVersion >= 30`, with D1's chip as the
one named exception (8.2). It binds the story session, role moments and the competitions topic: delegated call,
confirmed by the owner.

**Decided by the owner, 2026-10-29: the direction for All-Star seasons** (built in section 14 as alumni rules 2, 3 and
4). Verbatim:

> "All star seasons should default to having pre-existing relationships and loyalty, with the ability for it to change
> or decay due to actions in the house. Players also have reputations and their connections are known to other
> players. For instance, if there are 3 players in an All star season known to be a preexisting alliance then the
> other players in the house might ally against them for numbers"

**Added 2026-10-29, D-H13 (section 12): "Yes, let outsider pacts grow past two".** The question was "Should outsider
pacts be allowed to grow past two while a known bloc stands? Default: no, pairs only." Built at alumni rules 5 (14.L).
The story session's cap and its one-each rule stand. Decided by the owner, 2026-10-29.

What these settle, and what they leave:
- The real-person questions the master plan put to the owner (its decision 5: photos, likeness, real histories in
  play) are closed: the names, photos and likeness stay, and on-show game history may drive play. The master plan's
  AS1b (remove or replace the photos) is therefore dropped. AS1a, the bio audit and keying `WebIntroductions` by
  template id, was approved on 26 Sep (story plan 20, decision 10) and sits in the master plan's Stack 1, outside this
  track (section 11).
- **"Own schema"** is read as the histories' own saved fields and rules value, migrated through a frozen reader under
  a new schema number. It is not Codex's colliding 29 and not a borrowed story or dialogue version. The number is the
  pooled schema 30, which this branch builds once for every track (section 5). The owner's 15 Oct wording is met
  there: the histories have their own fields, and NPC-to-NPC changes go only through RelationshipLedger.
- The 15 Oct "research stays out of the public repo" was replaced on 22 Oct (OWNER-DECISIONS-2026-10-22 §1); decision 5
  counts the Codex branch's research as project research.
- The standing real-alumni rule still holds: game arcs only, no romance, secret, conduct or removal, for every
  identity-intact alumni card, the player's included (`Simulation/Story/StoryPeople.cs:7-15,43-49`).
- Still open, and not this plan's: the campaigns' route (the master plan assumes its route C, the alumni rules value
  at schema 30, which these histories share; route B rides dialogue), their pacing caps, personalised juror wording,
  the content-version policy for card and lore edits, the web stat overrides, reach. Section 12 lists them where a
  slice needs them.

**The order that follows from decision 4.** The master plan placed the histories last (its Stack 9, after every
campaign stack, with the balance-moving parts waiting on B9). The owner chose histories first, so on this branch:
1. **H0**, this plan.
2. **H1a**, the edition, inert.
3. **SCH30**, the pooled schema 30 with every writer inert. The master plan's SCH30 slice, built here and earlier than
   that plan had it. Its step 1, the frozen reader at the live 29, lands on main as its own PR before step 2, the
   bump (8.2).
4. **H1b**, the histories' opening state, at alumni rules 1.
5. **H1c**, an optional read-only line.
6. **H1d**, after B9: the parts that move balance.

The campaigns (AS2 onward) follow H1b. The master plan's Stack 9 conditions are kept whole: priors after the first
impressions and never in `SeasonBuilder.Create` (section 6), NPC pairs only through RelationshipLedger (section 7),
Will's throw through the competition rules, and the parts that move balance after B9 (section 9).

---

## 2. What the player histories are

Three things, all read from one immutable catalogue edition (section 3):

| # | Part | What changes in a fresh season with alumni | Status today | Slice |
|---|---|---|---|---|
| 1 | **Prior relationships** between two seated alumni | the pair's opening standing (both ways, shared alliances only) and each side's trust in the other | missing on main; Codex: candidate only | H1b |
| 2 | **Career reputation** | how big a threat each alumnus's record makes them look to the house | missing; Codex: candidate only | built at alumni rules 3 (section 14) |
| 3 | **One opening commitment**: Derrick and Cody's final two | one ordinary `final_two` deal between them when both are NPCs | missing; Codex: candidate only | H1b |

What the player meets: the opening relationships and the NPCs' behaviour (who allies, deals and targets whom). A
read-only "past games" line is a later, optional slice (H1c). The edition records the career record from H1a, but no
decision reads it before H1d.

Rules that hold throughout:
- **History is not this season.** No `nominated`, `voted-against` or betrayal entry is written for something that
  happened on the show; a houseguest's history is a separate, labelled receipt (section 7).
- **Public record only.** Everybody in the house can know the on-show record, so nothing here needs a knowledge gate,
  and nothing here gives anybody knowledge of a current private deal, ballot or score.
- **Nothing is forced.** Old partners can split, old rivals can ally; the opening final two can be broken like any
  other deal.

---

## 3. The facts used: edition 1

### 3.1 The rule for a fact

A fact enters an edition only if all of these hold:
1. It is an **on-show game act** between the two people: an alliance, a nomination, an eviction vote, a final two.
2. It is **already in the project's research**: a row of Codex's `ALLSTAR-RESEARCH.md` or `AlumniCatalog.cs`, the
   cast cards or the lore sheets. This plan adds no research.
3. **The act, not a reading of it.** Where a research row also reports how someone felt (respect, friendship, a
   suspicion, "concern that he could cut him"), usually from an exit interview, the feeling is not used.
4. **Game register.** Nothing romantic, personal or about conduct, and no source whose subject is any of those. A
   fact whose only source fails this rule is held out, however plain the act. It can come back only with a
   game-register source already in the project, or by the owner's explicit call, recorded in section 1.
5. **Direction matters.** Who nominated whom decides whose trust moves.
6. **No act the research does not name.** A row that says someone "targeted" another, with no nomination or vote, is
   not scored as one.

### 3.2 Edition 1's pairs

Edition id `allstar-histories-1` (immutable: a later change is a new edition, section 5.4). Five pairs, all from
shared Big Brother seasons. Numbers are provisional (3.6): set in H1a, reported on in H1b and measured again with B9.

| Fact id | People | On-show act (research row) | Shared standing (`Move`) | Directed trust (`RecordOneWay`, impact) |
|---|---|---|---|---|
| `bb16-hitmen` | Derrick, Cody | The Hitmen alliance reached the BB16 final two (`hitmen-bb16`) | +20 | each toward the other +15 (alliance, at half weight) |
| `bb22-committee` | Cody, Tyler | The Committee alliance in BB22 (`cody-tyler-bb22`) | +10 | each toward the other +15 |
| `bb22-tyler-nominated-janelle` | Tyler, Janelle | Tyler nominated Janelle in BB22 (`janelle-tyler-bb22`) | none | Janelle toward Tyler -12.5 |
| `bb7-danielle-nominated-janelle` | Danielle, Janelle | Danielle nominated Janelle in BB7 (`danielle-janelle-bb7`) | none | Janelle toward Danielle -12.5 |
| `bb7-chilltown-renomination` | Will, Danielle | Will's Chilltown "helped persuade" the Head of Household to nominate Danielle in Janelle's place before her eviction (`will-danielle-bb7`) | none | Danielle toward Will -12.5 |

Each fact keeps the research row's source ids, minus any source that fails rule 4.

Reach by table order (`Simulation/SeasonBuilder.cs:156-160`): a default 8-house All-Stars season with the newcomer
seats Dan, Will, Derrick, Janelle, Cody, Danielle and Vanessa, so it holds three of the five pairs (the Hitmen and the
two BB7 rows). The two BB22 rows need Tyler, who is seated from nine houseguests.

### 3.3 Held out of edition 1

| Research row | Why it is held out | Could a later edition take it? |
|---|---|---|
| `will-janelle-bb7`, the whole row: the eviction at BB7's final four and the "close game relationship" reading | Rule 4: the row's only source is not a game source, and no other project research holds the eviction (`Research/AllStars`, the cast cards and the lore sheets have none). Rule 3 as well, for the reading | The eviction, only with a game-register source already in the project or the owner's explicit call (rule 4). The reading, no |
| `cody-tyler-bb22`'s "Cody later targeted Tyler" | Rule 6: the research names no nomination or vote by Cody against Tyler, so no directed part is scored. The Committee alliance is kept (3.2) | Only with a recorded nomination or vote |
| `janelle-cody-bb22` ("a group she believed opposed her") | Rule 3: a perception from an exit interview; the research names no on-show act between them | Only with an on-show act, which the research does not hold |
| `cody-danielle-reindeer`, `danielle-xavier-reindeer`, `cody-xavier-reindeer` (Reindeer Games, 2023) | Rule 3: the rows report respect and "could not assume protection" from a joint exit interview; the on-show fact is a shared cast | Recommended no (decision D-H1) |
| `janelle-rachel-snake` (Snake in the Grass), `rachel-cody-traitors` (The Traitors US1), `janelle-dan-traitors` (The Traitors US2) | Allowed by the owner's rule as on-show game facts from other shows, but held out: every card and lore sheet presents the alumni as Big Brother players, and the rows' readings rest on interviews | Yes, as a new edition for fresh seasons, if the owner wants other shows (D-H1) |
| Dan and Will | No shared season (Codex: "not a fourteenth co-player history") | No |
| Dan and Janelle, BB14 | Both played BB14 (the research's career facts), but the research holds no act between them | Only with an on-show act |

### 3.4 Career record

From the research's career facts and the lore sheets. Values are provisional (3.6).

| Alumnus | Record used | Career reputation |
|---|---|---|
| Dan Gheesling | won BB10; finalist in BB14 | winner: +4 |
| Dr. Will Kirby | won BB2 | winner: +4 |
| Derrick Levasseur | won BB16 | winner: +4 |
| Janelle Pierzina | played BB6, BB7, BB14 and BB22 | played: +2 |
| Cody Calafiore | runner-up BB16; won BB22 | winner: +4 |
| Danielle Reyes | runner-up BB3; played BB7 | finalist: +3 |
| Vanessa Rousso | third in BB17 | played: +2 |
| Tyler Crispen | runner-up BB20; played BB22 | finalist: +3 |
| Chelsie Baham | won BB26 | winner: +4 |
| Rachel Reilly | played BB12; won BB13 | winner: +4 |
| Xavier Prather | won BB23 | winner: +4 |
| Jun Song | won BB4 | winner: +4 |

Each record uses the research's words or the seasons alone, and the sources rule 4 allows: Janelle's rests on
`janelle-bb22-2020`, and the second source Codex's catalogue gives it is not used.

The record is the edition's own table, never `LoreLegacy`'s live `wonSeason`. Lore sheets are read live by key from
schema 16, so a later lore edit must not move a saved season's history. The reputation values are read only by
career reputation (7.5), built at alumni rules 3 (section 14). H1a only pins the table, and H1c may show the record's
words.

### 3.5 Identity

The edition carries its own frozen table of the twelve template ids and card names (`dr-will-kirby` is
"Dr. Will Kirby"). A houseguest is that alumnus when:
- their `sourceTemplateId` (else their id) is a table id, **and** their saved name is the table's name. This is
  `StoryPeople.IsRealPerson`'s rule, read against the edition rather than live `CastTemplates`, so a later card edit
  cannot change which saved seasons hold which pairs;
- **and** no other houseguest in the season resolves to the same id. Two custom slots built from one card make both
  ambiguous, and neither gets a history (`SeasonBuilder.Choice.CustomHouseguests` allows repeats).

A renamed card is fiction and has no history, as the real-alumni rule already says. The player on an identity-intact
card is that alumnus (section 7.2).

### 3.6 The numbers: one rule, provisional

- **Directed trust** is the in-game act's own ledger value at half weight. The ledger's permanent acts are
  `nominated` -25, `voted-against` -20 and `alliance-formed` +30 (`Simulation/RelationshipLedger.cs:46-63`), so a past
  nomination is -12.5 and a past alliance +15. Trust reads a third of an impact point
  (`Simulation/ThreatAssessment.cs:198-211`), so +15 is +5 trust and -12.5 is about -4.
- **Shared standing** is written only for a shared on-show alliance: +20 for one that reached a final two, +10 for
  another. That is about the size of a strong first impression (`EpisodeEngine.Agency.cs:18`, three points of score
  per point of compatibility).
- **Career reputation**: winner +4, finalist +3, otherwise +2, inside the reputation component's cap of 15
  (`Simulation/ThreatAssessment.cs:149-178`).
- **Pair risk** (section 7.5): a public on-show alliance of two counts as a two-person bloc to an evaluator who is
  not in it, 8 for a final-two partnership and 4 for another, inside the alliance component's cap of 20
  (`Simulation/ThreatAssessment.cs:125-128`).

These are named constants in the edition. H1b reads the first two only. Career reputation and pair risk are read
from alumni rules 3, in the nomination threat and the vote only (14.C, 14.D). They are never retuned in place: a retune
is a new edition or a later alumni rules value, for fresh seasons only.

---

## 4. The Codex candidate: kept, rebuilt, deferred or dropped

| Candidate piece | Verdict | Where |
|---|---|---|
| Immutable catalogue with an edition id, provenance per fact, read-only lists, no RNG or state API (`AlumniCatalog.cs`) | **Kept, re-derived** from the research under section 3's rule, with the frozen identity table | H1a |
| Exact template identity, renamed and duplicate alumni excluded (`AlumniHistory.Identities`) | **Kept** | H1a |
| Honorific-aware names (`CharacterNames`) | Already on main | — |
| Directed pair priors as a relationship score plus `priorTrustOffset` (`AlumniHistory.Seed` writes `edge.score` directly) | **Rebuilt**: shared standing through `Move`, directed parts as one-way trust receipts. The direct score write and the nested `RelationshipState.priorTrustOffset` field are dropped | H1b |
| Root `alumniRulesVersion` and `alumniCatalogVersion` | **Kept**, renamed `alumniCatalogEdition`, in the pooled schema 30 (inert there) and written by H1b | SCH30, H1b |
| Root `playerHistoriesEnabled` and the cast screen's on/off choice | **Dropped** for now: histories are part of an alumni season. An off switch, if the owner wants one, is a fresh season at alumni rules 0 and needs no field (D-H5) | — |
| Public `prior-history` and `prior-career` story facts (`Knowledge.SeedAlumniFact`) | **Dropped**: the record is public, so readers derive it from the edition and the cast; nothing is stored | — |
| Initialisation once, on an unplayed season, before the first save, without randomness | **Kept** | H1b |
| Initialisation from the cast screen's `SeasonBuilder.Choice`, through an `ApplyFresh(fresh, choice)` overload | **Dropped**: the histories read the saved cast, after the first impressions, at the end of `ApplyFresh(fresh)`; nothing reads the choice, and nothing runs in `SeasonBuilder.Create` (section 6) | — |
| `CareerRisk` in reputation threat | **Kept**, from the edition's record (3.4) rather than a hand list. **After B9**: it is a new term in the threat rule (section 9) | H1d |
| `KnownPairRisk` in alliance threat | **Kept** for the two shared alliances, inside the cap. **After B9**, for the same reason | H1d |
| `InvalidatePositivePrior` on a known current breach | **Kept as a reader rule** (7.4): no write, no field | H1b |
| Derrick and Cody's opening final two, NPC pair (`NpcDeals.SeedOpeningPair`) | **Kept**: one ordinary row through the existing writer | H1b |
| The same with the player on either card: an opening proposal that takes one of the week's three offer seats (`SeedOpeningProposal`, the `fillOpeningReservation` change to `Propose`) | **Dropped**: the NPC's trust receipt about the player feeds the ordinary offer pass, which decides | — |
| `ShieldValue` in eviction votes (`WebEvictionVoting` factor `alumniShield`) | **Deferred** to H1d, after B9 | H1d |
| Will's conditional throw (`ShouldThrow`, score share capped at .5) | **Deferred** to H1d, after B9 and with the competitions topic, and **rebuilt as a competition rule** keyed to a competition rules version (section 9), not an alumni hook in the scoring loop | H1d |
| Per-alumnus NPC repertoires (`AlumniStrategy.Repertoire`) | **Deferred** to H1d, after B9 | H1d |
| The "past games" conversation (`AskForIntel` with `history:<fact>:<approach>` text, `EpisodeEvent.historyCommandRef`, authored replies through `Change`) | **Deferred** to the campaigns or the dialogue layer; the event field is dropped | AS-series |
| `AlumniVoice` ordinary lines | **Deferred**; `WebIntroductions` already voices the alumni | — |
| `GAMESIM_ALLSTAR_CANDIDATE` compile symbol | **Dropped**: behaviour is gated by saved rules | — |
| The candidate's "schema 29", refusing every schema 1-28 save | **Dropped**: the pooled schema 30 with the full upgrade chain | SCH30 |
| About 1,060 lines of EditMode tests and one PlayMode file | **Reference** for cases (identity, copy isolation, no RNG on refusal, catalogue pins), rewritten against the new design | H1a, H1b, H1d |

---

## 5. Saved state: the pooled schema 30

Schema 30 is **one bump, built once**, on this branch, as the slice SCH30 (section 11), and qualified alone. The
master plan's reasons: Frozen28 was 8,092 lines in 18 files, and a frozen reader is owed at every bump, so every
field any track needs goes into this one.
- **Every writer is inert until its own slice.** SCH30 adds the fields, the frozen reader and the upgrade, and no
  writer. A fresh season at SCH30 is the schema-29 build's season apart from the schema number and the off literals,
  All-Stars seasons included.
- **Validation accepts only the off values at first.** Each slice that defines a value widens the accepted set in the
  same step, as `StoryRules.IsDefined` does for story rules 10 (a season may not hold a value no code reads).

### 5.1 The pooled field list

| Field | Where | Type | Off value (every migrated season; every fresh season until its slice) | Track, and the slice whose writer turns it on | Accepted at SCH30 |
|---|---|---|---|---|---|
| `alumniRulesVersion` | root | int | 0 | **All-Star.** The fresh-season gate for every alumni behaviour: the master plan's route C, shared by the histories and the campaigns. H1b defines 1 (`AlumniRules.Histories`). The campaigns (AS7) and the parts that move balance (H1d) take later values, in landing order | 0 |
| `alumniCatalogEdition` | root | string | null | **All-Star: the histories' storage**, the id of the edition a season's histories were built from (5.2). H1b writes `"allstar-histories-1"` | null |
| `tuningRulesVersion` | root | int | 0 | **B9**, the field `BALANCE_BASELINE.md` plans for it (Q2's row: "B9 (`tuningRulesVersion`): any formula change moves the rules-off digests"), and the joint note's §6 recommends it for the pool. B9's first tuning slice defines its first value. If B9 ends up needing none, the field stays 0, which costs far less than a schema 31 | 0 |
| **Dialogue** | — | — | — | **None.** The joint note's §6, final, and unchanged by its review | — |
| AS3's commitment association, **only if** its derivation fails (below) | the unified commitment row | a reference | null | **All-Star**, AS3 | null |

**Declaration order, fixed.** The three root fields are appended after `dialogue`, in the table's order:
`alumniRulesVersion`, `alumniCatalogEdition`, `tuningRulesVersion`. The association, if it is added, is appended
after `conditionalVeto` on `UnifiedCommitmentState`. `UpgradeV29ToV30` writes the literals in that order, and the
shape pins (8.6) hold it.

**Dialogue: no field.** The joint note's §6, "Schema 30: fields dialogue needs (final)", concludes that every
dialogue need from D2 to D7 fits schema 29's `DialogueState` and the existing deal fields:
- the conditional veto is a new deal kind, `veto_if`, in `state.deals`, with a derived id that ties it to its
  conversation;
- one statement per conversation is enough;
- every weekly cap is a count over retained rows, which are never pruned;
- the version ladder (rules 2 to 5) lives in `dialogueRulesVersion`.

So the master plan's candidates for P0 to weigh, a conditional-veto family and a weekly-cap field, are not needed.
The note names what would reopen the question (pruning, two statements in one conversation, a new window or phase,
time or a grant earned outside a conversation), and each of those takes 31, never this 30. The list is final as of
`4f01a559`, its review `eb820bc8` kept it, and it reopens only if the owner's approval of the joint note changes it
before SCH30's step 2.

**AS3's association.** The master plan expects Cody's link to his canonical vote row to be derived: maker,
beneficiary, kind and week, made unique by the duplicate-word refusal. Schema 30 is cut before AS3 is built, so that
proof is run before SCH30's list closes. It is reading plus the Unity-free subset, with no Unity. If the derivation
is not unique under every admission path, the association joins schema 30 as a null field on
`UnifiedCommitmentState`, with the nested-DTO care `conditionalVeto` took at 29 (every row of every save gains the
literal). The lead decides if the proof is not ready in time (D-H9).

**Not in the list, and why:**
- a per-pair history field: not needed (5.2);
- `playerHistoriesEnabled`: dropped (D-H5);
- dialogue's rules 2 to 5, `veto_if` and its budgets: values and rows in existing fields (the joint note's §2, §4.2,
  §5.5 and §6);
- PC5's competition rules 5 and the throw's competition rules value: values in the existing `competitionRulesVersion`;
- story rules 10, Roles' values and R8's origin value: values in existing fields (ROLE-MOMENTS-PLAN §4.1, §4.6);
- the campaigns' storylines and endings: existing `StorylineState` or `DialogueEpisodeState` storage, gated by
  `alumniRulesVersion`.

A field found missing after SCH30 lands takes schema 31 and a frozen reader of its own, which is the cost pooling
avoids.

### 5.2 The histories' storage, and the existing storage they use

The histories' own fields are the two All-Star rows above: the gate and the edition. Everything a pair holds lives
in storage the season already has.

**No per-pair field.** The design does not need one: each pair's shared standing is an ordinary score, and each
directed part is a one-way ledger receipt on the existing edge. That avoids Codex's `priorTrustOffset`, a nested-DTO
field that every relationship row of every save would have to migrate. Which facts a season holds is a pure function
of its edition and its saved cast (3.5), so it is not stored either.

| What | Where it lives | New value |
|---|---|---|
| Shared standing | `relationships[].score`, both directions | none |
| Directed trust | `relationships[].events[]`, one receipt on the holder's edge, `decayable = false` | event type `alumni-history` |
| Hitmen final two | `deals`: a `final_two` is not a vote-family kind (`UnifiedVoteFamilyValidation.cs:637`), so even in mode 2 it is an ordinary deal row, `deal-npc-<sequence>`, active, open-ended | none |
| Career reputation, pair risk, the breach rule | not stored: pure readers of the edition, the cast and the ledger | none |

No command kind (the next free stays 66), no story milestone, no dialogue rules version, no event kind and no
storyline.

### 5.3 Live validation at schema 30

- **From SCH30:** every pooled field holds its off value only: alumni rules 0 with a null edition, tuning rules 0, and
  the association (if added) null.
- **From H1b:** `alumniRulesVersion` is 0 or 1. The edition is null at 0, and a known edition at 1. Later slices widen
  the range to the values they define.
- **At 0, the type means nothing.** Schema 29 validates a ledger entry's type as free text, so the widest legal
  schema-29 state can hold an entry typed `alumni-history`, though no shipped writer makes one. At alumni rules 0 such
  an entry is an ordinary entry: validation does not refuse it, and no reader treats it specially. Every reader keyed
  to the type (7.3: `RecentImpact`, `WeeklyRecap`, the breach rule, the prune skip) checks `AlumniRules.On(s)` first,
  so an upgraded season replays as it did at 29. Frozen29's parity matrix carries such an entry (8.3).
- At 1 and every later value, every `alumni-history` entry:
  - is at week 1, permanent, and alone of its type on its edge;
  - sits on the holder's edge of an edition pair seated in the season (3.5), in a direction the edition gives a trust
    part. The holder is always an NPC; the other end may be the player (7.2);
  - holds exactly the edition's impact and description for that direction.
- Bounds are the ones the ledger already has: type up to 100 characters, impact within ±200, 512 entries an edge
  (`Simulation/EpisodeFinaleValidation.cs:20-22`).

### 5.4 Version keys: a bound, not a gate

- `AlumniRules.On(s)` is `alumniRulesVersion >= AlumniRules.Histories` with a known edition. Each behaviour names the
  version that introduced it; only range checks and "new seasons start here" may name a `Latest`.
- `AlumniEditions` keeps every edition ever shipped, compiled forever. A new edition gets a new id and is selected
  only for fresh seasons. A saved season reads its own edition by id.
- The other pooled fields follow the same rule. `tuningRulesVersion` names B9's versions as B9 defines them. The live
  schema literals become bounds or a single constant at SCH30 (8.5).

---

## 6. Fresh-season initialisation

**Where.** The last step of `ShippedRules.ApplyFresh` (`Simulation/ShippedRules.cs:26-65`):
`EpisodeEngine.EnableAlumniHistories(fresh)`. H1b adds the call; SCH30 adds none.
- **After the first impressions.** `ApplyFresh` calls `EnableAgency` at `:42`, which seeds them on a season that has
  not begun (`Simulation/EpisodeEngine.Agency.cs:46-52`), by writing scores and no ledger row. The priors come after,
  so a pair's standing is its first impression plus its history, and the first impressions are the same with or
  without alumni.
- **After the commitment rules and the unified mode** (`:47`, `:63-64`), so the Hitmen row is written under the
  season's own deal rules.
- **Never in `SeasonBuilder.Create`** (`Simulation/SeasonBuilder.cs:127-165`). Create builds the cast before any rule
  is on, and tests and harnesses call it without `ApplyFresh`. A prior written there would:
  - land before the first impressions;
  - land in seasons that never enrol, leaving receipts at alumni rules 0, which validation refuses (5.3);
  - put a rule outside the one place shipped rules live.

  Codex's candidate passed the cast screen's choice into an `ApplyFresh` overload. Here nothing reads the choice: the
  saved cast decides (below).
- If D1 changes the signature to `ApplyFresh(fresh, options)` (the master plan's Stack 2), the histories stay its last
  step and read no option.
- The director's `StartSeason` is unchanged: it calls `ApplyFresh`, materialises appearances and saves
  (`Runtime/Episode/EpisodeDirector.Season.cs:315-322`). `StressHouseTests` already fails on a rule field set beside
  `ApplyFresh`.

**When.** Only on an unstarted season: week 1, revision 0, no accepted command, alumni rules 0, edition null (the same
test `EnableAgency` uses, `Simulation/EpisodeEngine.Agency.cs:46-52`). The first save follows, so the initialised
state is what every save of the season starts from.

**Trigger: the cast, not the roster tab** (decision D-H2). The state stores no roster. A season enrols at alumni rules 1
when its cast seats at least one identity-intact, unique alumnus (3.5), unless section 9's gate has held enrolment
back until B9. So:
- a Regular season without alumni stays at 0 and null, unchanged;
- an All-Stars season enrols;
- a Regular season with an alumni custom slot enrols for that alumnus, who is the same person whichever tab seated
  them;
- the verification stress house (both rosters) enrols.

`ContentCatalog.Create`'s default scene never calls `ApplyFresh`, and neither do the web importer, load, migration or
recovery, so none of them enrol.

**Steps**, in a fixed order (fact ids ordinal):
1. Set `alumniRulesVersion = 1` and `alumniCatalogEdition = "allstar-histories-1"`.
2. For each edition pair whose two people are seated, intact and unique:
   - both NPCs: `RelationshipLedger.Move(a, b, standing)` if the pair has a standing, then
     `RelationshipLedger.RecordOneWay(holder, about, "alumni-history", impact, description, permanent: true)` for
     each direction with a trust part;
   - one of them the player: only the NPC's receipt about the player (7.2).
3. If Derrick and Cody are both NPCs, are seated, intact and unique, and the commitment rules are on: one `final_two`
   through the NPC deal writer. `NpcDeals.Strike` (`Simulation/NpcDeals.cs:519-556`) is private, so H1b adds one
   narrow internal entry, `NpcDeals.SeedOpeningPair(state, a, b)` (Codex's name). It calls Strike's logic for the
   `final_two` kind with no roll, and refuses, with nothing written, if the pair already holds an active final two or
   either is not an active NPC. Strike also records its ordinary `deal_accepted` +18 both ways; that record fades as
   usual.

**No randomness.** None of `Change`, `Roll`, `StoryRandom` or the season's `randomState` is used. The sequence numbers
the receipts and the row take come from `nextSequence`, in a fixed order, so the same choice and seed always build
the same house. The cast order itself is the table order, which is why `SeasonBuilder` never draws.

**Never inferred.** A season at alumni rules 0 is never moved to 1, whoever it seats.

---

## 7. How the pairs move

### 7.1 NPC to NPC

- **Shared standing**: `RelationshipLedger.Move` (`Simulation/RelationshipLedger.cs:204-212`). It is symmetric, has no
  roll and no arc, and sets the pair's `lastInteractionWeek` to 1. Only the two shared alliances use it, because only
  an alliance is shared by nature.
- **Directed parts**: `RelationshipLedger.RecordOneWay(..., permanent: true)` (`:160-170`). It records one holder's
  view and moves no score. It is the only writer of one-way receipts.
- **Never `EpisodeEngine.Change`.** It feeds `relationshipArcs`, which are the player's arcs, and it spends the
  season's generator.
- **Asymmetric pairs** (Codex's Janelle -15 toward Cody against Cody 0 toward Janelle) keep their asymmetry in trust,
  not in standing. Under the act rule (3.1) such a pair has no shared standing at all.

### 7.2 Pairs with the player on an alumni card (decision D-H3)

- **The NPC's view only.** One `RecordOneWay` receipt, held by the NPC, about the player.
- **No `Move`:** it would set the player's own view, which is theirs to decide (first impressions leave it alone for
  the same reason, `Simulation/EpisodeEngine.Agency.cs:57-71`). It would also stamp the player's edge's
  `lastInteractionWeek`, which `Allegiance.CommitmentKnown` reads as "in touch this week"
  (`Simulation/Allegiance.cs:167-177`).
- **No `Change`:** it rolls and writes an arc. Initialisation draws nothing, and an arc is about this season.
- **No seeded row.** The player on Derrick's or Cody's card gets no opening final two. Whether the NPC offers one is
  the ordinary weekly offer pass's call, which the receipt informs through trust (`Simulation/PlayerDeals.cs:211,385`
  reads the NPC's trust in the player).

### 7.3 What reads the receipts

| Reader | Effect | Change needed in H1b |
|---|---|---|
| `ThreatAssessment.TrustScore` through `WeightedImpact` | trust moves by a third of the impact, permanently | none, apart from the breach rule (7.4) |
| `NpcAlliances` (alliance desire), `NpcDeals` (information sharing), `PlayerDeals` (deal odds with the player), reputation bands (below 35, above 70) | as trust moves | none; measured (section 9) |
| `RelationshipLedger.RecentImpact` (receipts within two weeks) | a week-1 receipt would count as recent in weeks 1-3 | exclude `alumni-history` when `AlumniRules.On(s)`: history is not recent. No reader outside the ledger today, but keep it truthful |
| `WeeklyRecap.Movements` (`Runtime/Presentation/WeeklyRecap.cs:256-275`) | would show an NPC's receipt about the player as a week-1 move of their read of you | exclude the type when `AlumniRules.On(s)` |
| `RelationshipWeb` history column (`Runtime/Presentation/RelationshipWeb.cs:1023-1033`) | shows the player's own edge only | none (no receipt sits there) |
| Every other reader that sums or lists ledger entries (Allegiance, BlockSpeeches, FinalistRead, HouseguestNotes, SeasonAutopsy, WebJurySentiment, YourWeek and others) | — | H1b audits each one: filtered by type, reading trust as intended, or excluded. A type filter checks `AlumniRules.On(s)` first (5.3) |

**The 512-entry prune.** `Append` drops an edge's oldest entry past 512 (`Simulation/RelationshipLedger.cs:186`), and
a receipt is the oldest. H1b measures the busiest NPC edge in 12-house All-Stars seasons. If it can reach 512, the
prune skips `alumni-history` under alumni rules only; old seasons hold no receipt, so they are unaffected.

### 7.4 A breach ends inherited goodwill (reader rule)

A positive receipt stops counting once the same edge holds a permanent negative entry written this season: a broken
promise, a broken word or a betrayal between the two, as the engine's own breach writers record them. Otherwise an
old partner's betrayal would hurt less than a stranger's. With a +15 receipt, an alliance betrayal (-50) leaves trust
at about 38, outside the "dangerous" band below 35; without it, 33.

- It is a pure function of the saved edge: no write, no new value. It applies only when `AlumniRules.On(s)` (5.3).
- A ledger entry names no actor. Where the engine records a breach on both edges, the inherited goodwill ends both
  ways: the old partnership is broken either way. H1b lists which breach writers record one way and which both.
- It inherits the engine's knowledge rules. A hidden ballot writes no entry on the holder's edge until it is known.
- Negative receipts are never cancelled: an old grievance does not heal by being betrayed again.

**As built in H1b, and still to be settled.** H1b reads "a breach" as any permanent negative entry on the same edge
that is not a receipt, whatever its type. `AlumniHistoriesTests.ABreachThisSeasonEndsInheritedGoodwillButNeverHealsAnOldGrievance`
pins that reading type by type:
- **ends the goodwill:**
  - `deal_broken`: the deal settlement's two-way record, `RecordBreach`'s one-way record and the unified vote's atoms
    all write this type;
  - `promise-broken`: the legacy and the unified writer;
  - `alliance-betrayed`. Its only writer today is the player's own record, which never holds a receipt;
  - mending fences' rebuff, `amends-refused`;
  - D4's `story:double-dealt`;
  - the negative permanent story receipts: `story:sold-out`, `story:secret-exposed`, `story:showmance-betrayed` and
    `story:public-blowup`;
  - the ledger's other permanent negatives, `nominated` and `voted-against`. Nothing writes them as ledger types
    today; the grudges keep their own store;
- **keeps it:**
  - anything that fades: a `deal_broken` written off the commitment rules, `story:snubbed`, `story:argued`, hearsay;
  - the breaker's own zero-weight `deal_broken`;
  - anything positive.

This is wider than the examples above. It also counts the story receipts, the rebuff and the two reference-table
types. **The lead or the owner settles the list before the branch ships.** Once an alumni-1 season is saved, it
replays by this rule. After that, narrowing or widening it is a new alumni rules value for fresh seasons, never an
edit: as a type list in plan and code, pinned type by type the same way.

Settled by the owner's direction (section 14). Alumni rules 1 keeps the list as built; 2 and later read 14.B.

### 7.5 Career reputation and pair risk (built at alumni rules 3 by the owner's direction, 14.C, 14.D)

Both are new terms in the threat rule, so both wait for B9 (section 9). They are designed here so that H1a's edition
carries what they read.
- **Career reputation**: the edition's record value (3.4), added inside `ReputationThreat` before its clamp to 0-15.
  Every evaluator sees it, because the record is public. A newcomer player has none.
- **Pair risk**: the two shared alliances, while both members are active, count to every evaluator outside the pair
  as a bloc of two, unless a current alliance the evaluator can see already holds both. It is added inside
  `AllianceThreat`'s cap of 20.
- Both are read only from the alumni rules value H1d defines, never at alumni rules 1. A season enrolled by H1b keeps
  the threat rule it started with.

### 7.6 The Hitmen row after week one

It is an ordinary `final_two`: it shows wherever deals show, it can be kept or broken under the commitment rules, and
it is settled by its existing owners. Validation accepts it as it would any week-1 NPC deal. It is not tagged as
history and needs no tag. An offer pass that would strike a second final two between them refuses as it already does.

---

## 8. Old saves: the frozen V29 reader and the upgrade (SCH30)

All of this section is SCH30's. H1b adds only the alumni-on cell (8.7), the observer's alumni rules 1, and its own
pins and re-pins (8.6 and 8.8, "From H1b").

### 8.1 Why the reader is a whole contract

Schema 29 gained values after it froze, without a schema number, as schema 28 gained mode 2 (OLD-SAVE-ACCEPTANCE §6:
"Mode 2 joined schema 28 without a version, so no overlay on 27 can describe it"):
- story rules 11 (`StoryRules.Roles`);
- the role moments' `veto_hold` deals, the `deal-keep-` and `deal-save-` ids and the pitch board's bounds
  (`Simulation/EpisodeValidation.cs`, `Simulation/EpisodeLedgerValidation.cs`, ROLE-MOMENTS-PLAN §4.1, §4.6);
- dialogue rules 1 in new seasons, if D1's chip lands before the bump (the master plan's Stack 2). Rules 2 and later
  are not among them: the joint note defines them only at schema 30 (8.2).

The first two sit inside collections Frozen28 already validates, and Frozen28 refuses them: its pinned story rules end
at Threads. An overlay that projects a 29 payload down to 28 cannot describe them. The dialogue store, at rules 0 and
1, is a whole domain of its own that Frozen28 never saw. So `FrozenEpisodeV29` is a
whole contract in Frozen28's form: a frozen 29 DTO hydrated from the JSON, and a frozen copy of the whole schema-29
validator. It uses BCL and Newtonsoft only, never a live DTO.

### 8.2 Scope: the widest legal schema-29 state

The reader accepts what the live schema-29 validator accepts at the commit that cuts it (SCH30 step 1). That includes
values a fresh season does not get yet, such as Roles' opt-in values. The reader must hold for ever, so it covers
every value a shipped path can write at 29, not only what main writes on the day:
- **The schema-29 root**: every schema-28 field, `dialogueRulesVersion`, `dialogue` (episodes, grants and veto
  occasions) and `conditionalVeto` on every commitment row.
- **Commitment modes 0, 1 and 2; hearing rules 0 and 1**; everything Frozen28 covers (C0-C9, Wave D, the economy, the
  finale, the vote family).
- **Dialogue rules 0** (empty store, every shipped season) and **1**: the pilots, and D1's chip, which writes 1 into
  new saves (at 29 if it lands before the bump). The pilot's validator is on main
  (`Simulation/EpisodeValidation.cs:91`), so rules 1 is accepted whether or not D1 has shipped when the reader is cut.
- **No dialogue rules 2 or later.** Today's live validator accepts only 0 and 1 (`:91`), and the joint note's final §6
  keeps it so:
  - rules 2 and later need schema 30 (`Rules2Schema` = 30);
  - from D2 on, the live validator refuses them at 29;
  - D2 starts from the schema-30 bump merged into its branch, and its proofs run at 30 (the joint note's §6 item 5).

  So no save, shipped or not, holds rules 2 at 29, `veto_if` included (validation refuses it below rules 4). Frozen29
  refuses all of it, and the parity matrix (8.3) has no exception. A frozen reader is never widened after the fact:
  if the order changed and rules 2 reached main at 29 first, the reader would be cut only after that version froze.
- **Story rules** 0 to 11: 0 to 9 and 11 today (`StoryRules.IsDefined`, `Simulation/Story/StoryState.cs:291-309`), and
  10 too if the story session defines it at 29 before schema 29 closes (below).
- **Role-moments values**: `veto_hold` deals and the keep-the-block and save ids only in weeks played under Roles, the
  pitch board's three pitchers a week and the recorded impacts, the pawn conversation's and quiet tears' storylines,
  the story favours and grudges, the 92 and 100 storyline ceilings; and R7's and R8's values if they land at 29
  before schema 29 closes.
- **Ledger entry types** are free text up to 100 characters at 29, so the reader accepts any type, `alumni-history`
  included (5.3).

**Closing schema 29.** Frozen29 is cut on this branch, but schema 29 stays live on main until step 2 merges. In that
window other work can land on main and widen what a schema-29 save may hold, and saves made then would hold values
the frozen reader refuses. Three rules keep the reader whole:
1. **Step 1 lands on main as its own PR** once it qualifies, as Frozen28 was wired at the live 28 (`23e8bbae`) before
   the bump to 29 (`9d87b59e`). From then on every direct-29 load anywhere runs Frozen29. A save holding a value that
   only the live validator accepts fails to load, so any save-and-reload test of that value catches it.
2. **From that landing, schema 29 is closed.** A new value in any closed vocabulary is defined only at
   `schemaVersion >= 30`. That covers story rules 10, R7's values, R8's origin value, a new deal kind or deal-id
   prefix, a new storyline ceiling, any change to the dialogue validator, and the competition rules above 4: PC5's
   competition rules 5, PC6's 6 and later, and Will's throw value. Frozen29 freezes `CompetitionRules.Current` at 4
   (`FrozenV29ReadOnly.Leaves.cs:1527-1533`) and refuses a save above it (`FrozenV29ReadOnly.Core.cs:190`), so a
   schema-29 save at competition rules 5 would be refused by its next load (rule 1), and for good once step 2 lands.
   The story session, role moments and the competitions topic are told (section 10).
   - One named exception: D1 may still land at 29, because its chip writes dialogue rules 1, which the reader already
     accepts. Its `>= 29` literals change nothing at 29.
   - If D1 also changes what a rules-1 store may hold (for example the rules-2 fallback the joint note's P1(a) may
     bring to rules 1), that change lands in the same commit as the matching Frozen29 change and an extended parity
     matrix, and only before step 2. After step 2, never.
3. **Before step 2 lands,** preview the merge with `git merge-tree`, then re-diff every live file reachable from
   `EpisodeSaveValidation` between the commit that cut the reader and main's tip (8.3). Any change in what it accepts
   blocks the landing until that change moves to 30 or into Frozen29 under rule 2's exception.

### 8.3 How it is built and proven

- **Build:** carry Frozen28 forward by exactly the live validators' changes since its basis (`76371982`, the 2026-10-18
  re-diff, OLD-SAVE-ACCEPTANCE §6).
  - **The delta is defined mechanically, not by hand:** `git diff 76371982..<cut commit>` over every file the live
    validators' call graph reaches from `EpisodeSaveValidation`. Each file is reviewed in turn, and the list, with
    each file's verdict (frozen copy, reused, or no validation effect), goes in SCH30's report.
  - The diff is not small: 99 files under `Simulation/` and `Runtime/Persistence/` changed between `76371982` and
    `b208d46d`. Those known to reach validation include `DialogueRules.cs` and `DialogueState.cs`, `EpisodeState.cs`,
    `DealState.cs` (`DealKind.All` and `Title` gained `VetoHold`), `EpisodeValidation.cs`,
    `EpisodeLedgerValidation.cs`, `Story/EpisodeStoryValidation.cs`, `Story/StoryState.cs` and
    `EpisodeSaveValidation.cs`. They also include the role-moments predicates these call (`HoHPitches.RecordedImpact`,
    `NpcDeals.IsKeepTheBlockId`, `DealKind.RolesOnly`, `EpisodeEngine.RolesOn` and `RolesOnIn`). The unified vote
    files (`UnifiedVoteStore.cs`, `UnifiedVoteAdmission.cs`, `UnifiedCommitments.cs`) are checked too. This list is a
    starting point; the mechanical diff decides.
  - Reuse Frozen28's components where a domain did not change; give a changed domain a frozen copy.
  - Fork a component only if it cannot be reused cleanly. SCH30 decides after reading `FrozenV28ReadOnly.Core`.
  - If D1 lands on main first, its live changes join the delta (`>= 29` at `DialogueRules.cs:19` and `:97`, and any
    validator line it touches), and the reader is re-diffed against the commit that cuts it.
  - Before step 2 lands, the same diff runs again from the cut commit to main's tip (8.2, closing rule 3).
- **Prove, before the bump, at the live schema 29** (the B+ precedent, OLD-SAVE-ACCEPTANCE §6 step 1):
  `PrepareCurrentPayload` runs `FrozenEpisodeV29.Validate` on every direct-29 load.
  - Every fixture and every copy of the owner's saves passes it, input unchanged.
  - A parity matrix holds it to the live validator: every schema-29 fixture, a Roles opt-in season, a dialogue rules-1
    season (a chip season, if D1 has shipped), a rules-2 payload at 29 (both refuse), a ledger entry typed
    `alumni-history` (both accept; at 30 it upgrades and replays as an ordinary entry, 5.3), and the mutations
    Frozen28's tests use. Both accept or both refuse.
  - A malformed 29 is refused by the dispatch itself, and the test fails if the call is removed.
  - Measure the added load time, as on 2026-10-22.
  - Record the fresh-season state digests that step 2 checks against (8.8).
  - **Then step 1 lands on main as its own PR** (8.2, closing rule 1).

### 8.4 The upgrade path

```
PrepareCurrentPayload(original):
  30          -> clone, as it is
  29          -> UpgradeV29ToV30(original)                              migrated = true
  1 .. 28     -> UpgradeV29ToV30(UpgradeV28ToV29(the existing chain))   migrated = true
  otherwise   -> refused

UpgradeV29ToV30(original):
  FrozenEpisodeV29.Validate(original)      before any clone or literal
  clone; add every pooled field's off value (5.1) as a hand-written literal, in 5.1's declaration order:
    after "dialogue": "alumniRulesVersion": 0, "alumniCatalogEdition": null, "tuningRulesVersion": 0,
    and the association (if added) null after "conditionalVeto" on every commitment row
  schemaVersion = 30
```

- **`PrepareV29Payload`**: the frozen dispatch that returns a schema-29 payload for 1 to 29, per the checklist.
- **The literals are written by hand**, never from a live DTO (`JObject.FromObject` of a type a later schema widens
  broke `FrozenEpisodeV17` once). A nested literal, such as one on every commitment row, is added row by row.
- **Nothing is inferred:** `dialogueRulesVersion` and the dialogue store, story rules, commitment mode and every
  role-moments value carry over as they are. An old All-Stars season stays at alumni rules 0 and plays exactly as it
  did.
- `EpisodeSaveStore` is unchanged: checksum first, then dispatch. Load, backup checks, recovery and the staged file
  all use `TryRead`.
- A schema-29 build refuses a schema-30 save. Copy the owner's save folder in full before the play copy updates, as
  for 29.

### 8.5 Live code pinned to 29

The master plan's list of live literals that must become bounds: `DialogueRules.cs:19` and `:97`,
`EpisodeValidation.cs:88` and `EpisodeSaveValidation.cs:13`.

| Where | Today | At schema 30 |
|---|---|---|
| `Simulation/EpisodeState.cs:247` | `schemaVersion = 29` | 30, plus the pooled fields at their off values |
| `Simulation/EpisodeValidation.cs:88` | `!= 29` | the live schema, named once as a constant beside `EpisodeState`'s default, so the next bump is one line |
| `Runtime/Persistence/EpisodeSaveValidation.cs:13` | `== 29` | the same constant |
| `Simulation/DialogueRules.cs:19` (`On`) and `:97` (`TryValidate`) | `== 29` and `!= 29` | `>= 29`: the schema that introduced the store. Pinned to 29, the bump would switch dialogue off for good, and `TryValidate` would refuse every season. D1 makes this change itself (the joint note's §6 item 1); if D1 has not landed when SCH30 does, SCH30 makes it |
| `Simulation/ShippedRules.cs:74-104` (`Fields`) | — | lists `alumniRulesVersion` and `tuningRulesVersion`, in that order, after `dialogueRulesVersion`. Its completeness test requires both, because it marks every int named `*RulesVersion`; 8.6 lists what moves with them. The edition is content, not a rule field |

**At the merge with `claude/dialogue-v2`** (the joint note's §6 items 6 and 7, read at `9f8305fe`). These pins are
on that branch only (D1 and D2), so SCH30 cannot move them on this one; the merge that brings the two together moves
them, in the same commit:
- **`DialogueRules.cs`** conflicts on the `On` line. Take D2's `On`, `Admits` and `IsDefined`: its `On` reads
  `dialogueRulesVersion >= Rules1 && Admits(version, schemaVersion)`, which holds SCH30's `>= 29` for rules 1. The
  `TryValidate` line (`< 29`) is the same on both sides.
- **D1's native-29 arm** (`CurrentOldSaveTrace`'s `NativeSchema` = 29, `LegacyDigestSchema29Observer.IsNative29`,
  `NativeFields29` and `NativeTrace29`) binds a live state at 29. At 30 the committed
  `s29-mode2-dialogue1-w2-campaign` fixture loads to a schema-30 state, so the arm gets a 30-to-29 layer:
  `LegacyDigestSchema30Observer.ProjectFields29` already passes a rules-1 store through (it runs Frozen29, which
  accepts rules 1), and the native contract takes that projection as a plain schema-29 object, the way the retained
  entry does. `ProjectRetained28` refuses rules 1 (its 'rule-1' case), so a rules-1 projection goes to the native arm,
  never to 28.
- **`s29-mode2-dialogue1-w2-campaign`** joins `FrozenEpisodeV29IntegrationTests`' direct-29 cases. The SCH30 review
  ran `FrozenEpisodeV29.Validate` on it: accepted unchanged (schema 29, rules 1, one conversation); with rules 2 it is
  refused with "Dialogue rules are not enabled in this build.", and with story rules 10 with "Unsupported story rules
  version.".
- **`DialogueRules2LadderTests.AtSchema29ALaterVersionIsRefusedInTheFrozenReadersWords`**: its whole-state wording pin
  moves to `FrozenEpisodeV29.Validate`, whose words it already uses; at 30 the live validator refuses any schema-29
  state with "Unsupported episode schema.".
- **`DialogueRules2Season.HeldBelowItsSchema`** switches D2's engine and rescue cases to their conversation arm by
  itself once a new season is written at 30. **`DialogueRules2PublicRescueTests.Enrolled`** loads its schema-29
  carrier through the real migration to 30 instead of assigning the live schema.
- **Then D2's own set runs at 30 on the merge**: `DialogueRules2LadderTests`, `DialogueRules2ValidatorTests` and
  `DialogueRules2EngineTests` in the dotnet subset, and `DialogueRules2PublicRescueTests` under Unity's Mono first.
  The merge also conflicts in `CurrentOldSaveTrace.cs`, `LegacyDigestSchema29Observer.cs`, its tests and
  `Tools/baseline.txt` (a `git merge-tree` preview at `9f8305fe`), and adds D1's rows beside SCH30's in
  `OldSaveAcceptanceTests`' fixture and cell lists.
- **`ShippedRules.ApplyFresh`, the director, and the files around them (from H1b).** H1b and D1 both change these.
  - D1 (at `9f8305fe`):
    - turns `ApplyFresh(fresh)` into `ApplyFresh(fresh, null)` and adds a public
      `ApplyFresh(EpisodeState, SeasonBuilder.Choice options)`, which writes the preview chip last;
    - has the director's `StartSeason` call `ShippedRules.ApplyFresh(fresh, choice)`
      (`Runtime/Episode/EpisodeDirector.Season.cs:319` on that branch);
    - changes `ShippedRulesTests.cs`, `StressHouseTests.cs` and `EpisodeValidation.cs`.
  - H1b turns `ApplyFresh(fresh)` into `ApplyFresh(fresh, alumniHistories: true)`, over an internal
    `ApplyFresh(fresh, bool alumniHistories)`. It changes the same two test files and `EpisodeValidation.cs`, adds
    `EnableAlumniHistories` to the director-setup copy in `ShippedRulesTests`, and the lab's `BalanceLab.Fresh` calls
    the internal form.
  - **The trap.** Suppose the merge keeps H1b's one-argument form beside D1's options overload. That overload has no
    histories step. Every test that calls `ApplyFresh(fresh)` stays green, and the director's real path never enrols
    a season.
  - **The rule.** Keep one internal body, `ApplyFresh(fresh, options, alumniHistories)`. Its steps are D1's, then
    the histories as the very last step (section 6). `ApplyFresh(fresh)` and `ApplyFresh(fresh, options)` both
    delegate to it with `alumniHistories: true`. Only the lab's control passes false.
  - **The merge commit adds a test:** `ApplyFresh(fresh, new SeasonBuilder.Choice { Roster = AllStars, HouseSize = 8 })`
    builds the same state as `ApplyFresh(fresh)`, histories included. The director-setup copy keeps
    `EnableAlumniHistories` last, after D1's chip.

### 8.6 The EditMode pin list (the Unity-free subset runs none of it)

From `adding-a-saved-field-needs-a-schema-version`, a grep at `b208d46d`, and a reading of the digest layer. That
memory note's checklist comes from bumps that had no observer layer, so its sweep rule does not carry over as it is.
Paths are under `Tests/EditMode/`.

**The sweep rule.** About 126 lines in 56 test files name schema 29. A 29 changes to the live constant only where
the line means *the live schema*:
- a fresh season's schema and the current dispatch;
- the 19 `"schema 29 in memory"` message pins in `PersistenceV3`, `V4`, `V7` to `V12` and `V22` to
  `V28MigrationTests`;
- `CurrentOldSaveTrace.Current`.

Every *historical* 29 stays 29:
- `StripSchema29`, `HasSchema29Data` and `RemoveSchema29` (`PersistenceMigrationTests.cs:283-336`);
- `LegacyDigestSchema29Observer`, including its "literal schema29" input check (`:78`), and the 29 arms of its
  callers (`CommitmentRulesSeasonDigests.cs:384-387`, `ModeOneSeasonDigests.cs:229-244`);
- `PersistenceV29MigrationTests`, the `s29-` fixtures and the Frozen29 tests.

Each changed line is read by hand. No search and replace, and no rule keyed to "lines that mention
`schemaVersion`".

**The digest-observer layer.** Every season digest takes its state through a live-state projection chain:
- `CommitmentRulesSeasonDigests.Checkpoint`, which `AllWeekSeasonDigests`, `AllianceLeakSeasonDigests` and
  `PactPlanSeasonDigests` reuse through `Run`;
- `ModeOneSeasonDigests.WholeState`;
- `CurrentOldSaveTrace.Checkpoint`.

Each chain asserts a literal schema 29 today and would fail at 30. The 28-to-29 precedent added an observer
(`LegacyDigestSchema29Observer`) and moved no digest. SCH30 does the same:
- **`LegacyDigestSchema30Observer`** (new) projects a current schema-30 state to the retained 29:
  - it binds the JSON to the live state's own view, the check `LegacyDigestSchema29Observer.cs:94` makes today;
  - it requires every pooled field present at its off literal, in 5.1's order, and the association null on every
    commitment row if it is added;
  - it removes exactly those and writes schemaVersion 29;
  - it checks the exact inverse through `UpgradeV29ToV30`, field order and numeric tokens included;
  - it runs `FrozenEpisodeV29.Validate` on the projection.
- **`LegacyDigestSchema29Observer` is re-layered** to take a retained-29 `JObject` with no live state, as
  `LegacyDigestSchema28Observer.ProjectFields` already does. The own-view binding moves to the 30 observer; the
  literal-29 input check stays. If D1 has given it a native rules-1 arm (the master plan's D1 row), a rules-1
  projection reaches that arm unchanged.
- **Every caller gains a 30 arm above its 29 arm.** That covers the three chains above and the observer's direct
  users:
  - `OldSaveAcceptanceTests` (`FindDecisionAnswers28`, which also strips the association from the typed `Record`
    if it is added);
  - `PersistenceV27MigrationTests` and `LegacyDigestSchema27NativeTests`.
- **`LegacyDigestSchema30ObserverTests`** (new), beside the 29 observer's tests. Each case is refused, and the source
  is unchanged after the refusal:
  - a pooled field missing, null where an int is due, nonzero, or out of order;
  - an unknown root key;
  - a wrong header;
  - a commitment row without the association, if it is added.

  `LegacyDigestSchema29ObserverTests` move to the retained-29 entry. D1 still owns their pinned refusal at
  `:269-299`.

**Shape and order pins:**
- **`PooledSchema30ShapeTests`** (new, beside `DialogueSchema29ShapeTests`): the live root and commitment-row field
  lists equal an `s29-` fixture's plus exactly the pooled appendages, in 5.1's order, with their types and off
  defaults.
- **`DialogueSchema29ShapeTests`** moves although dialogue adds no field:
  - `ExistingRootAndCanonicalWireInventoriesGainOnlyReviewedAppendages` (`:62-70`) compares the live types to s28
    plus dialogue's two appendages. It gains the pooled appendages after them, or it is pinned to Frozen29's DTO
    instead;
  - `Accepted29` (`:254-256`) asserts schema 29 on fresh seasons. It moves to the live constant.
- **`WaveDInertSchema28Tests`** (`:58-61`) pins the root by position from the end: Wave D at `Length - 5`, and
  dialogue's two fields last. It is re-anchored by name: Wave D's segment sits immediately before
  `dialogueRulesVersion` and `dialogue`, and the pooled three follow.

**The strip helpers** (`PersistenceMigrationTests`):
- **`StripSchema29`** keeps its literal 29. It checks the payload with `FrozenEpisodeV29`'s shape and validation, in
  place of the live `CheckDtoShape` against `EpisodeState` and the live `EpisodeSaveValidation` (`:290-295`), as
  `StripSchema28` uses Frozen28 (`:240-247`). A 29 projection would fail both live checks at 30.
- **`StripSchema30`** (new) is the native current inverse:
  - the live shape and live validation;
  - every pooled field at its off literal;
  - removal, and schemaVersion 29;
  - the exact inverse through `UpgradeV29ToV30`.

  `StripSchema29` composes it first when a payload holds schema-30 data, as `StripSchema28` composes `StripSchema29`
  today (`:222-231`).
- **`Current29NativeInverseRequiresCompleteInertSourceBeforeCallerMutation`** (`:340`) becomes
  `Current30Native…` against `StripSchema30`, with a case per pooled field. A frozen-29 counterpart covers
  `StripSchema29`.

**Migration pins:**
- **`ShippedRulesTests.TheLiveSchemaIs29AndAShippedSeasonStartsOnDialogueRules0`** becomes 30, with every pooled field
  off in every fresh season, Regular and All-Stars alike.
- **The future-schema constant** in `PersistenceV4MigrationTests` (30 becomes 31). **`AddedSinceV3`** (`:199`) and its
  V4 counterpart name every pooled root field. **`CurrentDispatch_…`** (`PersistenceV3MigrationTests.cs:109`) and
  `CurrentDispatchTraversesFrozenContracts…` (`PersistenceV4MigrationTests.cs:83`) take the new dispatch.
- **The "validated as the save it becomes" chains** in `PersistenceV13`, `V14` and `V15MigrationTests` call
  `UpgradeV29ToV30`.
- **`FrozenEpisodeV28IntegrationTests`**, `PersistenceV26`, `V27` and `V28MigrationTests`: chains now end at 30.
- **`PersistenceV29MigrationTests.Direct29OffControlsLoadWithoutEnrollmentOrMigration`** (`:160-176`) expects a
  direct-29 load to stay unmigrated with its text unchanged. At 30 a direct-29 load migrates:
  - the case becomes "a direct-29 load migrates to 30, gaining the pooled off literals and nothing else";
  - a `Direct30…` case keeps the no-migration assertion for a schema-30 load.
- **`OldSaveTrace`**: every one of `UpgradeV29ToV30`'s literals joins `Additions`, and `Newest` (`OldSaveTrace.cs:131`)
  becomes 30. `Tools/OldSaves` compiles the file into the historical recorders, so the additions stay plain literals
  over JSON. `CurrentOldSaveTrace.Current` becomes 30, and its checkpoint goes through the 30 observer. H1b widens
  the observer to alumni rules 1.

**Rule-field pins.** `ShippedRulesTests.FieldsNamesEveryRuleBoundaryTheStateHasAndReadsEachFromItsOwnField` marks every
int field named `*RulesVersion`, so both new ints join `ShippedRules.Fields` (8.5). With them:
- **`BalanceLabGoldens.Rules`** (`:36-40`, asserted at `:132`) gains `alumniRulesVersion=0,tuningRulesVersion=0`. Its
  comment gives the reason, as it does for `dialogueRulesVersion=0` (9d87b59e): the fields came with schema 30, a fresh
  season plays neither, and no cell moved. Not one `Recorded` cell moves;
- **`BalanceLabParts`**' stamp (`:54`) reads `Fields`, so parts stored by an earlier build are refused as stale, which
  is what the stamp is for;
- **`ShippedRulesTests.Shipped`** (`:41-67`) gains both at 0.

**Fresh-season state digests** (8.8): recorded at step 1, checked at step 2.

**Dialogue's own pins:** none move for a dialogue field, because dialogue adds none. No test pins the
`"Unsupported dialogue schema."` message (a grep at `b208d46d`), and D1's own pins are D1's.

**Build and floors:** `Tools/SimulationTests/SimulationTests.csproj` compiles the Frozen29 sources beside Frozen28's,
and the frozen reader keeps a warnings-as-errors compile proof of its own. `Tools/baseline.txt` takes the new
floors.

**From H1b**, with its own slice:
- **`ShippedRulesTests.Shipped`** is one map for every builder today. It splits by cast: the Regular builders stay at
  alumni rules 0, and `("regular", 8, AllStars)` and `("stress", 16)` start at 1, with edition 1 checked beside the
  map.
- **`ApplyFreshGivesTheSeasonTheDirectorsInlineSetupGave`** copies the director's setup line for line (`:90-100`). It
  gains the new step in the same commit, as D3's war rooms did.
- A Regular season without alumni still starts at alumni rules 0 with a null edition, and an All-Stars season at 1
  with edition 1.
- Tests that build an alumni season through `ApplyFresh` or `StartSeason` are re-checked, and re-pinned with the
  reason if they move:
  - `StressHouseTests`: the stress house enrols;
  - `AllWeekSeasonDigests.Fresh` at 16: the stress house through `ApplyFresh`, so its 16-house runs enrol;
  - `BalanceLabReports`' All-Stars grids;
  - PlayMode `EpisodePlayModeTests.CastSelect` and `.OpeningGrounds`: each is audited for a season that runs
    `StartSeason`.

### 8.7 Old-save acceptance at 30 (OLD-SAVE-ACCEPTANCE §6 step 5's pattern)

- **The three schema-29 fixtures** (`s29-default-w2-social`, `s29-mode1-allweek-w3-veto-meeting`,
  `s29-mode2-allweek-w3-campaign`) replay through `UpgradeV29ToV30` to their recorded digests. So do all the older
  fixtures. No script, byte or digest changes; the manifest only gains rows.
- **New fixtures written by the SCH30 build**, every writer inert:
  - the default scene (mode 0, alumni 0);
  - a shipped Regular director season (mode 2, alumni 0);
  - a shipped All-Stars director season at alumni rules 0. At SCH30 an All-Stars season plays as it did at 29.
- **The alumni-on cell, written by the H1b build**: a shipped All-Stars director season (mode 2, alumni 1, edition 1,
  eight houseguests, saved mid-season with the Hitmen row live). If section 9's gate holds enrolment back, the same
  season is written through the harness path that sets alumni rules 1, and the manifest says so.
- **Extra schema-29 cells first.** Before the bump, the 29 build records any shipped schema-29 cell that has no
  fixture yet. If D1's chip ships dialogue rules 1 at 29 before SCH30, its own build writes that fixture; the
  dialogue workflow extends `CurrentOldSaveTrace`'s rules-0-only boundary for it (OLD-SAVE-ACCEPTANCE §7). If D1
  lands after the bump, its rules-1 fixture is written at 30 instead. Rules 2 and later get their fixtures at 30 from
  their own builds (the joint note's §8).
- **A sweep of copies of the owner's saves** on the 30 build, never the originals: load, migrate, replay, save at 30,
  reload, recover.

### 8.8 Ordinary seasons are unchanged

At SCH30, every fresh season, and from H1b every fresh season without an alumnus, is unchanged apart from the schema
number and the pooled off literals (`alumniRulesVersion: 0`, `alumniCatalogEdition: null`, `tuningRulesVersion: 0`,
and the association's null if it is added). Three proofs, because no schema-29 build exists once the bump is in:
- **No recorded digest moves at SCH30.** That covers every season digest, golden and fixture digest. The observer
  layer (8.6) projects each schema-30 state to the retained 29 before it hashes, so a digest that moves is a
  regression, never a re-record. This follows OLD-SAVE-ACCEPTANCE §6 step 5: recorded digests remain evidence,
  "never re-recorded to hide a change".
- **Fresh-season state digests, recorded at the live 29.** At step 1, record canonical-JSON digests of fresh seasons,
  taken after `ApplyFresh`, for every `ShippedRulesTests` builder over seeds 1, 77 and 4242. The builders are the
  quick start, Regular 6, 8 and 12, All-Stars 8 and the stress house at 16. Add `ContentCatalog.Create`'s default
  scene without `ApplyFresh`, as the scene builds it (section 6). At step 2, `StripSchema30` of each fresh schema-30
  season (pooled fields removed, schemaVersion 29) must hash to its recorded digest. This is the byte-level check
  `BalanceLabGoldens` cannot make, because the goldens hash the lab's rows, not the state.
- **`BalanceLabGoldens`' recorded cells do not move.** It holds Regular cells only. Only its `Rules` string gains the
  two fields, with the reason (8.6).

No draw, sequence, event or log line changes.

**From H1b:** the same three hold for every fresh season without an alumnus. A digest or recorded state that moves
must be of a season that seats an alumnus through `ApplyFresh`: the All-Stars 8 builder, the stress house at 16, and
the tests 8.6 lists for H1b. It is re-pinned with that reason; any other move is a regression.

### 8.9 SCH30 as built (2026-10-25, branch `claude/all-star-histories`)

Two commits, so step 1 can land on main before the bump (8.2, closing rule 1): step 1 wires the frozen reader at the
live 29; step 2 is the bump. Offline-compiled; the dotnet subset, the Frozen29 warnings-as-errors project and the
EditMode fixtures named in each commit ran under Unity's Mono; the full sets are the lead's.

**Step 1's PR carries the branch up to `f600afae`, not `f600afae` alone:** H0's plan commits (`cacc59e0`, `6d179b01`,
`74fa06b6`), AS1a (`b127cc05`, `751d6bca`) and step 1. Step 1's fresh-season digests for `regular/8/AllStars` and
`stress/16/Regular` (seeds 1, 77 and 4242) were recorded on top of AS1a, so they hash AS1a's audited bios:
`CastTemplates.ToContestant` copies a card's bio into the save, and the stress house seats alumni
(`SeasonBuilder.cs:113-119`). A cherry-pick of `f600afae` alone onto main fails those six cases. Re-recording them on
a main-based cherry-pick would work, but it records digests of cards the branch has already corrected.

- **How Frozen29 was cut.** Frozen28's 17 production files, carried by name, then by exactly the live validators'
  changes since its image. The delta was found mechanically: every member Frozen28 copied (its `SOURCE` spans in
  515246bf) was looked up in b208d46d. 472 of 485 were unchanged; the 13 that moved are carried and marked
  `DELTA b208d46d` (the schema checks and the dialogue store in `TryValidateCore`, the keep-the-block deal outside the
  role moments, the pitch board's three pitchers and recorded impacts, `StoryRules.IsDefined`, `DealKind`'s
  `VetoHold`, `DealResolution`'s kept weight, `FinalistRead.DealBreaker`), with the members they newly read
  (`RolesOn`, `RolesOnIn`, the tones' declines). The dialogue domain is a frozen copy of `DialogueRules.TryValidate`
  with no pending command: rules 0 and 1 only.
- **The file verdicts** (every non-frozen file `git diff 76371982..b208d46d` lists under `Simulation/` and
  `Runtime/Persistence/`): a frozen copy for `EpisodeSaveValidation`, `EpisodeValidation`, `EpisodeLedgerValidation`,
  `EpisodeStoryValidation`, `StoryState`, `DealState`, `DealResolution`, `FinalistRead`, `HoHPitches`, `NpcDeals`,
  `EpisodeEngine.Story` (`RolesOn`, `RolesOnIn`), `RoleTone` (its declines), `StrategyRules` (the vote ask the
  dialogue validator makes), `DialogueRules`, `DialogueCampaignV1` and `V2`; frozen data for `EpisodeState`,
  `DialogueState` and `UnifiedCommitments` (and its clone); no validation effect for the other 34 (engine behaviour,
  readers, content, the importer and the migration chain; `UnifiedVoteAdmission` and `UnifiedVoteStore` changed only
  their pending-command writer paths).
- **Two corrections to this plan.** No `deal-save-` id exists at b208d46d (8.1 named one): the role moments' only new
  id is `deal-keep-`. And `LegacyDigestSchema29ObserverTests` reach the retained-29 entry through the schema-30
  observer's state-bound entries, which compose it; `LegacyDigestSchema30ObserverTests` call the retained entry
  directly on the schema-29 fixtures.
- **What schema 30 carries:** exactly 5.1's three root fields, off. The joint note's §6 stayed final with no dialogue
  field (re-read at `c003ded2`, and again at `9f8305fe`, D2 and its review). AS3's association was not added: its
  derivation proof has not run (D-H9 is the lead's).
- **The review's fixes** (the fix commit after `936b73a4`):
  - 8.7's three schema-30 fixtures, written and recorded by `936b73a4` through `Tools/OldSaves`
    (OLD-SAVE-ACCEPTANCE 2.5): `s30-default-w2-social`, `s30-mode2-allweek-w3-campaign` (Regular) and
    `s30-mode2-allstars-w3-campaign`, all at alumni rules 0. From schema 30 a cell names the alumni rules and the
    cast, both read back from the save, so H1b's alumni-1 All-Stars cell is distinct. The recorder gained the
    `director-allstars` rules value for it.
  - `LegacyDigestSchema30ObserverTests.EveryPooledFieldRefusesMissingNullOrNonInertJson` asserts each field's own
    refusal, so a deleted value check fails it; the own-view binding alone would refuse every edited trace too.
  - The editor's v5 QA save (`LegacySaveFixtures`) is a byte copy of the `s05-default-w3-hoh` fixture, a file the
    schema-5 build wrote, in place of a live capture stripped by hand: that capture had not stripped the fields added
    since schema 14, so `FrozenEpisodeV5` refused it, and it needed an edit on every bump.
  - The closing list (8.2 rule 2, section 10, D-H11) names the competition rules above 4, and section 10 tells the
    competitions topic; 8.5 lists what the merge with `claude/dialogue-v2` moves.
- **Closing rule 3, re-checked:** `origin/main` is `772828ba`. It changes no file under `Simulation/` or
  `Runtime/Persistence/` since `b208d46d`, and `git merge-tree` of it into this branch is clean. Fetch and run both
  again before step 2 lands.

---

## 9. Balance: what ships with the histories, and what waits for B9

**The histories move balance in seasons that seat alumni, and only there.** B9 is unresolved: the studier wins about
95% of weekly competitions against a 60-70% target, and 73.5% of 8-house seasons against 25-35%
(`BALANCE_BASELINE.md`, Q3). The master plan's rule for H1 is that the parts that move balance wait for B9.

**Where the line falls.** A part waits for B9 when it **adds or changes a term in a decision rule**: who reads as a
threat, how a vote is cast, how a competition is scored, which act an NPC picks, or the statistics a houseguest plays
with. B9 is the work that retunes exactly those rules: PC5's competition rules 5, and `tuningRulesVersion` for the
formulas. A term added before B9 would be tuned against numbers that are about to move, and then tuned again.

The **opening state** is not a rule. It is a standing, a one-way receipt and one ordinary deal row, written once
through existing writers, into storage that existing readers already weigh and that B9 measures, as agency's first
impressions are. It is built in H1b and measured there, not tuned. It is enrolled in fresh seasons only if that
measurement passes the gate below. Otherwise it waits for B9 like the rest, which keeps the master plan's plain rule
whenever the opening state turns out to move balance (D-H8).

| Part | What it moves through | Waits for B9? | Slice |
|---|---|---|---|
| Shared standing (the Hitmen +20, the Committee +10) | the existing score readers | no: opening state | H1b |
| Directed trust receipts (3.2, 7.2) | `TrustScore` and its existing readers: alliance desire, information deals, the reputation bands, deal odds with the player | no: opening state. The breach rule (7.4) only bounds a receipt's own goodwill | H1b |
| The Hitmen's opening final two | the existing deal readers and settlement | no: one ordinary row | H1b |
| Career reputation (7.5) | a new term in `ReputationThreat` | **yes** | H1d |
| Pair risk (7.5) | a new term in `AllianceThreat` | **yes** | H1d |
| A trusted, bigger threat kept as a shield in early votes (Codex's `alumniShield`) | a new factor in `WebEvictionVoting` | **yes** | H1d |
| Will's conditional throw | the competition's scoring | **yes**, and through the competition rules (below) | H1d |
| Per-alumnus NPC repertoires (Codex's `AlumniStrategy.Repertoire`) | which act an NPC picks | **yes** | H1d |
| The web's All-Star stat overrides, if the owner wants them (never `personalityProfile`) | every competition, and the threat rule | **yes**: the master plan leaves them unported until B9 | H1d |

**The opening state still moves outcomes**, and the report says so. What H1b moves:
- the opening standings of the Hitmen and the Committee;
- trust, and through it alliance desire, information deals and the reputation bands;
- the player's deal odds with an alumnus NPC, when the player plays an alumni card;
- the Hitmen row's protection, and its breach.

What H1d would move as well: reputation threat by career, alliance threat by pair risk, votes, competitions and
statistics. Its likeliest effect is that a newcomer player in an All-Stars house reads as the smallest threat in the
room, so Q4 (the newcomer's nomination rate) and the winner rates move. That is the clearest reason career reputation
waits.

**Will's throw goes through the competition rules.**
- **A competition rule, not an alumni hook.** It is keyed to the competition rules version that introduces NPC throws.
  It is claimed with the competitions topic. Rules 5 is PC5's (the joint note's §9), and PC6's new minigames take 6
  or later, so the throw takes the next free value when H1d lands.
  `CompetitionRules.Current` and `CompetitionMiniGames.CurrentRules` move together, and the minigame attempt seed
  includes the rules version (`Runtime/Presentation/CompetitionMiniGames.cs:26`), so boards re-roll only in seasons
  that start under it.
- **The competition's own arithmetic.** A thrown score is cut by a share named in `CompetitionRules`, beside the
  `ThrowShare` the player's throw uses (`Simulation/CompetitionRules.cs:125`; Codex capped Will's at 0.5). It is
  applied where the player's throw is applied (`Simulation/EpisodeEngine.cs:721-733`). There is no extra draw: every
  competitor draws the rolls they draw today, in the same order.
- **Who throws** is Will's policy, with Codex's `ShouldThrow` (`24baba83`, `Simulation/AlumniStrategy.cs:54-71`) as the
  reference. Read it the right way round: Will throws to stay in good standing while his partners are safe, never to
  save a partner on the block. He throws a competition only when every one of these holds:
  - he is an NPC on Will's identity-intact card;
  - the competition is not a final, not a part of the final Head of Household, and has a category;
  - more than six houseguests are active;
  - he is not a nominee;
  - no protection partner and no ally of his is a nominee (a protection partner is the other side of an active
    `final_two`, safety or partnership deal of his; an ally shares an active alliance with him);
  - at least one of those partners or allies is active and holds Will's trust at 60 or more
    (`TrustScore(state, partner, will)`).

  It is a pure function of the saved state, with no draw. It is read only when both gates hold, the competition
  rules version and H1d's alumni value. A season below either keeps today's competitions.
- **Measured** with PC5's paired 100-seed lab runs, together with the competitions topic.

**How it is measured:**
- **`BalanceLab`'s All-Stars grids at 8 and 12** (`Tests/EditMode/BalanceLabReports.cs:26,66,104`), paired seeds,
  alumni rules 1 against 0 for H1b, and H1d's value against 1 for H1d. A harness may set an earlier value on a fresh
  season for the control; shipping never does.
- **The player on an alumni card.** The lab seats a newcomer today (`Tests/EditMode/BalanceLab.cs:197`). H1b adds a
  grid option for it, because 7.2's receipt reaches the player's own deal odds.
- **NPC-to-NPC traffic through fixtures.** Sweeps measure the engine's decisions, not the NPC world's week, so
  NPC-to-NPC outcomes are pinned with fixtures (`InstallDiaryFixture` style), never from season walks.
- **Not stacked unmeasured** with agency's first impressions: the report shows both together and the histories alone.
- **Pacing**: the histories add no interaction, so the 45-minute target for 8 players is unaffected. Report it anyway.
- **H1b's report** goes to the owner with its PR, measured on PC5's scale: 100 paired seeds a cell.

**The gate, with a default.** The report's headline rates in the alumni cells are:
- Q3, the player's season wins;
- Q4, the newcomer's nomination rate;
- the player's weekly competition wins.

If any of them moves beyond seed noise (its paired difference's 95% interval excludes zero), H1b still lands, but
with its writers wired and `ApplyFresh` not enrolling until B9:
- `EnableAlumniHistories` is reached only by a harness that sets alumni rules 1 on a fresh season, as the lab's
  control does;
- shipped seasons stay at alumni rules 0;
- the lab, the fixtures and the alumni-on cell keep the code tested.

That default holds unless the owner decides otherwise. If none of the three moves, H1b enrols fresh seasons as
section 6 says. Either way nothing is tuned in place.

A retune is a new edition or a later alumni rules value, for fresh seasons only. A saved season keeps its numbers.

**Superseded for alumni rules 2 to 5** by the owner's direction of 2026-10-29 and the owner's answer of the same day
on outsider pacts (14.A, 14.L). All-Star seasons default to them, and reputations and known connections are read now,
not after B9. Section 14.H's measurement goes to the owner and is not a gate; it has two stop conditions. The other
H1d terms (the vote shield, Will's throw, the repertoires and the stat overrides) still wait for B9, each on a later
value.

---

## 10. Claims and coordination

**Schema 30 is pooled and built once, on this branch.** It is the master plan's SCH30, moved earlier by the owner's
histories-first order, and it carries every field in 5.1.
- **Claimed jointly.** The master plan's decision 6 puts one pooled schema 30 and the dialogue version ladder to the
  owner, because it touches the story session's claims.
  - The dialogue workflow's joint note accepts it and claims no schema number: "schema 30 is the histories',
    provisional until the story session confirms it" (its §10).
  - It stays provisional until the story session and B9 confirm it. The lead records the confirmation here.
  - A peer session's decision needs the owner, recorded as "delegated call, confirmed by the owner".
- **The list closes when SCH30's step 2 starts.** Dialogue's part is final, and is none (5.1). AS3's derivation proof
  must have run by then (5.1). A field found after that takes 31.
- **No other track adds a persisted field before SCH30 lands.** If one must, the sessions agree first, and the field
  joins the pool instead of taking 30 on its own. A step that adds a persisted field is qualified alone
  (OWNER-DECISIONS §3's amendment), and SCH30 is that step.
- **Schema 29 closes when step 1 lands on main** (8.2). From then on, every track defines a new value in a closed
  vocabulary only at `schemaVersion >= 30`. That covers story rules 10, R7's and R8's values, deal kinds and id
  prefixes, storyline ceilings, dialogue validator changes, and competition rules above 4 (PC5's 5, PC6's 6 and
  later, and Will's throw value). D1's chip is the one named exception. This binds the story session, role moments
  and the competitions topic, so it is a peer-session decision for the owner (D-H11). The owner closed it on
  2026-10-29 (section 1).
- **Not claimed:** no command kind (the next free stays 66), no story milestone (10 stays the story session's, 11 is
  Roles), no dialogue rules version (the joint note claims 2 to 5 in dialogue's own field), and no competition rules
  version (5 is PC5's; the throw's is claimed with the competitions topic at H1d).
- **Alumni rules 2, 3 and 4 are taken by section 14** (the owner's direction, 2026-10-29), and 5 by its 14.L (next
  claim). The campaigns (AS7) take the next free value after S6. Because the value is a bound, AS7's value carries the
  behaviour of 2 to 5 (2 to 4 if growth is withdrawn). Tell AS7's track when it next runs.
- **Alumni rules 2 to 5 are section 14's** (2026-10-29). 5 is S3b's (14.L) and is released if growth is withdrawn
  before S6. It is the alumni rules field, not competition rules 5 (PC5's). AS7 takes the next free value after S6.
- **Settled at S6** (2026-11-01). Growth is kept (the owner's call on the stop conditions, 14.H), so 5 is not
  released, and S6 freezes it with its old-save fixture (14.J's as-built note). The next free alumni rules value is 6:
  AS7's, and it carries the behaviour of 2 to 5. Telling AS7's track is the lead's.
- **The titled names took 6** (the quick fixes, 2026-11-03, the owner's "the 'Dr.' names"; a delegated call, confirmed by
  "Yes your judgment on questions and proceed"): from alumni rules 6 the pact names and the betrayal's lines the engine
  writes call Dr. Will Kirby Will (`CharacterNames.SeasonFirstName`), words only, so every season below 6 replays the
  words it wrote; AS7 takes the next free value, 7, and carries 6.

**With dialogue:**
- **The joint note's final §6 is SCH30's input.** It adds no dialogue field to 5.1, and it fixes 8.2's dialogue scope
  at rules 0 and 1.
- **The order is the joint note's decision 7**, as its review `eb820bc8` states it, and this plan adopts it (D-H6):
  - SCH30 cuts the reader from main as it stands, with dialogue rules 0 and 1;
  - D2 starts from SCH30's step-2 commit, which the lead merges into `claude/dialogue-v2`, and its proofs run at
    30. At the live 29 every rules-2 state fails validation, so D2's subset, Mono fixtures and greedy-player hunt
    cannot pass on a 29 base;
  - D1 may land before the bump or after it (8.2's exception).

  It is also the alternative the master plan asks P0 to weigh ("landing SCH30 before D2 keeps Frozen29 to rules
  {0,1}"), and the histories-first order needs it: SCH30 does not wait for D2, and the reader is never redone after
  it. Its cost falls on dialogue: D2 waits for SCH30's step 2, so SCH30 is on D2's critical path. A preview build's
  schema-29 save at rules 2 is refused, with a note in OLD-SAVE-ACCEPTANCE §7. It is never admitted by widening a
  frozen reader after the fact.
- **`DialogueRules.cs:19` and `:97` become `schemaVersion >= 29`** in whichever of D1 and SCH30 lands first (the
  joint note's §6 item 1). It is harmless at 29.
- **The joint note names "H1b" for SCH30's work** (the live schema checks `EpisodeValidation.cs:88` and
  `EpisodeSaveValidation.cs:13`, and "H1b's schema-30 commit" for D2's base). It read this plan at `cacc59e0`, where
  H1b held the bump. Both are SCH30's now (8.5, section 11).
- **The joint note's §6 still describes D-H6 as "D2 on main first"**, the answer at `cacc59e0`. This plan's D-H6 now
  adopts decision 7, so the two agree; the dialogue workflow may want to update its wording.

**With B9:** `tuningRulesVersion` is in 30 at 0, and B9 defines its values; the joint note's §6 recommends the same.
If B9's plan needs another field, it joins the pool before the list closes.

**With role moments** (tell them): R7 (enrolment) and R8 (the holder's offer, a new origin value) wait for B9. If
either lands at 29 before step 1 lands on main, its values join the frozen reader in the same step. After that, they
are defined only at `schemaVersion >= 30` (8.2).

**With the competitions topic** (tell it when it next runs): PC5 is competition rules 5, and PC6's minigames take 6 or
later. Will's throw takes the next free value when H1d lands, and is measured with PC5's lab runs.
- **PC5 defines competition rules 5 only at `schemaVersion >= 30`**, and so do PC6's 6 and later and the throw's value.
  Frozen29 freezes the competition rules at 4 (`CompetitionRules.Current = Widened = 4`) and refuses a schema-29 save
  above it, so a season at 29 must keep being refused 5 or later by the live validator too, with the bound keyed to
  the schema (`a-current-version-constant-is-a-bound-not-a-gate`).
- If PC5 reaches main at 29 before SCH30's step 1 lands, its value joins the frozen reader in that step, as role
  moments' R7 and R8 would (above). After that, never.

**With the story session** (tell it when it next runs):
- schema 30 is pooled and built here;
- a new ledger receipt type, `alumni-history`, exists, and means nothing at alumni rules 0 (5.3);
- no story milestone is used;
- story rules 10 stays theirs. Frozen29 accepts it only if they define it at 29 before step 1 lands on main. After
  that, they define it only at `schemaVersion >= 30` (8.2).

---

## 11. Slices

Each slice is built, run in full on the suites it touches, independently reviewed, committed and pushed. Stacking
follows OWNER-DECISIONS §3's amendment: at most three unqualified steps, and SCH30 adds the fields, so it is qualified
alone. Proof inside the slices, before the lead's full sets: the offline compile
(`Tools/offline-compile.ps1 -WithoutUma`), `dotnet test Tools/SimulationTests`, EditMode fixtures under Unity's Mono
where feasible, and careful reading. The order is the owner's, histories first (section 1).

| Slice | Scope | Persisted | Player-visible | Size, qualification | Risks |
|---|---|---|---|---|---|
| **H0** | This plan | none | no | XS; review only | — |
| **H1a The edition, inert** | `AlumniEditions` with `allstar-histories-1`: the frozen identity table (3.5), the five pairs (3.2) with sources and acts, the career record (3.4), the named constants (3.6). Pure readers: identity resolution, pairs seated in a state, career value. Pins: exact roster, ids and names, pair uniqueness and direction, no RNG or state API, read-only lists, held-out rows absent, and no source that fails rule 4 (3.1) | none | no | S; stackable | an edition read live from `CastTemplates` or `LoreLegacy` (3.4, 3.5) |
| **SCH30 The pooled schema 30, every writer inert** | The joint note's list is final and adds no dialogue field; step 2 starts once AS3's derivation proof has run (5.1). **Step 1**, at the live schema 29: `FrozenEpisodeV29` on every direct-29 load, scoped to the widest legal schema-29 state (8.2) and built from the mechanical delta, with its parity proof (8.3) and the fresh-season state digests recorded (8.8). It lands on main as its own PR, which closes schema 29 (8.2). **Step 2**, the bump, after the merge-tree preview and the delta re-diff (8.2, closing rule 3): every pooled field (5.1) at its off value in its fixed order, `UpgradeV29ToV30` with the hand-written literals, `PrepareV29Payload`, the live literals made bounds (8.5), and live validation of the off values (5.3). Also the digest-observer layer and the pin list (8.6), the SCH30 fixtures (8.7), and the three proofs that ordinary seasons are unchanged (8.8). No writer: `ApplyFresh` is untouched | schema 30: every field in 5.1 | no | L-XL; qualified alone, one or two sets (step 1 qualifies and lands first) | the reader's scope and the window before schema 29 closes (8.2); `DialogueRules.On` (8.5); the observer layer and the EditMode-only pins (8.6); a field found missing later costs a schema 31; D2 waits for step 2 (section 10) |
| **H1b The histories' opening state, alumni rules 1** | `EnableAlumniHistories` as `ApplyFresh`'s last step (section 6); `AlumniRules.Histories`; the writers (7.1, 7.2, 7.6), with `NpcDeals.SeedOpeningPair` as the narrow internal entry to Strike's logic (section 6, step 3); the receipts' readers and the breach rule, each gated on `AlumniRules.On` (5.3, 7.3, 7.4); the prune check (7.3); validation of alumni rules 1 (5.3); the observer's alumni rules 1 and the alumni-on fixture (8.6, 8.7). Its pins and re-pins (8.6 and 8.8, "From H1b"): `ShippedRulesTests.Shipped` split by cast, `ApplyFreshGivesTheSeasonTheDirectorsInlineSetupGave`'s new step, `StressHouseTests`, `AllWeekSeasonDigests.Fresh` at 16, `BalanceLabReports`' All-Stars grids, and the PlayMode CastSelect and OpeningGrounds audits. Also the lab option for the player on an alumni card and the balance report with its gate (section 9) | no new field: new values in SCH30's | yes, in fresh seasons with alumni: opening relationships and behaviour. If the gate holds enrolment back, no until B9 | L; one or two sets. It adds no field, but it enrols fresh seasons, so a set of its own is recommended | receipts in week-keyed readers (7.3); the prune (7.3); the report's size and the gate (section 9) |
| **H1c Past games, shown** (optional) | A read-only line on the notebook's houseguest page for a seated pair: "Played together before: Big Brother 16, the Hitmen, to the final two". Wording comes from the edition, game register only. Captions are a contract; checked at Standard and Largest text | none | yes | S; stackable after H1b | the real-alumni rule; Inter's short-box truncation |
| **H1d The parts that move balance** (after B9) | Under one new alumni rules value, for fresh seasons only, measured with B9: career reputation and pair risk (7.5); a trusted, bigger threat as an early shield in votes; Will's conditional throw through the competition rules (section 9, with the competitions topic); per-alumnus NPC repertoires; the web's All-Star stat overrides, if the owner wants them (never `personalityProfile`) | values only (alumni rules, and a competition rules value for the throw) | yes | M-L; one or two sets | each one moves B9 numbers; the throw re-rolls boards in the seasons that start under its competition rules |

**Then the strategic campaigns**, as the feature research sliced them (`Research/AllStars`; the design review's five
gates). They follow H1b.
- **AS1a**, the bio audit, is the master plan's Stack 1 slice, already approved (story plan 20, decision 10), and
  outside this track. Rachel's displayed bio quotes a romance-related catchphrase (`Simulation/CastTemplates.cs:236`),
  and two other bios were flagged. It also keys `WebIntroductions`' alumni lines by template id. Landed on this
  branch with no freeze: it edits only All-Stars bios and lines, and `Lore.SheetFor` gives an alumnus the Legacy
  sheet by template id before any bio check (the bio comparison is the regular roster's), so `Lore.Cast` is
  unchanged. The freeze of the schema 2 to 15 lore cast in `UpgradeV15ToV16` (OLD-SAVE-ACCEPTANCE §3, F6) is still
  owed before any edit to the lore sheets (`LoreLegacy`, `LoreSheets`), the regular cards' bios or motives, or a card's
  name. Rachel's first introduction played on the same catchphrase, so it was rewritten in its slot too, under
  decision 10 and the build addendum's caution about Rachel's lines (`StoryArcs/21-bb-build-addendum.md`, G8). The owner
  may reverse that.
  - **Still unaudited after AS1a.** AS1a audited only the three bios above, Rachel's first and third introductions and
    Janelle's counts of her seasons. H1c, every campaign and every arc treat the rest as unsourced until it is sourced
    in `Research/AllStars` or softened to 3.4's record. That includes:
    - the nine other alumni bios;
    - Dr. Will's bio claim that he "won without winning a single competition", and his second introduction ("I've
      never won a competition"), where 3.4 holds only "won BB2";
    - Cody's third introduction ("Last time I took someone else to the end"), where 3.4 holds only "runner-up BB16";
    - Janelle's first introduction ("Floaters, grab a life vest!"), which may be another houseguest's phrase; the
      research holds no source either way.

  **AS1b**, removing or replacing the photos, is dropped by the owner's decision 3.
- **AS2 Campaign framework, inert.** The route the master plan assumes is its route C: the campaigns are gated by
  `alumniRulesVersion`, which these histories share. Route B, riding dialogue, is the alternative. Record the route
  first. Then:
  - the governor (at most 2 a season, 1 active, 1 opening a week, once per alumnus, decline final), counted on
    attempt rows;
  - close-on-departure endings, revalidation at each choice, a read-only settlement reader;
  - eligibility keyed by template id through H1a's identity rule;
  - the storyline budget: built in the story catalogue, a campaign counts in the pool's 92; built on the dialogue
    layer, it is a new occasion kind under a new dialogue version and counts in the joint note's 32 conversations a
    season, playing only in chip seasons until D7 (the joint note's §5.5).
- **AS3 Cody: One Vote at a Time.** One targeted, consented canonical vote promise with a single debit, settled from
  `UnifiedVoteObligations`, with a private recap. Its commitment association is derived, proven unique before
  SCH30's list closes. Otherwise it is SCH30's null field (5.1); D-H9 covers a proof not ready in time.
- **AS4-AS6**: Danielle, Janelle and Xavier, each naming its command, receipt, visibility reader and cost first.
- **AS7**: enrolment and measurement. `ApplyFresh` writes the campaigns' alumni rules value, the next free value when
  AS7 lands.
- **AS8-AS10**: the remaining eight.

A campaign may cite an edition fact in its words only when both people are seated and the fact is in the season's
edition.

---

## 12. Decisions for the lead, three of them the owner's

| # | Question | Recommendation |
|---|---|---|
| D-H1 (owner) | Edition 1's scope: shared Big Brother seasons only, or also Reindeer Games, Snake in the Grass and The Traitors? | **Big Brother seasons only** (3.3). Other shows are allowed by the owner's rule and can come as a later edition; owner's call if wanted |
| D-H2 | What enrols a season: the cast (at least one intact, unique alumnus) or the All-Stars tab? | **The cast** (section 6): the state stores no roster, and `ApplyFresh` takes only the state. H1b built this. **Confirmed by the lead, 2026-10-29:** enrolment stays by the cast. H1b's brief said "fresh All-Stars seasons only"; by the cast, the verification stress house and a Regular season with an alumni custom slot enrol too, and the gate measured All-Stars rosters only (section 13). The alternative is to narrow enrolment to the All-Stars tab once D1's options form lets `ApplyFresh` see the roster. Enrolment happens only when a season is created, so narrowing changes fresh seasons only, and saved seasons keep their value |
| D-H3 | Pairs with the player on an alumni card | **The NPC's view only**, through one receipt; no `Move`, no `Change`, no arc, no seeded row (7.2) |
| D-H4 | Will and Janelle's row | **Held out whole** (3.3): the eviction's only project source is not a game source (rule 4), and the reading fails rule 3 as well. It comes back only with a game-register source already in the project, or by the owner's explicit call, recorded in section 1 |
| D-H5 (owner) | An in-game on/off switch for histories (Codex's `playerHistoriesEnabled`) | **None for now**. If the owner wants one, it is alumni rules 0 on a fresh season and needs no field |
| D-H6 | Order against dialogue's D2 for the frozen reader | **The joint note's decision 7, as reviewed at `eb820bc8`: SCH30 first.** The reader is cut from main as it stands, with dialogue rules 0 and 1. D2 starts from SCH30's step-2 commit merged into its branch, and its proofs run at 30, so D2 waits for step 2 (8.2, section 10). This replaces H0's first answer ("D2 on main first"), which the histories-first order overtakes. The joint note leaves the order to the lead, so the lead confirms it |
| D-H7 | Confirm the pooled schema-30 claim with the story session, dialogue and B9 | Dialogue has accepted it (the joint note's §6 and §10). Confirm with the story session and B9 before SCH30's step 2, and record it in section 10 (the master plan's decision 6) |
| D-H8 | Where the line falls for "the parts that move balance wait for B9" | **New terms in a decision rule wait** (career reputation, pair risk, the vote shield, Will's throw, the repertoires, the stats). **The opening state is built in H1b**, measured and not tuned. It enrols fresh seasons only if no headline rate in the alumni cells moves beyond seed noise. Otherwise H1b lands with `ApplyFresh` not enrolling until B9, unless the owner decides otherwise (section 9). A stricter reading would hold enrolment back in every case; the gate keeps that protection wherever it matters. **The exception, decided by the owner on 2026-10-29 (section 14):** alumni rules 2 to 4 read their new terms now, and All-Star seasons default to them; so does 5, outsider pacts that grow (14.L, D-H13) |
| D-H9 | AS3's commitment association, if its derivation proof has not run when SCH30's list closes | **Run the proof first**: it needs no Unity. If it cannot run in time, the lead chooses between adding the null field to 30 as insurance and accepting a later schema 31 for it |
| D-H10 | Reserve B9's `tuningRulesVersion` in 30 before B9 has a plan | **Yes**, at 0, with validation accepting only 0 until B9 defines a value. An unused int costs far less than a schema 31 (5.1). The joint note's §6 recommends the same |
| D-H11 (owner, peer sessions) | Close schema 29 when SCH30's step 1 lands on main (8.2) | **Yes.** From that landing, story rules 10, R7, R8, competition rules 5 (PC5), PC6's 6 and later, Will's throw value and every other new closed value are defined only at `schemaVersion >= 30`, with D1's chip as the one named exception. It binds the story session, role moments and the competitions topic, so it is recorded as "delegated call, confirmed by the owner". **Decided by the owner, 2026-10-29: "Close it" (section 1).** |
| D-H12 (owner) | The owner's direction for All-Star seasons: default loyalty that changes and decays, reputations, known connections (section 1) | **The direction's three values, decided by the owner** (2026-10-29): alumni rules 2 (loyalty), 3 (reputation and known pairs) and 4 (counter-blocs), section 14. Fresh seasons start at the latest; outsider pacts grow past two from alumni rules 5 (14.L, D-H13) |
| D-H13 (owner) | Should outsider pacts be allowed to grow past two while a known bloc stands? | **Yes.** "Yes, let outsider pacts grow past two." Alumni rules 5 (14.L): up to two plus the live known pairs, never past four; the cap still counts pacts, one each. Decided by the owner, 2026-10-29 |

---

## 13. H1b as built (2026-10-26, branch `claude/all-star-histories`)

On top of `0656c58f` (SCH30's review fix). H1a was not built on its own: its edition is part of this slice. Proof without
Unity: the offline compile (`-WithoutUma`), the dotnet subset and the Frozen29 project, and the EditMode fixtures named
below under Unity's Mono. The full sets are the lead's.

**What it adds**
- `Simulation/AlumniHistories.cs`:
  - `AlumniRules`: `Off` 0, `Histories` 1, `Latest` 1, `IsDefined`, and `On(s)` (alumni rules at least 1 with an
    edition this build knows);
  - edition 1, `AlumniEditions.Histories1` (`allstar-histories-1`): the frozen identity table (3.5), the five pairs
    of 3.2 with their research rows and sources, the career record of 3.4 with its sources, and 3.6's constants. Every
    list is read-only, nothing takes a state or a generator, and nothing outside the simulation can make an edition;
  - the pure readers `AlumniHistories.Seated` (intact and unique, the player included) and `SeatedPairs`.
- `EpisodeEngine.EnableAlumniHistories` (section 6), called last in `ShippedRules.ApplyFresh`. It runs only on an
  unstarted season at alumni rules 0, and only when the cast seats an intact, unique alumnus (D-H2: the cast, not the
  roster tab). It writes, in fact-id order: `RelationshipLedger.Move` for an NPC pair's shared standing,
  `RecordOneWay(..., permanent: true)` for each directed part held by an NPC (7.1, 7.2), and then the Hitmen's
  `final_two` through the new internal `NpcDeals.SeedOpeningPair`, under the commitment and deal rules. No `Change`, no
  roll, no arc, no log line. The receipts and the deal take sequences from `nextSequence` in that fixed order.
- An internal `ShippedRules.ApplyFresh(fresh, alumniHistories)`, so the lab can play the paired control at alumni rules
  0. The director and every other assembly reach only `ApplyFresh(fresh)`.
- Validation (5.3), in `EpisodeValidation.cs` and the new `EpisodeValidation.Alumni.cs`:
  - the pooled check now accepts alumni rules 0 with no edition, or 1 with an edition this build knows. An undefined
    value, a nonzero tuning value, or an edition at 0 keeps "Schema 30's pooled rules are not enabled in this
    build."; rules 1 without a known edition reads "The alumni rules need the edition their season was built from.";
  - at 1, every `alumni-history` entry must be alone on its edge, held by an NPC, on an edition pair the season seats
    (3.5), in a direction the edition gives a part, at week 1, permanent, and with the part's exact impact and words
    ("Invalid alumni history receipt."). At 0 the type is free text, as at 29.
- The readers (7.3, 7.4), each asking `AlumniRules.On` first, so a season at 0 reads exactly as before:
  - `RelationshipLedger.RecentImpact` leaves the receipt out: a past game is not recent;
  - `WeeklyRecap.Movements` leaves it out, so week one never reports a past game as a move of the house's read of you;
  - `RelationshipLedger.WeightedImpact` (trust) applies the breach rule (7.4): a positive receipt stops counting once
    its edge holds a permanent negative entry of this season, and a negative receipt always counts;
  - the 512-entry prune (`RelationshipLedger.Trim`, now shared by `Append` and the engine's own path) drops the oldest
    entry that is not a receipt. This is cheaper than measuring whether an edge can reach 512, and no season off the
    alumni rules holds a receipt.

**The other readers, audited** (7.3's last row). Every reader of a relationship's entries in `Simulation/` and
`Runtime/` was read:
- reads only by type, never this one: `Allegiance` (betrayals), `EpisodeEngine.Introductions`, `EpisodeNpcSocial`
  (conversation counts), `Negotiation` (calls and amends), `StoryCatalog.BondsAndSecrets` (talks),
  `StoryOdds.PermanentBadBlood` (the `story:` prefix only), `StoryOdds` (broken deals) and `YourWeek` (deal outcomes);
- reads trust, as intended: `ThreatAssessment.TrustScore` and its readers (alliance desire, information deals,
  reputation bands, `PlayerDeals`' odds);
- shows the player's own edge only, which never holds a receipt: `RelationshipWeb`'s history column;
- no caller outside the tests: `MostSignificant` and `HoldsAGrudge`;
- validation and the frozen readers bound entries by size only.

**Breach writers, one way or both** (7.4):
- both edges, so the goodwill ends both ways: the deal settlement's `deal_broken` (`EpisodeEngine.cs`, both sides
  record a deal they walked away from);
- one edge only, the wronged one's view: the commitment rules' `deal_broken` (`EpisodeEngine.Commitments.cs`; the
  breaker's own entry is 0), `promise-broken`, the unified vote ledger's atoms, mending fences' rebuff, D4's
  `story:double-dealt` and the story receipts.

Which types count is listed in 7.4 ("As built in H1b, and still to be settled"), pinned type by type. The rule is
wider than 7.4's examples, and the lead or the owner settles it before the branch ships.

**What stays off until B9** (section 9, H1d). Career reputation and pair risk exist only as edition data
(`AlumniEdition.CareerReputation`, `AlumniEditions.FinalTwoPairRisk` and `AlliancePairRisk`), and no decision reads
them. The vote shield, Will's throw, the repertoires and the stat overrides are not built. Each waits for its own
alumni rules value after B9, defined only at schema 30 or later; the throw also waits for a competition rules value
claimed with the competitions topic. Alumni rules 1 never reads any of them. Since the owner's direction of
2026-10-29, career reputation and pair risk are section 14's, at alumni rules 3 (14.C, 14.D); the rest still wait.

**The balance gate** (section 9). `BalanceLabAlumniReport.AlumniHistoriesGate` (dotnet only, Explicit): All-Stars 8
and 12, alumni rules 1 against the same 100 seeds at 0. The first run played 1,600 seasons in 15.6 minutes. The
review fix's run adds a third arm and the pacing rows (below): 2,400 seasons in 17.3 minutes, with every
histories-alone row unchanged to the last digit. The gated rows were fixed before the run: Q3 season wins (passive,
studier), Q4 nomination rate (passive) and the weekly competition win rate (passive, studier).

| player | size | Q3 season wins | Q4 nomination rate | weekly competition wins |
|---|---|---|---|---|
| passive | 8 | -0.060 [-0.171, +0.051] | +0.015 [-0.035, +0.065] | +0.009 [-0.022, +0.039] |
| studier | 8 | -0.080 [-0.162, +0.002] | (not gated) | 0.000 [0.000, 0.000] |
| passive | 12 | +0.010 [-0.061, +0.081] | -0.003 [-0.050, +0.044] | +0.004 [-0.019, +0.027] |
| studier | 12 | +0.030 [-0.060, +0.120] | (not gated) | -0.002 [-0.009, +0.005] |

No gated rate moved beyond seed noise, so fresh seasons that seat an alumnus enrol, as section 6 says.
- Closest to the line: the studier at 8, whose season wins fall from 0.80 to 0.72 with an interval ending at +0.002.
- Reported, not gated: the social newcomer at 12 reaches the final two more often (0.39 against 0.25, +0.140
  [+0.015, +0.265]), with season wins +0.070 [-0.049, +0.189].
- The passive player on Cody's card moves on no metric at either size. The receipts about them reach the weekly offer
  pass only through trust.
- With ten gated comparisons at 95%, a chance crossing was about a 40% risk; none crossed.
- **The gate holds for every reported player type, not only the gated ones.** No social or on-card row's Q3, Q4 or
  weekly competition interval excludes zero. The nearest is the social newcomer's Q4 at 12, +0.051 [-0.008, +0.111].
  The widest reading of section 9's headline rates passes too.

**Pacing** (section 9: "report it anyway"; histories alone, every player type). Nothing moves that a session would
notice:
- weeks a season: 6 at 8 and 10 at 12 in both arms, a difference of exactly 0;
- the player's decisions a week in the house: 0.0 in every cell;
- commands a season: within noise everywhere. The largest are the social newcomer's, +3.4 [-0.7, +7.4] at 8 and
  +9.6 [-0.2, +19.5] at 12 on about 250 commands;
- ceremony seconds a regular week: within ±0.1 s of about 45 s (8) and 50 s (12). Two rows cross zero by less than
  a tenth of a second: the studier at 8, +0.1 [0.0, +0.2], and the player on Cody's card at 12.

The 45-minute target for 8 players is unaffected, as section 9 expected.

**Both together** (section 9: "not stacked unmeasured with agency's first impressions"). This compares the shipped
season with a second control that starts with neither the histories nor the first impressions. The lab's
`impressionsOff` sets the agency boundary to week one before `ApplyFresh`, so agency is on from week one but seeds no
first impression. Every other rule is as shipped, and a probe checks that such a season starts with no standing and
no entry. Reported, not gated:
- Q3 and the weekly competition win rate stay inside noise for every player type. The largest is the social newcomer
  at 8, whose season wins are -0.120 [-0.251, +0.011].
- **Q4 falls beyond noise in five cells**, the newcomer being nominated less often by an NPC Head of Household:
  - the passive newcomer: -0.110 [-0.202, -0.018] at 8 and -0.079 [-0.152, -0.006] at 12;
  - the studier: -0.069 [-0.136, -0.003] at 12;
  - the passive player on Cody's card: -0.168 [-0.260, -0.075] at 8 and -0.191 [-0.264, -0.118] at 12.
  - "times nominated" falls with it in the same cells, and the player on Cody's card stays longer in the house
    (+0.93 weeks at 8, +1.54 at 12).
- The histories alone move none of these, so the fall is the first impressions' doing. They have shipped in every
  season since agency (schema 20), and H1b does not change them. It is still news for the owner's report: in an
  All-Stars house the two opening states together make the newcomer a less likely nominee, and the histories add
  nothing measurable to that.
- Pacing: commands a season rise slightly in three cells (the social newcomer at 8, +5.8 [+0.1, +11.5]; the player on
  Cody's card, +2.3 at 8 and +3.2 at 12). The weeks are unchanged.

**What enrols, and what the gate measured** (D-H2, open in section 12 then; confirmed by the lead, 2026-10-29). By the cast, three kinds of fresh season
enrol:
- All-Stars seasons, the only ones the gate measured;
- the verification stress house: enrolled, played to its finale by `StressHouseTests`, not measured;
- a Regular season with an alumni custom slot: enrolled, not measured. When it seats no edition pair, it is at alumni
  rules 1 with no receipt and no deal. By reading, it then plays exactly as at 0: every reader keyed to the rules
  (`RecentImpact`, `WeightedImpact`'s breach rule, `Trim`, the recap's `Movements` and validation) acts only on
  receipts.

The opening state is measured here, not tuned. A retune is a new edition or a later alumni rules value, for fresh
seasons only.

**Pins and re-pins** (8.6 and 8.8, "From H1b"):
- `ShippedRulesTests`:
  - the shipped map splits by cast (`SeatsAlumni`, `AlumniShipped`), with edition 1 checked beside it;
  - the director's inline copy gains `EnableAlumniHistories` last;
  - the schema-30 pin becomes `TheLiveSchemaIs30AndOnlyASeasonThatSeatsAnAlumnusStartsOnTheAlumniRules`;
  - `ApplyFreshDrawsNothingFromTheSeasonsStreamAndCommitsNothing` holds unchanged: it builds a Regular season.
- `FreshSeasonStateDigestTests`: no digest is re-recorded. The six All-Stars 8 and stress 16 cases take exactly what
  the histories wrote back out (`AlumniHistoriesTests.WithoutHistories`) and still hash to their schema-29 records, so
  the histories are the only change, byte for byte.
- `PooledSchema30ShapeTests.LiveValidationAcceptsOnlyTheValuesASliceHasDefined` (was `...TheOffValuesUntil...`).
- `LegacyDigestSchema30ObserverTests`: the nonzero-pooled case per field and value, and two new cases. A season on the
  alumni rules is never projected to 29, and is admitted natively at 30 (`LegacyDigestSchema30Observer.AdmitNative30`,
  used by `CurrentOldSaveTrace` for such a season, at the live 30 only).
- `PersistenceV30MigrationTests`: "alumni-rule" holds the undefined 2, and a new "alumni-unedited" holds 1 without an
  edition.
- Re-checked, and unmoved:
  - `StressHouseTests`: the stress house enrols (Dan, Will, Derrick, Janelle, Cody, Danielle and Vanessa, and three
    pairs) and plays to its finale;
  - `CastTemplateTests` and `AlumniAuditSaveTests`: both arms enrol alike;
  - `AllWeekSeasonDigests.Fresh` at 16 and `BalanceLabReports`' All-Stars grids: Explicit reports with no recorded
    digest, so they now play enrolled seasons;
  - missed here, and re-pinned on 2026-11-01: `BalanceLabTests.BudgetNoughtRows` hashes the smoke grid, whose 24
    All-Stars seasons seat alumni. It is `5481eec784acd1ed`, was `3878f7a1d2ea39a8` (main at `37416049`). The 540
    Regular rows are main's byte for byte, so only seasons that seat an alumnus moved (8.8, "From H1b");
  - `BalanceLabGoldens`: Regular cells only, so its `Rules` string is unchanged.

**New tests**
- `AlumniHistoriesTests`: 31 shared cases and 8 more in the subset only (the internal control). They cover:
  - edition 1's pins;
  - the opening state at 8 and 12 over five seeds;
  - the Hitmen row;
  - the same house on every run, and the same histories on every seed;
  - nothing drawn, and only the histories' own sequences minted;
  - every builder without an alumnus unchanged;
  - nothing but `ApplyFresh` writing a history;
  - the player on an alumni card;
  - renamed and repeated cards;
  - validation of every receipt, and of the rules with their edition;
  - type-only meaning at 0;
  - the readers and the breach rule;
  - the prune;
  - a JSON round trip that plays on the same;
  - two seasons to a winner;
  - in the subset only, field for field against the control for all seven builders.
- `AlumniHistoriesSaveTests` (EditMode only, 4): through the real `EpisodeSaveStore`, a fresh alumni season saves at
  30, loads unmigrated, plays on the same, saves and reloads in week three, and recovers its backup. The SCH30 build's
  All-Stars fixture loads at alumni rules 0 and is never enrolled.
- `AlumniHistoriesRecapTests` (EditMode only, 1): week one's recap never sums a past-game receipt into a move, and off
  the rules the same entry is summed as before.

**The alumni-on old-save cell** (8.7), recorded in the commit after `0fef7b59` by that build itself
(OLD-SAVE-ACCEPTANCE 2.6): `s30-mode2-allstars-alumni1-w3-campaign`, a shipped All-Stars director season of eight at
alumni rules 1 and edition 1, saved at the week-3 campaign with the Hitmen's final two live, four receipts, and 48
commands to its end. Seed 3004, because the alumni-0 fixture's seed 3002 had evicted Derrick by then. It is the 55th
cell. `CurrentOldSaveTrace` and the historical `FindDecision` reader admit a season on the alumni rules natively at
30, since it has no 29 view.

**The review fix** (2026-10-26, the commit after `274fefa5`). Tests, the lab and this plan only. No simulation or
runtime source changed, and no test case was added, so the floors stand.
- **Edition 1's words are part of the save format.** Validation compares each receipt's description with its part's,
  character for character (5.3). `EditionOneHoldsFivePairs` (in the subset) now pins verbatim:
  - all seven parts' `Description`s;
  - the five pairs' `Act`s;
  - the twelve career `Record`s.

  Its message says the edition is immutable, and that a wording change needs a new edition id, selected for fresh
  seasons only. The pin matters most for the BB22 receipts (the Committee both ways, Tyler's nomination of Janelle).
  The alumni-1 fixture is an eight-house season and holds none of them, so before this pin an edit in place to their
  words passed every test. After such an edit, no saved 9-to-12-house alumni season, or its backup, would load. This
  applies to any later wording audit of the edition in the style of AS1a: it is a new edition, never an edit.
- **Week one** (5.3) is pinned. A receipt redated to week two, in a season in week two where the ledger allows that
  week, is refused with "Invalid alumni history receipt.". The review's one-line form could not work: in week one the
  general ledger check refuses a week-two entry first, with "Invalid relationship history.".
- **The breach rule** is pinned type by type (7.4's "As built" list), with what keeps the goodwill beside it. Its
  width is the lead's or the owner's to settle before the branch ships.
- **The gate report** adds the "both together" arm, the pacing rows and the all-player-types line (above).
- **The merge with D1** gets its `ApplyFresh` rule and its trap in 8.5.
- **D-H2** was marked open for the lead in section 12 (confirmed by the lead, 2026-10-29). The kinds of season that enrol without being measured are listed
  above.
- **Not done: a twelve-house alumni-1 save fixture**, which the review marked optional. The verbatim pin catches the
  wording edit it guards against, in the subset and in seconds. A fixture needs the recorder run from an archive of
  the build that writes it, and four more EditMode cases. Record it if the lead wants the BB22 receipts in a saved
  file: a `director-allstars` season of 12 at the next seed that keeps Cody, Tyler and Janelle in the house to its save
  point.

Evidence for the review fix (C: scratch, not committed):
- three mutations, each caught by its named test and restored by SHA-256: the Committee part's words, `receipt.week`'s
  check, and the breach rule narrowed to `deal_broken`;
- a fourth mutation, the breach rule counting zero-weight entries, also caught;
- the 2,400-season gate report.

**For the lead: PlayMode and visual checks.** No PlayMode file changed.
- `EpisodePlayModeTests.CastSelect` starts All-Stars seasons through `StartSeason`, so they are now enrolled. Expected
  green. `OpeningGrounds` builds without `ApplyFresh`, so it is unaffected.
- The verifier's stress house enrols, so the standalone verify walk is worth one run.
- Capture an All-Stars season at Standard and Largest text:
  - week one's recap shows no past-game movement;
  - on an alumni card, the relationship web's history column for Cody shows no receipt;
  - the Hitmen's final two shows only where the player may see NPC deals.
- Optional: a sweep of copies of the owner's saves. Every one is alumni rules 0 and should play exactly as before.

---

## 14. The owner's direction of 2026-10-29: loyalty that moves, reputations, known blocs

Section 13 is H1b as built, so this is section 14. The owner's direction, verbatim:

> "All star seasons should default to having pre-existing relationships and loyalty, with the ability for it to change
> or decay due to actions in the house. Players also have reputations and their connections are known to other
> players. For instance, if there are 3 players in an All star season known to be a preexisting alliance then the
> other players in the house might ally against them for numbers"

This section builds the direction as three alumni rules values. It also answers 7.4's open question (call 2), and it records the owner's decision on section 9's gate and D-H8 for these values. A fourth value, 5, builds the owner's answer of the same day on outsider pacts (14.L).

Paths: `Simulation/`, `Runtime/` and `Tests/EditMode/` are under `Assets/Gamesim/`. `Tools/` is at the repository root. Line numbers are at `4ab8c054`.

| Value | Constant | What it adds | Slice |
|---|---|---|---|
| 2 | `AlumniRules.Loyalty` | The graded receipt share. Inherited trust is eroded by this season's wrongs and public nominations. Pacts and kept words repair it, up to a cap. A betrayal ends it. Old grievances heal through play. The shared standing is written off once, at a public split | S1 |
| 3 | `AlumniRules.Reputation` | Career reputation and the risk of known past pairs, in the nomination threat and in the vote. What the player is shown | S2, S5 |
| 4 | `AlumniRules.CounterBlocs` | Outsiders want each other more while a known pair stands. Outsider pacts hunt the bloc, with a stop above zero | S3 |
| 5 | `AlumniRules.GrowingPacts` | While a known pair stands, an outsider pact of houseguests may take members past two: one a week, while nobody outside it knows of it, up to two plus the live pairs and never past four. Decided by the owner, 2026-10-29 | S3b |

**Rules for every part:**
- no saved field and no schema change;
- no new closed-vocabulary value (no ledger type, fact kind, act kind, grudge cause or vote factor code);
- no new draw site and no new sequence-id site, with one exception at value 5: a join's `alliance-formed` receipts, two per member in the house, through the existing writer (14.L.D.5);
- no `EpisodeEngine.Change`. The one write between NPCs is `RelationshipLedger.Move`, and at value 5 a join's `RelationshipLedger.Record` of `alliance-formed`, the record `Form` writes (`Simulation/NpcAlliances.cs:271`);
- no opening write. A fresh season at 2, 3 or 4 is the H1b opening with a different integer;
- every new term asks `AlumniRules.At(s, v)` with v of 2 or more, and is added only when it is nonzero. Seasons at 0 and 1 therefore play byte for byte as before, and the `s30-mode2-allstars-alumni1-w3-campaign` fixture replays unchanged;
- every word shown comes from edition 1 or from this season's public events. No new fact about a real person;
- nothing reads a private ballot, a private deal or another houseguest's score into anyone's trust (section 2's rule).

### 14.A Default loyalty

**Enrolment is already the default.**
- Both public `ApplyFresh` forms pass `alumniHistories: true` (`Simulation/ShippedRules.cs:26,37`), and the histories run last (`:101`).
- H1b's gate passed (section 13).
- A fresh season that seats an identity-intact alumnus therefore starts with:
  - the Hitmen's +20 and the Committee's +10 standing (`Simulation/EpisodeEngine.AlumniHistories.cs:55`);
  - the directed receipts (`:56-61`);
  - the Hitmen's final two (`:63-65`).
- **The one change:** fresh seasons now start at `AlumniRules.Latest` instead of the literal `Histories` (`:49`). "New seasons start here" is the one use `Latest` is allowed (`Simulation/AlumniHistories.cs:15`).

**No opening pact.** An `AllianceState` for the Hitmen or the Committee would cause four problems:
- it would be private at birth (`Simulation/Story/StorySystems.cs:131-145`), the opposite of "known";
- it would take half of an 8-house's pact cap of 2 (`Simulation/NpcAlliances.cs:88`);
- it would shift sequence ids;
- it would decide in advance what play should decide.

Loyalty is carried by the standing, the graded receipt (14.B) and the final two. Being known is carried by 14.D.

**Enrolment scope stays "by the cast" (D-H2).** The stress house and a Regular season with an alumni custom slot enrol too. The lead confirmed D-H2 on 2026-10-29: enrolment stays by the cast.

**Section 9's gate and D-H8, decided by the owner.**
- Section 9 holds a value back when a gated rate moves "unless the owner decides otherwise".
- D-H8 makes new decision terms wait for B9.
- The direction decides otherwise for values 2 to 4, and the owner's answer for value 5 (14.L):
  - All-Star seasons default to them;
  - reputations and known connections are read now, not after B9.
- The measurement in 14.H is reported to the owner and is not a gate. It has two stop conditions.
- The other H1d terms still wait for B9, each on a later value: the vote shield, Will's throw, the repertoires and the stat overrides.

**The record, on insertion of this section:**
- **§0:** one line pointing here.
- **§1:** the direction verbatim, as "Decided by the owner, 2026-10-29".
- **§1:** the question and the answer verbatim (D-H13).
- **§2's table, row 2, and §3.4's last paragraph:** "built at alumni rules 3 (section 14)".
- **§3.6's last paragraph:** career reputation and pair risk are read from alumni rules 3, in the nomination threat and the vote only (14.C, 14.D).
- **§7.4's last paragraph:** "Settled by the owner's direction (section 14). Alumni rules 1 keeps the list as built; 2 and later read 14.B."
- **§7.5's heading:** "(built at alumni rules 3 by the owner's direction, 14.C, 14.D)".
- **§9:** the gate is superseded for values 2 to 5.
- **§10, new claim:**
  - alumni rules 2 to 4 are taken by section 14, and 5 by its 14.L, released if growth is withdrawn before S6; alumni rules 5 is not competition rules 5 (PC5's) (2026-10-29);
  - the campaigns (AS7) take the next free value after S6. Because the value is a bound, AS7's value carries the behaviour of 2 to 5 (2 to 4 if growth is withdrawn);
  - tell AS7's track when it next runs.
- **§12:**
  - D-H8: the exception;
  - D-H12, new: "the direction's three values, decided by the owner";
  - D-H13, new.
- **§13's "What stays off until B9":** points here.

### 14.B Change and decay: the graded receipt share (value 2)

**B.1 The rule.** It runs only at alumni rules 2 or later, and only on an edge that holds an `alumni-history` receipt. Every other edge returns the existing sum, `edge.events.Sum(e => e.impactScore * Weight(e, week))` (`Simulation/RelationshipLedger.cs:97-98`), bit for bit as before.

For holder H's edge about A, in week w:

```
L  = sum of |impact| over H->A's permanent (decayable == false) non-receipt entries with impact < 0
N  = sum over u in AlumniLoyalty.PutUpWeeks(s, A, H, includeThisWeek: true)
         of max(0, NominationErosion - NominationFadePerWeek x (w - u))
E  = L + N                           erosion: what A did to H this season, on the ledger and at the public ceremonies
P  = min(LoyaltySpan, sum of impact over H->A's permanent non-receipt entries with impact > 0
                      + sum of impact x Weight(e, w) over H->A's "deal_fulfilled" entries)
betrayed  = H->A holds a permanent non-receipt entry with impact <= -BetrayalLine
goodwill  = betrayed ? 0 : clamp01(1 - max(0, E - RepairWeight x P) / LoyaltySpan)
grievance = clamp01(1 - max(0, P - E) / LoyaltySpan)
WeightedImpact = sum over the edge's entries, in stored order, of
    receipt:  impact x (impact > 0 ? goodwill : grievance)
    other:    impact x Weight(e, w)
```

| Constant (`AlumniLoyalty.`) | Value | Why |
|---|---|---|
| `LoyaltySpan` | 30 | Twice a receipt's +15. One `alliance-formed` (+30) fully heals an old grievance. It is also the most repair can bank |
| `BetrayalLine` | 30 | Catches a High deal broken (−30), a final two broken (−45), and a broken vote promise (−31) or heavier. It leaves out an information promise (−22) and Low or Medium deals (−15, −22.5) (`Simulation/WebRules.cs:77-93`, loyalty 5) |
| `RepairWeight` | 0.5 | Play together banks half its value against erosion. With P capped at 30, at most 15 points of wrongs are absorbed |
| `NominationErosion` | 35 | Half of a nomination's grudge of 70 (`Simulation/Story/EpisodeEngine.StoryHooks.cs:25-31`), read from the public record instead of the grudge store |
| `NominationFadePerWeek` | 1 | Half the grudge store's 2 a week (`Simulation/Story/Grudges.cs:28`), so "you put me up" stays a season-long memory, as the store intends (`:97-101`) |

**B.2 What counts.**

- **Erodes:**
  - every permanent negative non-receipt entry on H's own edge. These are the ledger's breach writers (`deal_broken`, `promise-broken`, `amends-refused`, `story:double-dealt`, the negative permanent story receipts), the list 7.4 pins for rule 1. L inherits the ledger's own knowledge rules, as rule 1 does;
  - A putting H up at a public ceremony (N):
    - a nomination (a veto save included) or the replacement, as A's Head of Household week;
    - read through `FinalistRead.PutUp` (`Simulation/FinalistRead.cs:621`) over `s.ledger.power`. Rows of earlier weeks are filtered by `row.week < s.week`. The final eviction's row is skipped by the predicate at `:461`;
    - with `includeThisWeek`, it also reads this week's row and, while `!s.evictionResolved`, the live block (`s.hohId`, `s.nominees`). A week counts once;
    - a nomination writes nothing between NPCs on the ledger (`Simulation/EpisodeEngine.cs:927-950` writes a memory and a grudge). The replacement's `veto-replacement` entry fades (`Simulation/EpisodeEngine.BetweenHouseguests.cs:56-68`). So N never double-counts L.
- **Never read:**
  - ballots. An ally's vote against and a Head of Household's flip are written to grudges from `s.votes` at the reveal (`Simulation/Story/EpisodeEngine.StoryHooks.cs:93-116`);
  - grudge severity. The store stacks causes and keeps only the last one (`Simulation/Story/Grudges.cs:67-73`), so it cannot be filtered;
  - leaving a pact. It is a grudge, not a ledger entry (`:51-55`).
- **Repairs (P, capped at `LoyaltySpan`):**
  - `alliance-formed` +30;
  - `promise-made` +15, written both ways (`Simulation/NpcPromises.cs:182-183`);
  - `promise-kept`;
  - the positive permanent story receipts;
  - `deal_fulfilled` at its fading weight (`Simulation/EpisodeEngine.cs:2323-2324`).
  - The cap stops a weekly word to the closest houseguest (`Simulation/NpcPromises.cs:131-141`) from banking an unbounded shield.
- **Neither:** talk, conversations, rumours, hunts, confrontations, hearsay, `deal_accepted`, and other fading traffic. They still count for themselves in the sum.
- **Ends for good:** a single permanent wrong of −30 or worse. No repair undoes it. An old partner's betrayal never hurts less than a stranger's (7.4).
- **Decay with time:** only N ages, by 1 a week. Nothing fades for neglect: the direction says "due to actions in the house".

**B.3 Worked values.** Trust = 50 + impact/3.
- For the Hitmen rows (Cody about Derrick), the edge holds the receipt +15 and the +18 handshake. The handshake is full through week 3, fades from week 4 and is gone by week 7 (`Simulation/RelationshipLedger.cs:75-82`).
- For the Committee rows (Tyler about Cody), the edge holds the receipt +15 and no handshake.

| Situation | Rule 1 (as built) | Rule 2 |
|---|---|---|
| Hitmen, week 1 | 33, trust 61.00 | 33, trust 61.00 |
| Hitmen, a bare permanent `deal_broken` −20 (H1b's test entry) | −2, 49.33 | share 1/3: 3, 51.00 |
| Hitmen, a Medium deal broken (−22.5) | −4.5, 48.50 | share 0.25: −0.75, 49.75 |
| The same, with an in-house pact (+30) | 25.5, 58.50 | E − 15 = 7.5, share 0.75: 36.75, 62.25 |
| Hitmen, a broken vote promise (−31) | −13, 45.67 | betrayed: −13, 45.67 |
| Hitmen, final two broken at the final choice (−45, handshake gone) | −45, 35.00 | betrayed: −45, 35.00 |
| Committee, Cody as Head of Household nominates Tyler in week 3 | 15, 55.00 | N = 35, share 0: 0, 50.00. The standing −10 is also written off (B.4) |
| The same, allied in-house (+30) | 45, 65.00 | E − 15 = 20, share 1/3: 35, 61.67 |
| Week 9 after that nomination, no pact | 15, 55.00 | N = 29, share 1/30: 0.5, 50.17. Then 1/30 more a week |
| Four words (+60) banked before the nomination | 75, 75.00 | P capped at 30, share 1/3: 65, 71.67 |
| Janelle about Tyler (−12.5), then they ally (+30) | 17.5, 55.83 | grievance 0: 30, 60.00 |
| Janelle about Tyler, one `promise-made` +15 | 2.5, 50.83 | grievance 0.5: 8.75, 52.92 |

**B.4 The standing at a public split (the one write).** The loyalty that decisions read is the shared standing in the score. The score is read by:
- `Desire` (`Simulation/NpcAlliances.cs:69`);
- `NominationWeight` (`Simulation/EpisodeEngine.cs:865-872`);
- the vote's relationship factor (`Simulation/WebEvictionVoting.cs:605`).

The design:
- **Where:** at the end of `NominationEffects`, after the promise loop (`Simulation/EpisodeEngine.cs:947-949`). That covers the first nominations (`:912`) and the replacement (`:1036`).
- **When:** all of these must hold:
  - alumni rules 2 or later;
  - the Head of Household and the nominee are the two non-player houseguests of a seated pair with `Standing > 0`, resolved by `Seated`'s identity rule;
  - neither put the other up in an earlier week (`AlumniLoyalty.PublicSplitWeek(s, a, b, includeThisWeek: false) == 0`).
- **What:** `RelationshipLedger.Move(s, hohId, nomineeId, -fact.Standing)`, which is −20 or −10. It is symmetric, writes no entry, draws nothing and mints nothing (`Simulation/RelationshipLedger.cs:244-252`).
- It happens at most once per pair per season, and it removes exactly what the history added. It is driven by an event, not a weekly ratchet.
- **Pairs with the player are skipped** (D-H3). The final two survives a nomination, as today.
- **In practice this is the Committee's (12-house), not the Hitmen's.**
  - The Hitmen's opening final two adds 50 to each one's reluctance to nominate the other (`Simulation/StrategyRules.cs:80,431-438`).
  - A final two settles only at the final Head of Household's choice (`Simulation/DealResolution.cs:305-311`), so the 50 holds while both are in.
  - So the Hitmen do not split in public. Their known pair ends when one of them leaves, which is the bloc the direction asks the house to answer.
  - In the default 8-house, the Hitmen are the only seated shared alliance (§3.2).

`PublicSplitWeek(s, a, b, includeThisWeek)` returns the first week in `PutUpWeeks(s, a, b, …) ∪ PutUpWeeks(s, b, a, …)`, or 0.

**B.5 Both directions, and the player.**
- The share reads only H's own edge and the public put-ups of H by A. It is H's private trust (7.4), and the other edge does not move.
- An NPC who holds a receipt about a player on a card (`Simulation/EpisodeEngine.AlumniHistories.cs:58-60`) reads:
  - the player's breaches on that edge;
  - the player's nominations of them, from the power rows;
  - the player's pacts, which write `alliance-formed` (`Simulation/EpisodeEngine.Pacts.cs:146-148`).
- The player's own view holds no receipt and is untouched.

**B.6 Code (S1).**
- **New file `Simulation/AlumniLoyalty.cs`**, with a hand-written two-line `.meta`. It holds `public static class AlumniLoyalty` with:
  - the five constants;
  - `internal static double WeightedImpact(EpisodeState s, RelationshipState edge)`;
  - `public static double GoodwillShare(s, holderId, aboutId)` and `GrievanceShare(...)`;
  - `public static List<int> PutUpWeeks(s, byId, ofId, bool includeThisWeek)`;
  - `public static int PublicSplitWeek(s, a, b, bool includeThisWeek)`;
  - `internal static void SplitInPublic(s, hohId, nomineeId)`.
  - Everything a test needs is public. The two internal members are reached through `RelationshipLedger.WeightedImpact` and the engine's nomination path.
  - It is not on `AlumniEdition`/`AlumniEditions`, whose public methods may not take a state (`Tests/EditMode/AlumniHistoriesTests.cs:191-207`).
- **`Simulation/RelationshipLedger.cs`:**
  - after the null check at `:96`, insert `if (AlumniRules.At(state, AlumniRules.Loyalty)) return AlumniLoyalty.WeightedImpact(state, edge);`;
  - `:97-101` and `BreachesInheritedGoodwill` (`:105-106`) stay word for word as rule 1;
  - the doc at `:85-93` names both rules.
- **`Simulation/EpisodeEngine.cs`,** after `:949`: `AlumniLoyalty.SplitInPublic(s, s.hohId, id);`. It does nothing below 2.
- `RecentImpact` and the recap's `Movements` already ask `On`, so they carry into 2 unchanged.
- **As built in S1** (2026-10-29, on `4ab8c054`). Where the code asked for it, the build keeps the intent and differs in the letter:
  - `PutUpWeeks` reads the live block whenever this week's Head of Household and nominees hold it, not only while `!s.evictionResolved`. The reveal sets `evictionResolved` (`Simulation/EpisodeEngine.cs:559`) before it writes this week's row (`:579`), so a read between the two would have lost the week. After the reveal the live block and the row are the same week, and a week counts once.
  - Repair takes a `deal_fulfilled` entry once, at its weight, and every other permanent positive entry in full. B.1's two sums would count a permanent `deal_fulfilled` twice. No writer makes one (`Simulation/EpisodeEngine.cs:2324,2336`, `Simulation/EpisodeEngine.Commitments.cs:127` and `Simulation/EpisodeEngine.UnifiedVote.cs:156,181` write it fading), so every season reads the same either way.
  - Below 2, `GoodwillShare` and `GrievanceShare` read 1, `PutUpWeeks` is empty and `PublicSplitWeek` is 0.
  - The career and pair-risk docs at `Simulation/AlumniHistories.cs:171-175,215,218` are S2's, with the reader.

### 14.C Reputations (value 3)

**The term.** `KnownBlocs.CareerFor(s, id)` is the edition's `CareerReputation`: winner 4, finalist 3, played 2 (`Simulation/AlumniHistories.cs:176-186,216`).
- Winners: Dan, Will, Derrick, Cody, Chelsie, Rachel, Xavier, Jun.
- Finalists: Danielle, Tyler.
- Played: Janelle, Vanessa (`:287-299`).
- It returns 0 below 3, for a newcomer, and for a card that is not intact and unique. It resolves with `Seated`'s rule (`:341-357`: template, name, uniqueness) as a loop over the contestants, with no dictionary.

**Where it reaches, and where it does not.**
- It reaches the nominations, including the replacement, through `ThreatTerm` (14.D.3), and the vote (14.D.4). Every evaluator sees it, because the record is public (3.1).
- It does not reach `Total`, `Assess` or `RankedTargets` (`Simulation/ThreatAssessment.cs:48-95`). So the rumour subject (`Simulation/NpcSocialActions.cs:517`), the Head of Household's pitch target (`Simulation/HoHPitches.cs:35`) and broken-word grudge scaling (`Simulation/Story/Grudges.cs:138-152`) are the same at every value.
- That keeps the alumni term out of the weekly rumour pass (six lead traits spread information each week, `Simulation/NpcSocialActions.cs:93-116`; each rumour costs a symmetric −15, `:62`). Feeding it would compound on the hub.
- A newcomer's reputation stays what they do in the house.

### 14.D Known connections (value 3)

**D.1 What is known.**
- The edition's acts and careers are public, on-show acts (3.1).
- Only shared alliances (`Standing > 0`) form known pairs: `bb16-hitmen` (Derrick, Cody) and `bb22-committee` (Cody, Tyler) (`Simulation/AlumniHistories.cs:246-261`).
- The three nomination facts shape only their holders' receipts.

**D.2 `KnownBlocs`.** New file `Simulation/AlumniKnownBlocs.cs`, with a hand-written `.meta`, `public static class KnownBlocs`. It reads `AlumniHistories.EditionOf(s)`, never `AlumniEditions.Latest`.
- **`LivePairs(s)`:** the seated pairs with `Standing > 0` where both houseguests are `Active` and `PublicSplitWeek(s, a, b, includeThisWeek: true) == 0`.
  - Early-outs, in this order: `!AlumniRules.At(s, Reputation)`, the edition is null, no shared-alliance card is seated.
  - Loops over the edition's two shared facts and the contestants. Pinned equal to `SeatedPairs` filtered. There is no cache by revision.
  - Private things never end a pair in the house's eyes: scores, receipts, deals (the final two included), ballots, a pact's end.
- **`Risk(fact)`:** 8 (`FinalTwoPairRisk`) when `Standing >= FinalTwoAllianceStanding`, otherwise 4 (`AlliancePairRisk`) (`Simulation/AlumniHistories.cs:213,219`).
- **`RiskFor(s, evaluatorId, targetId)`:** the sum of `Risk` over live pairs that hold the target and not the evaluator. It skips a pair when an active alliance the evaluator can see (`Knowledge.AllianceVisibleTo`, `Simulation/Story/StorySystems.cs:235-242`) holds both members, so a re-formed pact is never counted twice.
- **No groups.** Pairs are counted as pairs. The chain through Cody is two facts, never a trio. `PairOf(Derrick, Tyler)` is null, and rule 3.1 forbids saying the three were one. A counter-pact's bound (14.L.C) counts live pairs: two, plus one for each live pair, capped at four. It is arithmetic, never shown, and never names a group.

**D.3 In the nomination threat.** New `public static double ThreatAssessment.TotalWithPastGames(state, evaluatorId, targetId)`.
- It equals `Total` below 3.
- Otherwise it is the same five components, with:
  - `KnownBlocs.RiskFor` inside `AllianceThreat`'s `Math.Min(20, …)` (`Simulation/ThreatAssessment.cs:125-128`);
  - `CareerFor` before `ReputationThreat`'s clamp (`:177`);
  - each added only when nonzero.
- It is built by a private `bool pastGames` through `AssessBeforeCommitmentEffects` (`:51-62`), which every existing caller passes as false.
- `ThreatTerm` (`Simulation/EpisodeEngine.Agency.cs:95-101`) reads it in place of `Total`, for both the Head of Household's reading and the pact-mates'. Nothing else reads it.
- The uplift is at most 0.4 × 16 + 0.2 × 16 = 9.6 nomination points for Cody (pair risk 12 + career 4). It is less where the caps bind.

| Reading, 12-house, all live | Derrick | Cody | Tyler |
|---|---|---|---|
| An outsider | 8 | 12 | 4 |
| Derrick | — | 4 | 4 |
| Cody | 0 | — | 0 |
| Tyler | 8 | 8 | — |

**D.4 The vote mirror.**
- New ephemeral class `WebVoteAlumniTerm { string nomineeId; double pairRisk, career; }`, placed beside `WebVoteSafetyTerm` (`Simulation/WebEvictionVoting.cs:131`).
- `WebVoteOptions` gains `public List<WebVoteAlumniTerm> alumniTerms = new List<WebVoteAlumniTerm>();` after `:128`, commented "derived, never saved". It starts empty, as `Tests/EditMode/ModeTwoHouseReaderTests.cs:130-135` requires.
- `FromNative`, after `:277`: `if (AlumniRules.At(state, AlumniRules.Reputation)) options.alumniTerms = KnownBlocs.VoteTerms(state, voterId);`. It holds nonzero entries only.
- `ThreatWithSafety` (`:407`) takes a `WebVoteAlumniTerm alumni`:
  - `pairRisk` goes inside `Math.Min(20, …)` at `:413`;
  - `career` goes into the sum at `:421`.
- The leaf at `:404-405` passes null. `:606` passes the nominee's entry.
- No factor code is added. "Threat" stays hidden in the read (`Simulation/VoteRead.cs:39`). Imported web rounds carry an empty list.
- About 3.6 ballot points at the default weights (0.25 of 1.1, `Simulation/WebEvictionVoting.cs:197-198`).
- The vote's term is not cut by the 15 clamp that can cut the nomination's term. A test pins that difference.

### 14.E Counter-blocs "for numbers" (value 4)

**E.1 Desire.** `NpcAlliances.Desire`'s return (`Simulation/NpcAlliances.cs:78-85`) gains `+ KnownBlocs.CounterDesire(state, npcId, targetId)`, added only when nonzero:

```
Near(x)        = {x} + x's partners in live pairs
counted(a, b)  = live pairs p with p ∩ (Near(a) ∪ Near(b)) empty
CounterDesire  = CounterBlocDesire x min(CounterBlocPairsCap, |counted(a, b)|)        0 below CounterBlocs
```

- **Constants:** `CounterBlocDesire` = 7.5, half the source's shared-threat price of 15 (`:79`): a known pair is a bloc nobody has quarrelled with yet. `CounterBlocPairsCap` = 2.
- **Effect:**
  - 8-house, the Hitmen live: +7.5 between any two outsiders;
  - 12-house, both pairs live: +15. That is the owner's three "for numbers", counted as two pairs;
  - members, and the partners of members, get 0 toward anyone.
- **The gate stays.** At a score of 25, neutral trust, social 6 and no pact carried, Desire is about 12.7 + 2 × compatibility.
  - Today, at the floor, a pair needs compatibility 7, so most pairs need a higher score.
  - With one known pair, compatibility 3.
  - With two, −1.
  - The score floor of 25 (`:149-151`), the cap of one NPC-only pact per three NPCs and one each (`:88-99`), and pacts born as pairs (`:258-272`) are the story session's limits, untouched. Outsider pacts grow past two from value 5 only, by the owner's answer (14.L).
- **Side effects:**
  - Build agendas and the NPC world's pairing lean toward fellow outsiders (`Simulation/NpcAgendas.cs:62-68`);
  - an outsider player is wanted more, so more invitations reach them (`Simulation/NpcDeals.cs:186-189`).

**E.2 The bloc hunt, with a stop.** In `NpcAgendas.Of`, after the `CommonThreat` loop (`Simulation/NpcAgendas.cs:54-58`) and before `Hold` (`:59`), a second loop runs over the same mates in the same order:
- it skips the player;
- it returns a `Hunt` with `targetId = KnownBlocs.CounterTarget(s, npcId, mate)` when that is not null.

`CounterTarget(s, hunterId, mateId)`:
- null below 4;
- the candidates are the members of `counted(hunter, mate)` who are `Active`, with `s.Score(mateId, m) >= HuntStopLine`;
- **and who are no pact-mate** (`APactMate`): no active pact holds the candidate and the hunter, nor the candidate and the listener where the hunter can see it (`Knowledge.AllianceVisibleTo`). This is an addition the text above did not state, made at the S3 review (2026-10-31): `counted` reads only live pairs, never pacts, so as first built a pact could hunt its own member - the player on Cody's card taken into two outsiders' pact (`AllyThroughInvitation`), a story trio holding Cody, a hunter's other pact with Cody, or the listener's. The visibility check keeps a pact of the listener's the hunter has not heard of out of the hunter's reading. Outsider pacts still hunt the bloc;
- they are ordered by `RiskFor(s, hunterId, m)`, highest first, then id ordinal, so the hub comes first. The order is the hunter's reading, not the listener's.

How it behaves:
- **The stop.** `HuntStopLine` = 6 = −`HuntImpact` (`Simulation/EpisodeEngine.Agency.cs:39`). A hunt costs the listener 6 with the target (`Simulation/NpcSocialActions.cs:427-436`).
  - The route fires only while the listener rates the target at 6 or more, so on its own it never takes a score below 0.
  - So it cannot reach `CommonThreat`'s −10/−5 (`Simulation/NpcDeals.cs:143-150`), Desire's −20 or the confrontation line.
  - It ends when the listener is no longer warm to the target, and the agenda falls back to `Hold`.
- **The target's side.** `Move` is symmetric (`Simulation/NpcSocialActions.cs:682`). The target's view of the listener falls by the same amount, at most by the listener's starting warmth.
- The ordinary `CommonThreat` route still comes first: unchanged below 5; from 5 it passes over a non-player who shares a houseguests' pact with both (14.L.F).
- **The player is never the listener.** A player on a bloc card can be the target. The existing gossip roll is then drawn on the beat's keyed stream (`:440`). That is an existing draw site.
- **The house's targeting follows with no further term.** Outsider Heads of Household and their pact-mates read pair risk and career through `ThreatTerm`. The vote reads the same through D.4. The hunt has lowered the listener's score, which `NominationWeight` reads.

**E.3 Bounds.**
- Desire is a reading, not a turn. Build stops at 25 (`Simulation/NpcAgendas.cs:77-83`).
- The pursuit runs once a week (`Simulation/NpcSocialActions.cs:214-219`).
- The hunt stops above zero.
- Every term ends the week the pair stops being live: one departure or one public split.
- From 5, a grown pact sends at most one hunt a member a week, each listener's stop holds on its own, and nobody hunts a pact-mate of both (14.L.D.6, 14.L.F).

**E.4 Held back: target agreements against the bloc.** Not built. Built only on the owner's word after the 14.H report, as the next free alumni value, and only if the report shows outsider pacts forming without known-pair members being put up more often. The specification:
- in `NpcDeals.Offer`, between the final-three rung (`Simulation/NpcDeals.cs:221`) and the information rung (`:223`), add `if (allied && warmth > CommonThreatWarmth && KnownBlocs.CounterTarget(state, npcId, targetId, forDeal: true) != null) return DealKind.TargetAgreement;`. Here `forDeal` drops the listener stop and the player exclusion;
- `TargetFor` (`:586-591`) becomes `CommonThreat(...) ?? <the same>`.

It is held back because a target agreement pulls a nomination by 40 (`Simulation/StrategyRules.cs:52`), against at most 9.6 from threat, and weekly agreements against one hub are the pile-on the story session budgets.

### 14.F What the player sees (value 3)

Every surface asks `AlumniRules.At(s, AlumniRules.Reputation)` and shows only the public record:
- the edition's `Record` and `Act`, verbatim (`Simulation/AlumniHistories.cs:246-299`);
- this season's public ceremonies.

It shows every seated fact. That includes the three nomination facts, which shape their holders' trust but no threat term. It never shows a number, a feeling or a present-tense pact.

1. **Houseguest profile.**
   - `ProfileRecords` (`Runtime/Episode/EpisodeDirector.Journal.cs:460-490`) adds `KnownBlocs.ProfileLines(s, actor.id)` after the ceremony line:
     - `"Before this season: " + Record`;
     - each seated fact involving the actor, as its `Act`. A fact that involves the player starts `"Between you, before this season: "`;
     - for a shared alliance, plus one of: `" · both in the house"`, `" · Week N: {first} nominated {first}"`, or `" · {first} has left the house"`.
   - These lines do not count toward `personal`.
   - When the actor shares a seated fact with the player, the empty line at `:488` reads `"No promises, alliances or declarations between you this season."`. Otherwise it is unchanged.
   - The doc at `:455-459` becomes: "Nothing between two other people, except the public record of past games."
2. **The alliances page.**
   - `AllianceRead.Page` is unchanged (`Simulation/AllianceRead.cs:70-73,185`), because `Tests/EditMode/ModeOneSeasonDigests.cs:213` and `Tests/EditMode/OldSaveAcceptanceTests.cs:369` serialize it.
   - A separate public reader, `KnownBlocs.PastGames(s)`, returns one entry per seated shared-alliance fact, live or not: fact id, season, `Act`, both houseguests, and status as above.
   - `RenderNotebookAlliances` (`Runtime/Episode/EpisodeDirector.Alliances.cs:30-53`) calls it and draws a third section after the suspected cards (`:49`) and before the footer (`:51`):
     - the heading `PastGamesHeading = "PAST GAMES"`, named "Past games heading", with the count;
     - a lock note named "Past games note": "The public record from earlier seasons: who played together before this one. It does not say who is working together now.";
     - one card per fact, named `PastGameCardPrefix = "Past game · "` + fact id, with eyebrow `"BIG BROTHER " + season + " · PUBLIC RECORD"`, the `Act` as body, the status line, and faces named `"Past game face · " + name`.
   - The section is omitted when there are no facts.
   - `empty` (`:33`) also requires no past games.
   - The page's contract comment (`:9-18`) adds "and the public record of past games, which the whole house knows".
   - `SuspectedCardPrefix`, the suspected count and `NoSuspectedCopy` are unchanged (`Runtime/Episode/EpisodeHud.Alliances.cs:20`).
3. **Diary "Your record".** For a player on an intact card only, `RecordSummary` (`Runtime/Episode/EpisodeDirector.DiaryRoom.cs:475-487`) gains a fourth row:
   - caption `"Before this season"`;
   - icon `PackArt.KitIconPerson`;
   - value: `Winner`, `Finalist` or `Played`;
   - note: the `Record` and the player's seated facts' `Act`s.
   - The caption is not "Career record", which is a status-message prefix (`Runtime/Episode/EpisodeDirector.Career.cs:109`). A newcomer gets no row.
4. **The house reacting, through existing lines only:**
   - the agenda read "X wants Cody out, with Y" (`Simulation/NpcAgendas.cs:103`);
   - the hunt clause on a listen-in (`Simulation/NpcActKinds.cs:71`);
   - counter-pact whispers (`Simulation/AllianceLeaks.cs:160-161`);
   - invitations.

   No new act kind or recap line.

**Layout:** label boxes at 1.3× their font, captures at Standard and Largest text, and nothing appended to an existing caption.

**As built in S5** (2026-10-31, on `20f665b7`). Where the code asked for it, the build keeps the intent and differs in the letter:
- **Two more readers in `KnownBlocs`,** so the subset pins every word the player sees: `ProfileEmptyLine(s, id)` gives the profile's empty line, "this season" when a seated fact is between the houseguest and the player and the old words otherwise, and `PlayerRecord(s)` gives the diary row's value and note. The old empty line's words move into `KnownBlocs.NothingBetweenYouCopy` unchanged.
- **The status,** as `PastGame.Status`, shared by the profile and the card:
  - a public split comes first, because it can only happen while both are in the house;
  - then a departure: evicted, on the jury or removed. When both have left, the one the record shows leaving first is named: a removal by its own week in `story.removals`, as `SeasonAutopsy` dates one, and an eviction by its power row (`JuryHouseRead.LeftWeek`);
  - the winner and the runner-up are in the house to the end;
  - a status the player made names the player by the card's first name, as the act does ("Week 1: Cody nominated Tyler").
- **The card** (`EpisodeDirector.PastGameCard`):
  - its headline is the two names, as a suspected card's is;
  - its lines are the act, then the status with its first letter capitalised;
  - under each face is the houseguest's status word, or "You" for the player, never a reading;
  - its eyebrow is drawn in brass;
  - `EpisodeHud.PactCard` takes the face prefix as an optional last parameter;
  - `PastGameCardPrefix` and `PastGameFacePrefix` sit beside `SuspectedCardPrefix` in `EpisodeHud`, and `PastGamesHeading` with its name, note and note copy beside the page's other headings in `EpisodeDirector`.
- **Item 4** adds no code. The house's reactions are the existing lines, read with no new term, and their pins stand where they are:
  - the agenda read "X wants Cody out, with Y", in its own words at value 4 in `CounterBlocTests.AnOutsidePactHuntsTheHubFirst`;
  - the hunt a listen-in can hear, as an act in `CounterBlocTests.AnOutsideHeadOfHouseholdPutsABlocMemberUpOverAnEqualOutsider`. Its clause is `NpcActKinds.Clause`'s, pinned where it always was;
  - a grown pact's whisper, in `CounterPactGrowthTests.AGrownPactShedsASourJoinerWhileKeptToItselfAndOtherwiseEndsWhole` and `AllianceLeakTests`;
  - invitations, through the existing offer pass: `CounterPactGrowthTests.AnInvitationNeverTakesAHouseguestsFourPastFour`, and the player taken into an outsiders' pact in `CounterBlocTests.AnOutsidePactNeverHuntsAPactMate`.
- **The diary** in week one's free time opens on its pending tab, because the free time's actions are the player's to decide. The fourth row is on the record tab, "Your record", after the room's three.
- **Tests:**
  - `PastGamesReadTests`, 10 cases, in the subset and in Unity's EditMode assembly;
  - `EpisodePlayModeTests.PastGames`, 4 PlayMode tests in both PlayMode builds:
    - the page, with a suspected card of three faces and one of four, at every text size;
    - the profile at every text size, and the diary's record tab at Standard and Largest text;
    - the caption audit: a Regular season and a season held at 2 show nothing new, and the same season at `Latest` has the same controls by caption;
    - the cast screen's All-Stars season, as Will.
  - Captures, in a batch run: `past-games-suspected-three`, `past-games-suspected-four`, `past-games-record`, `past-games-profile` and `past-games-diary`, each at Standard and with `-largest`.
  - **The CastSelect audit:** no `EpisodePlayModeTests.CastSelect` test reads the profile, the alliances page or the diary, so none changes. Nor does any other PlayMode test that builds an All-Stars season: `OpeningGrounds` reads the arrival line only. The cast-screen test above is the audit's new case.
  - The PlayMode tests are compiled offline, with and without UMA. Their Unity run is the lead's, never beside S4's lab run, and so is the standalone verify walk §14.I asks for: the stress house enrols.
  - `ModeOneSeasonDigests` is `[Explicit]`, so the full subset does not run it, and its 16433 does not count it. It serializes `AllianceRead.Read`, so it is proven by a filtered run of its own: 2 of 2 at the build's commit (its review's run), and again after the review's fixes.
- **The review's fixes** (2026-11-01) add checks, not cases:
  - `PastGamesReadTests`: the private half now holds this season's pacts between the two, one private and one the player was told of, and a past ballot between them, and the status and the profile lines stay the public ones; the page's reader is checked with the Hitmen live again; every reader is checked to write nothing, one call at a time, over a fourth week with a save, a replacement, an eviction and a live block; a removal is dated by its own week, either way round.
  - `EpisodePlayModeTests.PastGames`: the page's foot comes after the last past game; the page draws its empty illustration only at 2, with no past games either; under each face are the name and the status word, or "You" for the player; the Regular season's page is shown to be drawn before its absences are read. Each is to be mutation-checked once in the lead's Unity run: the section moved below the footer, `empty` without its past-games term, and a face note that reads the player's reading.

### 14.G Versioning

**Constants** in `Simulation/AlumniHistories.cs`:
- after `:27`: `Loyalty = 2`, `Reputation = 3`, `CounterBlocs = 4`, `GrowingPacts = 5`;
- `Latest` (`:30`) moves with each slice;
- after `:41`: `public static bool At(EpisodeState s, int version) => On(s) && s.alumniRulesVersion >= version;`;
- `IsDefined` (`:33`) widens on its own;
- the docs at `:12-16`, `:171-175`, `:215` and `:218` now say who reads career reputation and pair risk.

**The writer.** `Simulation/EpisodeEngine.AlumniHistories.cs:49` writes `AlumniRules.Latest`, with its doc at `:14-16` updated. `ShippedRules` changes only in its comment at `:96-100`.

**Holding a value, for tests and the lab.**
- There is no value-taking writer and no internal harness, so nothing needs InternalsVisibleTo (`Tests/EditMode/AlumniHistoriesTests.cs:706-707`).
- A test or the lab builds the shipped season with the public `ApplyFresh`, asserts that it enrolled, and writes `s.alumniRulesVersion = v`.
- This is valid only while the opening is the same at every value from v to `Latest`, which `TheOpeningIsTheSameAtEveryAlumniValue` pins. A later value that adds an opening write must bring back a value-taking writer.

**Validation:** no code change.
- `Simulation/EpisodeValidation.cs:105-109` and `TryValidateAlumni` (`Simulation/EpisodeValidation.Alumni.cs:15-41`) accept 2 to 5 with the same receipts.
- Only the comment at `:101-104` changes.

**Schema:**
- stays 30; there is no field;
- the values are defined at schema 30 by construction, so schema 29's closure is untouched;
- `WebVoteAlumniTerm` is never saved.

**Old saves:**
- every owner save is at 0;
- the alumni-1 fixture replays by rule 1;
- the observers admit any value where `On` holds.
- No recorded digest plays an enrolled season:
  - `FreshSeasonStateDigestTests` strips the histories (`:84-91`);
  - `AllWeekSeasonDigests` uses Regular houses of 6 to 12 (`:221-229`);
  - `BalanceLabGoldens` holds no All-Stars cell.

**The one-time re-pin (S1).**
- Fresh-season assertions change from `Histories` to `Latest`.
- "Undefined value" cases change from the literal 2 to `AlumniRules.Latest + 1`, which is valid in attributes.
- Values 3 to 5 then move no existing pin; the `Latest + 1` pins reach 6 on their own.

**Determinism.**
- Seasons at 0 and 1 are byte-identical.
- A season at k diverges from the same seed at k−1 only through decisions.

### 14.H Balance

**What moves, as expected and not measured.**
- **At 2:**
  - trust between old partners after wrongs, nominations and pacts;
  - the Committee's standing after a split, from +10 to 0;
  - grievances heal through pacts.
- **At 3:**
  - alumni winners and known-pair members read as bigger nomination threats, by up to 9.6 points, and about 3.6 ballot points at default weights;
  - the newcomer is the smallest career threat, so Q4 (the newcomer's nomination rate) is likely to fall and Q3 (season wins) to rise;
  - rumours and pitches are unchanged.
- **At 4:**
  - more and earlier outsider pacts;
  - hunts that stop above zero;
  - earlier exits for known-pair members;
  - outsider pacts may fill the cap;
  - a player on Cody's card is nominated more.
- **At 5** (14.L.H):
  - joins may be rare in the 8-house: a join needs a pact from an earlier week, a free outsider at 25 or more both ways with desire above 25, and both Hitmen in the house and unsplit;
  - a complying trio can carry the vote: an 8-house has four voters from week 2 and three from week 3;
  - births can fall, because a join takes an outsider someone else would have asked later;
  - known-pair members leave earlier; stop condition 2 is the one most likely to fire;
  - grown pacts are brittle once known (they end on any of their 3 or 6 edges); while private they shed instead;
  - the 12-house has a pile-on risk: a story's `AllianceLeave` from a four gives three grudges of 80 at once;
  - Power Shift draws grown pacts first and leaks them.

**The report.** `BalanceLabAlumniReport.LoyaltyReputationBlocsReport`, Explicit and dotnet only, in `Tests/EditMode/BalanceLabAlumniReport.cs`.
- `BalanceLab.Cell` (`Tests/EditMode/BalanceLab.cs:62-75`) gains `int alumniHeld`:
  - 0 means shipped;
  - the house name gains `"/alumni" + alumniHeld` only when held;
  - `SeedHouse` is unchanged, so seeds stay paired.
- `Fresh` (`:217-223`) writes the held value after `ApplyFresh` when the season enrolled.
- `AlumniHistoriesGate` (`Tests/EditMode/BalanceLabAlumniReport.cs:61`) holds its histories arm at 1, so it keeps reproducing H1b's evidence. Its house names gain `/alumni1`.
- **Arms:** 5 (shipped), held 4, 3, 2, 1, and 0.
- **Cells:** sizes 8 and 12 × passive, studier, social newcomer, and passive on Cody's card. 100 seeds each, 4,800 seasons, about 36 minutes on every core.
- **Comparisons:** 2−1, 3−2, 4−3, 5−4, 4−0 and 5−0, as paired differences with 95% intervals.
- **Rows:**
  - Q3, Q4 and weekly competition wins;
  - a known-pair member put up by an NPC Head of Household, per member-week;
  - the first evictee is a known-pair member;
  - a known-pair member is out by week 2;
  - the week the first known-pair member leaves;
  - the public-split rate;
  - the Hitmen final two's end;
  - the Hitmen re-forming a pact;
  - an outsider-only pact formed by week 3;
  - bloc hunts per season, and the longest run against one listener;
  - the longest same-subject rumour run (shows the house's talk is unchanged);
  - goodwill shares at the end;
  - pile-on seasons, as `story.pileOns > 0` (the-house-turns, `Tests/EditMode/StoryPacingTests.cs:101`);
  - pariah-free seasons (Q5, `Tests/EditMode/BalanceLab.cs:125`);
  - one houseguest put up three or more weeks running, as an extra row;
  - pacing and seasons per minute;
  - the pact-watch rows, the must-be-zero rows and the near-zero rule of 14.L.H.
- **Not a gate.** About 30 comparisons at 95% will cross by chance once or twice. Nothing is retuned in place; a retune is a later value.
- **Two stop conditions.** The lead asks the owner once before S6 and records the answer as "delegated call, confirmed by the owner" in either case:
  1. The shipped arm crosses a story-session budget beyond noise where arm 1 meets it. If arm 4 meets it there, growth is the cause, and the owner is offered value 4 (S3b out before S6, `Latest` stays 4). The budgets are: pile-on seasons at 25% or fewer; pariah-free seasons at 80% or more, and the twelve-house's not lower (`Assets/Plans/NPC-AGENCY-PLAN.md:23-26`).
  2. A known-pair member is the first evictee in more than half the seasons of any cell at 4, read at 4 and at 5 separately.
- **The owner's call on the stop conditions** (2026-11-01, asked once, on S4's full run at `20f665b7`):
  - stop condition 1 does not fire, in either reading;
  - stop condition 2 fires in one cell of the eight: the social newcomer at 12, where a known-pair member is the first evictee in 57% [47.2%, 66.3%] (57/100) at 4 and at 5 alike (55% at 3, 41% at 0 to 2). Reputation (value 3) drives it, not growth;
  - the lead's recommendation, taken: keep value 5 as built, and retune nothing before S6. The effect is the owner's own design ("the other players in the house might ally against them for numbers"), and it fires in one cell of eight, by a margin inside its interval;
  - the lever for a later value, built only on the owner's word after play, is the known-pair weight in the nomination threat and the vote: value 3's career 4/3/2 and pair risk 8/4;
  - growth is kept, so S6 freezes 5 and `Latest` stays 5.

  Delegated call, confirmed by the owner: "Yes your judgment on questions and proceed." (2026-11-01).
- **When it runs:** alone, as a background job, never beside a Unity run.
- NPC-to-NPC behaviour is proved by fixtures (14.I), never by walks.

**As built in S4** (2026-10-31, on `6a86a040`). The report and its watch are built; the full run is the lead's, alone, and its results go to the owner, not into this plan (S6 records the stop-condition reading that the owner's call rests on, above). Where the code asked for it, the build keeps the intent and differs in the letter:
- **The cell and the gate were S1's.** `BalanceLab.Cell.alumniHeld`, its "/alumni" house name and `Fresh`'s hold were built in S1, and `AlumniHistoriesGate` already holds its arm at 1 with house names ending "/alumni1". S4 changes neither. `BalanceLabAlumniReportTests` pins both, and a scratch run of the gate at two seeds a cell (48 seasons) wrote the same report at `2fdcf299` (the last commit before S1) and with S4, every one of its 162 table rows alike; only the timing line and the sentence S1 reworded differ.
- **Arm 0 is the season without the histories** (the lab's `alumniOff`, H1b's control). `alumniHeld` 0 means shipped, and a hold of 0 after enrolment would keep the opening's receipts and final two.
- **The shipped arm is the unheld cell** while `Latest` is 5. A test pins `ReportArms[0] == Latest`, so a later value moves the report knowingly.
- **Settable without code:** `BALANCE_ALUMNI_SEEDS` (100, shared with the gate), `BALANCE_ALUMNI_THREADS` (every processor), `BALANCE_ALUMNI_OUT` (the test's `balance/` folder) and `BALANCE_COMMIT` (else read from `.git`). The report writes `alumni-14h-report.md` for the owner, `alumni-14h-report.json`, and `alumni-14h-seasons.csv` (one line a season). It prints the command, the commit and the elapsed time.
- **The known pairs** are read from edition 1 by the cast at every arm, 0 included, and are live as `KnownBlocs.LivePairs` reads them. Wherever the rules read them (the live pairs from 3, the bound at 5) the watch checks that it agrees, as two more must-be-zero rows.
- **`PactWatch`** (`BalanceLab.PactWatch`; its `PactRecord` is `SeasonRun.pactWatch`) reads each accepted command's state before and after. A command can carry a whole stretch of the week, so each check counts only what must have held at the moment: a bound read before a join, and a pact or an agreement standing on both sides of the command. A violation that begins and ends inside one command is missed, never invented. The record is out of the hashed rows and carried by a part (`BalanceLabParts.Stamp` v7). A test plays one season with the watch and without it, to the same row.
- **The watch is guarded** (`PactWatch.Guard`, S4's review). A read that throws ends the watch for that season and keeps its record with the error. The season plays on and keeps every row that is not the watch's, and the report counts it as the must-be-zero row "the watch threw". The watch runs in every lab season, as built; the guard means it can never turn a good season into an error.
- **Hunts and rumours are read from the ledger entries they write** ("What {listener} heard from {hunter} about {target}", "Something {listener} heard about {subject}"), not from the week's acts. The eviction window's last beats fire in the command that turns the week, which clears its acts. A hunt with the player as listener writes no entry and is not counted.
- **Rows the plan left open, as built:**
  - a known-pair member put up "per member-week": at each NPC Head of Household's nominations, the live-pair members in the house other than the Head of Household, and the weeks they were nominated, saved or named the replacement. It is a pooled ratio (S4's review): the cell's put-ups over its member-weeks, each season one cluster, with the season-clustered interval, and its paired difference from each pair's linearised terms. A mean of the seasons' own ratios would weigh a season of two member-weeks like one of sixteen, and early exits differ by arm. "Joins per bloc-live week" is pooled the same way, and the CSV keeps both rows' parts;
  - a joiner is a member the watch saw join. A founder taken out of a grown pact that stands on is counted apart, as "founders out of a grown pact", never as a joiner;
  - "carried" by an outsider bloc: three or more of one outsider pact voted, all for the evictee, and without their votes the evictee would not have led. An outsider pact is a houseguests' own with no player and nobody in a live pair;
  - goodwill at 1 is the breach rule's all or nothing; at 0 there is no receipt;
  - the pact-weeks held back are read at every arm, against the bound the pact would have had; below 5 nothing grows anyway. The causes are whisper, listen-in, story (Power Shift named, other arcs apart), a member left (a shed or a story's leave), and other;
  - Power Shift cast on a grown pact is counted, as L.F asks.
- **Must-be-zero rows beyond L.H's list:** a join below 5, more than one join a pact a week, the two consistency checks, a season whose alumni rules value moved, a season not at its arm's value (so a hold that stopped reaching the season is a bug, not two identical arms), the watch threw, and a season the lab could not play. Every nonzero row is a bug line at the top of the report, with its arm.
- **Stop condition 1 has two readings, both printed** (S4's review), with the cells, arms and numbers, in the table, the owner's summary lines and the JSON, so the lead can choose when asking the owner:
  - **as built:** a budget is crossed "beyond noise" when the shipped arm's 95% Wilson interval lies wholly past the line;
  - **paired:** the shipped arm's rate is past the line and the paired 5 - 1 difference is beyond noise the wrong way (McNemar p under 0.05), the reading 14.H and H1b's gate use for every other row.

  In both, arm 1, or arm 4, "meets" the budget when its rate is on the budget's side. The twelve-house's pariah-free budget is its 45%.
- **The comparisons** run on every row but the timed one, each by its kind (S4's review):
  - a rate's paired difference is decided by the lab's McNemar test (exact under 25 discordant pairs), beyond noise at p under 0.05, and shown with Newcombe's hybrid score interval and its b:c and p. The normal interval of 0 and ±1 differences overstates a row few seasons hold;
  - a mean's uses the normal interval of the paired differences, as H1b's gate does;
  - a pooled ratio's uses the season-clustered interval.

  A comparison counts only where two pairs or more were read.
- **A primary family, reported first.** 14.H's headline and known-pair rows are the primary family: Q3, Q4 and weekly wins, the four known-pair member rows, the public split, pile-on seasons and pariah-free seasons. The report lists their crossings first, with their own expected-by-chance count, and every other row after. This departs from 14.H's "about 30 comparisons": six comparisons in eight cells over ten primary rows make 480 (about 24 by chance), and every row together makes about 2,000 (about 100).
- **How many seed pairs play differently.** For each cell and comparison, the report counts the pairs whose season rows differ (`BalanceLab.Row`) and the pairs whose final relationship scores differ (`PactRecord.scoresHash`). A zero difference then reads either as no change in play or as a change these rows do not show. Arms 1 and 2 play alike in most seasons yet `SplitInPublic` moves final scores, and arms 4 and 5 are alike in every season without a join.
- **The watch's tables are cell by cell** (14.L.H: "per cell"), then every player of a size pooled: the largest outsider pact, the pact-weeks held back by cause, how grown pacts end, the Hitmen's final two and the pile-on targets. All five reach the JSON (`pactTables`).
- **The full run is made from a clean tree,** or from a git archive of the commit with `BALANCE_COMMIT` set. The report labels itself with HEAD and cannot see changes not yet committed. Without a `.git` it says the tree's state is unknown.
- **Proof:**
  - the subset, with `BalanceLabAlumniReportTests` (dotnet only, as the lab is). Its fixtures prove the watch counts a birth, a join, a shed, founders and joiners out, the known-pair rows (member-weeks and put-ups, the public split, an eviction an outsider bloc carried, the Hitmen re-forming), the guard, and every one of its must-be-zero rows. A report built from synthetic seasons proves the bug lines, the near-zero rule, both stop conditions in both readings, the pooled ratios, McNemar's call and the play-differs counts;
  - the mutations of the cell, the watch and the report: S4's 20 and the review's, each caught;
  - a smoke run of the report at two seeds a cell on one core: every row computed and every arm paired by seed. The smoke's values (two seeds a cell, no join in any season) are not results.

  Not run: the full report.

### 14.I Tests

Every new test uses only public members, so the new files compile in Unity's EditMode assembly and in the dotnet subset. Each file is added to `Tools/SimulationTests/SimulationTests.csproj` near `:293`, with a hand-written meta.

`AlumniHistoriesTests.AllStars` (`:214`) gains a value: `AllStars(size, seed, player = null, value = AlumniRules.Latest)`. It builds the shipped season, asserts it enrolled, and writes the value. Every behaviour test names its value (S1 `Loyalty`, S2 `Reputation`, S3 `CounterBlocs`); the default is for the writer, replay and validation cases only.

- **`AlumniLoyaltyTests` (S1):**
  - `AWeekOnePastGameCountsInFull`: 33 at 1 and at 2;
  - `ABetrayalEndsInheritedGoodwillAndNoRepairUndoesIt`: −45, −31, −30, then +30;
  - `ALesserWrongErodesGoodwillInProportion`: −15, −20, −22, −22.5; B.3's values;
  - `ANominationErodesGoodwillFromThePublicRecordAndReturnsAsItAges`: the first nomination, a nominee then saved, the replacement, and week-by-week aging. The final eviction's row never counts;
  - `ABallotNeverErodesGoodwill`: an ally's vote against and a flip (grudges of 40 and 48) leave the share unchanged;
  - `AnInHousePactShieldsInheritedGoodwill`: a nomination plus a pact gives 1/3;
  - `RepairIsCappedAtOneSpan`: four words, then a nomination, gives 1/3;
  - `AnOldGrievanceHealsByPlayAndNotByTime`;
  - `SocialTrafficNeitherErodesNorRepairsAPastGame`;
  - `OnlyTheHoldersEdgeCounts`;
  - `AnEdgeWithNoReceiptReadsExactlyAsBefore`: bitwise, at 1 and 2;
  - `TheShareDrawsNothingAndWritesNothing`: `randomState`, `nextSequence` and the state digest unchanged;
  - `APublicSplitWritesTheStandingOffOnceAndNeverOnThePlayersEdge`: Cody as Head of Household through `EpisodeEngine.Apply`; first nomination, replacement, the reverse nomination later (no second write), the player on Tyler's card, nothing at 1;
  - `TheOpeningIsTheSameAtEveryAlumniValue`: the writer writes `Latest`, and a hold is the field alone;
  - `AtZeroAndOneEveryNewReaderIsSilent`.
- **`KnownBlocsTests` (S2):**
  - `OnlySeatedSharedAlliancesAreKnownPairs`;
  - `LivePairsAgreeWithSeatedPairs`: renamed and ambiguous cards;
  - `AKnownPairEndsWhenAMemberLeavesOrOneNominatesTheOther`: this week's block included; the final eviction's row is not a nomination;
  - `APrivateSplitIsNotPublic`: a broken final two, scores, ballots, a pact's end;
  - `PairRiskCountsToOutsidersInsideTheCapAndStepsAsideForAVisiblePact`: D.3's table, as `TotalWithPastGames − Total`;
  - `CareerReputationIsPublicAndANewcomerHasNone`: 4/3/2 and the clamp at 15;
  - `TheHouseTalkReadsNoPastGame`: `Total`, `RankedTargets`, the pitch target, the rumour subject and `Grudges.ThreatScaled` are equal at 1 and at 3;
  - `TheNominationThreatTermCarriesThePastGames`: the 9.6 bound;
  - `TheVoteWeighsTheSameAlumniThreatAsTheNominations`: the clamp difference included;
  - `BelowReputationNoThreatReadsTheEdition`.
- **`CounterBlocTests` (S3).** Fixtures in the style of `Tests/EditMode/NpcAgencyTests.cs:17-41`:
  - `OutsidersWantEachOtherMoreForEveryKnownPairTheyFace`: +7.5, +15; 0 for members and their partners;
  - `ACounterPactFormsAtFourWhereNoneFormedAtThree`;
  - `TheScoreFloorAndThePactCapStillHold`;
  - `AtTheFloorAnEightHouseCounterPactStillNeedsTemperament`;
  - `AnOutsidePactHuntsTheHubFirst`: in the hunter's reading, not the listener's;
  - `AnOutsidePactNeverHuntsAPactMate` (added at the S3 review): the player in the pact on Cody's card, a trio holding Cody, the hunter's other pact and the listener's visible one; a private pact of the listener's and an ended one do not count;
  - `TheBlocHuntNeverTakesAListenerBelowZero`: repeated pursuits; the target's fall is bounded by the listener's starting warmth; `Hold` resumes;
  - `TheBlocRouteNeverHuntsWithThePlayerAsListener`;
  - `TheOrdinaryCommonThreatStillComesFirst`;
  - `TheCounterBlocEndsWithTheBloc`;
  - `AnOutsideHeadOfHouseholdPutsABlocMemberUpOverAnEqualOutsider`.
- **`CounterPactGrowthTests` (S3b):** 14.L.I's twenty fixtures; `TheOrdinaryCommonThreatStillComesFirst` and the pair-shape cases held at `CounterBlocs`, and `CounterBlocTests.TheScoreFloorAndThePactCapStillHoldWhilePactsGrow`, their sibling at 5 with the size clause.
- **`BalanceLabAlumniReportTests` (S4, dotnet only, as the lab is):**
  - `AHeldArmPlaysItsWholeSeasonAtItsValueAndOnlyAnEnrolledSeasonIsHeld`: a season held at 4 is at 4 from its first command to its finale; a Regular house is never held;
  - `AnUnheldCellKeepsItsNameAndSeedsAndTheWatchNeverChangesItsSeason`: the name and seeds it always had, every arm on them, and the same row with the watch as without it;
  - `TheReportsArmsPairBySeedAndEachEnrolsAtItsOwnValue`: 48 cells, each at its arm, the known pairs the same at every arm, and the gate's arm at 1;
  - `TheStopConditionsAndTheNearZeroRuleReadAsThePlanWritesThem`;
  - `ThePactWatchCountsABirthAJoinAndAShed`: by the rules themselves, and a part carries the record;
  - `ThePactWatchCountsEveryMustBeZeroRowItIsShown`: each row from a scripted state (the cap, one each, a story's pact and the two consistency checks among them), the hunts read from their ledger entries, and the guard;
  - `ThePactWatchReadsTheKnownPairRowsAndWhoLeftAGrownPactFromTheRecord` (S4's review): member-weeks and put-ups, the public split, the scores' hash, the Hitmen re-forming, a carried eviction and a landslide, founders and joiners out, and the autopsy's week rows;
  - `TheRatioAndRateRowsAndStopOnesPairedReadingReadAsTheirLabelsSay` (S4's review): the pooled ratio and its paired difference, McNemar's call on a rare rate, Newcombe's interval, and stop condition 1's paired reading;
  - `TheReportPutsItsBugLinesTheNearZeroRuleAndBothStopConditionsWhereThePlanSays` (S4's review): `AlumniReport.Build` on synthetic seasons, so the text's first lines, both stop conditions in both readings, every comparison, the must-be-zero rows, the pooled ratio, the rare rate, the play-differs counts, the tables and the CSV are pinned.
- **`PastGamesReadTests` (S5):**
  - the lines verbatim;
  - the statuses;
  - the player's own wording and the "this season" empty line;
  - nothing below 3;
  - `JsonConvert.SerializeObject(AllianceRead.Read(s))` byte-identical at 3.

**Existing tests that change.**
- **`AlumniHistoriesTests`:**
  - `ABreachThisSeasonEnds…` (`:544-599`) is held at 1 through `AllStars(…, value: 1)` and renamed `AtAlumniRulesOneABreach…`. Its values are unchanged;
  - `:235`, `:384`, `:633` and `:741` assert `Latest`;
  - `EveryAlumnusHasACareerRecordThatNoDecisionReadsYet` (`:149-170`) is renamed `…AndTheNominationThreatReadsIt`, with `:166` fixed;
  - `:514` is extended to the new readers.
- **`ShippedRulesTests`:** `:84`, `:189`, `:332`, `:341` assert `Latest`.
- **`PooledSchema30ShapeTests`:** `:73` uses `Latest + 1`; `:84` asserts `Latest`.
- **`LegacyDigestSchema30ObserverTests`:** `:122` uses `Latest + 1`; `:144` and `:277` assert `Latest`.
- **EditMode only:**
  - `FreshSeasonStateDigestTests:87` asserts `Latest`;
  - `AlumniHistoriesSaveTests:56,63` assert `Latest`;
  - `PersistenceV30MigrationTests:138` uses `Latest + 1`, with its comment at `:129-130`.
- **Unchanged:**
  - `OldSaveAcceptanceTests` (the alumni-1 fixture);
  - `ModeOneSeasonDigests`;
  - `TheHistoriesDrawNothingAndMintOnlyTheirOwnSequences`;
  - the Regular `ThreatAssessmentTests`, `NpcAllianceTests` and `RelationshipLedgerTests`;
  - `BalanceLabGoldens`.
- **PlayMode (S5):**
  - captions;
  - captures at Standard and Largest text;
  - captures of three-face and four-face suspected cards (14.L.J);
  - an audit of `EpisodePlayModeTests.CastSelect`;
  - the standalone verify walk, because the stress house enrols.
- **New in S6:** the fixture `s30-mode2-allstars-alumni5-w3-campaign`, recorded from a seed whose week-3 state holds a grown houseguests' pact, with a fifth case asserting it; a 12-house if no 8-house seed in the first 200 has one (`…alumni4…` with four cases if growth is withdrawn). Built 2026-11-01 at 5, growth being kept (14.J's as-built note for S6).
- **Floors:** `Tools/baseline.txt` rises in each slice's commit.

### 14.J Slices and landing

| # | Slice | Files | Proof |
|---|---|---|---|
| S1 | Loyalty, value 2, and the re-pin | `Simulation/AlumniHistories.cs:12-41`; new `Simulation/AlumniLoyalty.cs` + meta; `Simulation/RelationshipLedger.cs:85-101`; `Simulation/EpisodeEngine.cs:949`; `Simulation/EpisodeEngine.AlumniHistories.cs:14-16,49`; comments at `Simulation/ShippedRules.cs:96-100` and `Simulation/EpisodeValidation.cs:101-104`; `Tests/EditMode/BalanceLab.cs:62-75,217-223`; the tests above | Subset; the EditMode-only pins through the Mono harness |
| S2 | Reputation and known pairs, value 3 | `Simulation/AlumniHistories.cs`; new `Simulation/AlumniKnownBlocs.cs` + meta; `Simulation/ThreatAssessment.cs:51-62,125-128,149-177`; `Simulation/EpisodeEngine.Agency.cs:95-101`; `Simulation/WebEvictionVoting.cs:128-131,277,404-421,606` | Subset |
| S3 | Counter-blocs, value 4 | `Simulation/AlumniHistories.cs`; `Simulation/AlumniKnownBlocs.cs`; `Simulation/NpcAlliances.cs:78-85`; `Simulation/NpcAgendas.cs:54-59` | Subset (fixtures) |
| S3b | Growing pacts, value 5 (14.L.J) | `Simulation/AlumniHistories.cs`; `Simulation/AlumniKnownBlocs.cs`; `Simulation/NpcAlliances.cs`; `Simulation/NpcDeals.cs:143-150`; `Simulation/NpcSocialActions.cs:155,206-210`; `Simulation/EpisodeEngine.Strategy.cs:249`; `Tests/EditMode/CounterPactGrowthTests.cs` + meta; four test helpers made internal | Subset (fixtures); the EditMode-only pins through the Mono harness |
| S4 | Measurement and the record | `Tests/EditMode/BalanceLabAlumniReport.cs`; `Tests/EditMode/BalanceLab.cs` (the pact watch); `Tests/EditMode/BalanceLabParts.cs`; new `Tests/EditMode/BalanceLabAlumniReportTests.cs` + meta (dotnet only); this plan's edits (14.A) | Subset (fixtures); a smoke run; the lab, alone; the report goes to the owner. Built 2026-10-31, with its review's fixes (14.H's as-built note) |
| S5 | Player-facing (after S2) | `Simulation/AlumniKnownBlocs.cs` (readers); `Runtime/Episode/EpisodeDirector.Journal.cs:455-490`; `Runtime/Episode/EpisodeDirector.Alliances.cs:9-53`; `Runtime/Episode/EpisodeHud.Alliances.cs`; `Runtime/Episode/EpisodeDirector.DiaryRoom.cs:475-487` | Readers in the subset; PlayMode captures; caption audit. Built 2026-10-31 (14.F's as-built note); the PlayMode tests are compiled offline, and their Unity run is the lead's |
| S6 | Freeze the latest value kept: 5, or 4 if growth is withdrawn | The alumni-5 fixture (alumni-4 if growth is withdrawn), its manifest row, the floors | `OldSaveAcceptanceTests`. Built 2026-11-01 at 5 (the as-built note below), proved under Unity's Mono offline; its Unity run is the lead's |

- **Order:** S1, S2, S3, S3b, S4. S5 needs S2. S4's all-core lab run and S5's Unity runs never overlap; each waits for the other to exit. S6 comes last, after the owner has read S4's report.
- **Landing:**
  - S1 to S5 land on `claude/all-star-histories` only. They are not merged to the consolidation line or main, or synced to the play copy, until S6.
  - So no season is ever saved at 2 to 5 outside tests, which pin each value through `AllStars(…, value)`.
  - Until S6, a change to 2 to 5 is an edit recorded here.

**As built in S6** (2026-11-01, on `51dd14a5`). S6 changes no Simulation, Runtime or Persistence file: value 5 is frozen as S3b built it and S4's and S5's reviews left it.
- **The owner's call** (14.H): growth is kept, so S6 freezes 5 and `Latest` stays 5. Nothing is retuned before S6. That covers 14.L.H's near-zero rule too, which S4's report printed for the 8-house cells: its lever (the recruit's bar toward its sponsor) waits for the owner's word, like 14.H's. S4's full run had every must-be-zero row at zero, so there was no bug to fix before S6.
- **The fixture**, `s30-mode2-allstars-alumni5-w3-campaign`, was written and recorded by `51dd14a5` itself, by OLD-SAVE-ACCEPTANCE 2.6's route (its new 2.7):
  - `Tools/OldSaves` was built from a `git archive` extract of that commit's Simulation and Runtime/Persistence;
  - `generate 3387 12 director-allstars 3 Campaign` ran twice, to the same state (only `savedAt` differs), and `record` ran twice, to the same script and digests;
  - the season has 112 commands to its end, season digest `2321f173f76d7c97`, and Tyler wins.
- **The seed.** A scratch scan ran the recorder's own `generate` from 3002, the All-Stars fixtures' series (the alumni-1 cell's 3004 was found the same way):
  - no 8-house seed from 3002 to 3201 holds a houseguests' pact past two at its week-3 campaign, so the cell is a 12-house;
  - no 12-house seed from 3002 to 3201 that could be saved holds one (3146 could not, OLD-SAVE-ACCEPTANCE F9, so 199 of the 200 were read), and 3387 is the first after them;
  - its pact `alliance-npc-203` was founded in week 2 and took its third member in week 3. Every member is in the house and in no live pair, and its fact is private and known to exactly the three. Two known pairs are live, so the bound is 4.
- **The fifth case**, `OldSaveAcceptanceTests.TheAlumniFiveCellIsSavedWithAHouseguestsPactGrownPastTwoAndKeptToItself`, holds the file to that state through the store: the two live pairs and the bound of 4, the pact and its private fact, and the recruit's week-3 "was joined" rows beside both founders, both ways (the pairs, the bound and the rows asserted exactly since S6's review). The four per-fixture cases load and replay it, read it with every reader, pin its seventeen holds and recover it from the backup. The manifest test counts it as the 57th cell, `…/dialogue0/alumni5/allstars`. Its `holdsDigest` is `37f1e67f5dea9dfe`.
- **The ballot sentinel, fixed in the test.** The fixture's reader case failed in 18 of its states. In the finalist read's lean "Danielle Reyes · Vanessa · voted to evict them" (a ballot the player knows, from week 7's unanimous count), `BallotPrivacyTests.NamesTheBallot` found the "Dan" in Danielle's name and took the line for her hidden week-3 ballot against Dan Gheesling. A houseguest nominee's first name now has to stand as a word of its own; the player is still found as "you" anywhere, "your" included (S6's review kept that, since a ballot against the player may say "your"). The check is only narrower, and `BallotPrivacyTests.TheSentinelNamesTheNomineeOnlyAsAWordOfItsOwn` pins it (OLD-SAVE-ACCEPTANCE F8). No reader changed. The PlayMode sentinel's copy is unchanged.
- **Floors:** EditMode 9463 to 9469: `OldSaveAcceptanceTests` 261 to 266, and the sentinel's pin. The subset 16433 to 16434: the pin. `OldSaveAcceptanceTests` is EditMode only.
- **From S6, values 2 to 5 are frozen.** A change to any of them is a later value, and the next free value is 6, AS7's (§10); since 2026-11-03 6 is the titled names' and AS7's is 7. S6 merges and syncs nothing. Landing on the consolidation line and main, and syncing the play copy, are the lead's.
- **One code comment goes stale at S6.** The `AlumniRules` summary (`Simulation/AlumniHistories.cs:23-25`) still says value 5 "is released if the owner withdraws growth before §14's freeze (S6)" and that AS7 takes "the next free value after S6". S6 leaves it, because it changes no Simulation file and `51dd14a5` stays the build that wrote and froze the fixture. AS7's value-6 commit, which adds 6 in that block, rewords it to say that 5 was kept and frozen at S6 (2026-11-01) and that AS7 is 6 (14.L.K's "Tell AS7").
- **Found while searching:** the 12-house at 3146 cannot be saved. Under Mono the store refuses its own save on the read-back (OLD-SAVE-ACCEPTANCE F9). The writer is right: Mono writes the double's shortest text, and its parse of that text comes back one bit off, so any load of such a text is refused under Mono too. This is for the lead, not S6.
- **Proof, offline only:**
  - the offline compile without UMA: every assembly, no error, and no warning in S6's two test files;
  - the Lister under Unity's Mono counted 9469 EditMode cases;
  - the Mono harness ran `OldSaveAcceptanceTests` 266 of 266. Before the sentinel's fix it was 265, the alumni-5 reader case failing;
  - `BallotPrivacyTests` ran 8 of 8. The classes that read the whole fixture folder all passed: `FrozenEpisodeV29IntegrationTests` 130, `PersistenceV29MigrationTests` 40, `PersistenceV30MigrationTests` 32, `FrozenEpisodeV28IntegrationTests` 23 and `AlumniHistoriesSaveTests` 4;
  - one mutation, the sentinel's old substring check, made the pin fail on its own assertion;
  - the Frozen29 compile gate passed;
  - the dotnet subset ran 16434 of 16434.

  Not run: the Unity EditMode run (the lead's) and the UMA compile (no PlayMode or UMA file changed).
- **S6's review** (2026-11-01: two reviews, every finding minor, all taken; still no Simulation, Runtime or Persistence file, and the committed fixture and manifest untouched):
  - the fifth case asserts the two live pairs and the bound of 4 exactly, and the recruit's week-3 "was joined" rows beside both founders, both ways, with none between the founders. These are checks, not cases, so the floors do not move;
  - the sentinel's whole-word match is a houseguest nominee's only. The player is "you" anywhere again, as before S6, and the pin's last check says so;
  - OLD-SAVE-ACCEPTANCE F9 now puts the fault on Mono's read, not its writer, so loads are refused too, and lists it in its section 7. The seed claim counts the 199 seeds that could be saved. 2.7's size line and F8's point on `BallotPrivacyTests`' own walk are brought up to date, and so are the notes on the four per-fixture cases and the stale `AlumniRules` comment;
  - proof, offline only: the no-UMA compile (no error, no warning in the two test files); the Lister, 9469 again; the ballot probe on the alumni-5 fixture, 0 of 73 states failing; the Mono harness, `BallotPrivacyTests` 8 of 8 and `OldSaveAcceptanceTests` 266 of 266; two mutations, each caught by the pin on its own assertion (a houseguest nominee back to a substring; the player as a whole word); the Frozen29 gate; the dotnet subset, 16434 of 16434.

### 14.K Defaults taken, and the owner's answer

**Defaults:**
- **Values and enrolment:**
  - three values for the direction, and a fourth, 5, for the owner's answer on outsider pacts (14.L);
  - fresh seasons at `Latest`;
  - a hold is a field write after enrolment;
  - enrolment by the cast (D-H2, confirmed by the lead);
  - section 9's gate and D-H8 superseded for 2 to 5;
  - no opening pact.
- **Loyalty:**
  - `LoyaltySpan` 30, `BetrayalLine` 30, `RepairWeight` 0.5, `NominationErosion` 35, aging 1 a week;
  - erosion is the ledger's permanent wrongs plus public put-ups. It never reads ballots or grudges;
  - repair is capped at one span;
  - a betrayal is not repaired; a grievance heals through play; no neglect fade;
  - the standing is written off once at a public split, never on the player's edge.
- **Known pairs:**
  - a public split means one put the other up. While both are in, the Hitmen do not split in public, because of their final two;
  - only shared alliances count;
  - pairs are counted as pairs.
- **Threat:**
  - career 4/3/2 and pair risk 8/4;
  - in the nomination threat and the vote only, never in `Total`.
- **Counter-blocs:**
  - 7.5 per counted pair, capped at 2;
  - the hunt stops above zero;
  - never with the player as listener;
  - `CommonThreat` first;
  - target agreements held back.
- **Growing pacts (value 5, 14.L.K):**
  - value 5, removable before S6; kept by the owner's call and frozen at S6 (14.H, 14.J);
  - pacts grow in place, only while private to their members, from an earlier week, with every member in the house and no target agreement among them (the last two added in S3b's review), and a pact that has lost a member never grows again;
  - only houseguests' own outsider pacts grow;
  - the bound is min(4, live pairs + 2);
  - one change a pact a week;
  - the three-tier bar, with the members' line at −5;
  - shed while private, whole once known;
  - pact-mates never a common threat;
  - no log line;
  - the invitation skip for houseguests' fours;
  - no one-at-a-time limit (it is measured);
  - the near-zero rule in 14.L.H.
- **Presentation:**
  - from value 3;
  - "PAST GAMES", "Before this season", "Past game · ";
  - `AllianceRead.Page` unchanged.
- **Edition 1 holds no alliance of three.** The Derrick–Cody–Tyler chain counts as two pairs. A sourced trio needs new research under 3.1. From 5, an outsider pact may hold three or four; it is never called a trio of the edition.
- **The report** is not a gate. It has two stop conditions. The owner's call on them (2026-11-01, 14.H): keep 5 as built and retune nothing; delegated call, confirmed by the owner.

**The question put to the owner.** Bigger counter-alliances. The owner's example has the house allying "for numbers" against a known three. NPC pacts here are pairs, with one NPC-only pact per three NPCs and each NPC in one at most. These are the story session's limits (`Assets/Plans/NPC-AGENCY-PLAN.md:27-28`; `Simulation/NpcAlliances.cs:88-99,258-272`). So the house answers with several outsider pairs, their hunts, and the player's own pacts, which can grow, but never with one NPC alliance of three or more. Should outsider pacts be allowed to grow past two while a known bloc stands? That changes a story-session limit, so it needs the owner's call. Default: no, pairs only.

**The owner's answer (2026-10-29):** "Yes, let outsider pacts grow past two" (14.L). It is the owner's decision, not a delegated one, and it overrides the story limit "NPC-only pacts are pairs" for that case only. No question is open.

### 14.L Counter-pacts grow past two (the owner's call, 2026-10-29)

Every line number below was checked read-only against the committed tree at `4ab8c054`. Paths follow §14's convention: `Simulation/`, `Runtime/` and `Tests/EditMode/` sit under `Assets/Gamesim/`. Nothing was compiled or run.

**As built in S3b** (2026-10-31, on `eb68acd1`). Where the code had moved, the build keeps the intent and differs in the letter:
- `Dissolve` starts at `Simulation/NpcAlliances.cs:313` and its `var members` is at `:318` (L.F says `:304` and `:309`); the shed call sits before `var members`, as specified. `TakeOutOfPact` is `internal static` at `Simulation/EpisodeEngine.GrowPacts.cs:270`. In `Simulation/AlumniHistories.cs`, `CounterBlocs` was at `:61` and `Latest` at `:68`.
- `ShedSourJoiners` returns at once for a pact of two members or fewer: an early-out with the same behaviour, so a pair reads nothing more at 5.
- `CommonThreat` is a block body, so `grown` is read once, first.
- The weekly pass's `Settle` and the beat call `TryJoin` only after `TryPropose` has failed, as D.3 sets out; the beat's own doc names the join.
- The comments that named 4 as `Latest` (`Simulation/EpisodeEngine.AlumniHistories.cs:16`, `Simulation/EpisodeValidation.cs:103`, `Simulation/ShippedRules.cs:100`) now name 5.
- Fixture 15 reaches the engine's private `AllyThroughInvitation` by reflection, as `GrowAndManageAlliancesTests` does; every other member the fixtures use is public, or one of the four test helpers made `internal`. Fixture 12's listen-in is the knower a listen-in writes, and its whispered and public facts are written as a leak writes them, without `Knowledge.MakeKnown`'s hearing checks.
- `TheScoreFloorAndThePactCapStillHold`'s sibling at 5 is `CounterBlocTests.TheScoreFloorAndThePactCapStillHoldWhilePactsGrow`: the floor, then a house that loves everybody, four weeks of the rule's pass, every pact within its bound, a pact with a pair's member still a pair, and at least one outsider pact grown.
- S1 to S3's behaviour tests already named their values through their files' own `AllStars` helpers (`Loyalty`, `Reputation`, `CounterBlocs`). The one call of `AlumniHistoriesTests.AllStars` among them without a value is `CounterBlocTests`' "fresh All-Star seasons start at 4 or later", which holds at 5. Every `AlumniRules.Latest` pin in `Tests/EditMode` follows `Latest` (the writer, the fresh-season pins and the `Latest + 1` refusals), and no pin names 4: the literal values tests write are 0 and 1, for seasons with no edition.
- The report's arms and `PactWatch` (L.H) are S4's: at `eb68acd1` `BalanceLabAlumniReport` holds only H1b's `AlumniHistoriesGate`, whose histories arm is held at 1, so `Latest` moving to 5 does not reach it, and it is compiled out of the editor's assemblies.

**Found in S3b's review** (2026-10-31, on `879350d1`), and fixed in its fix commit:
- **A pact regrew after a member left the house.** The departed member stays on the roster and a knower, so rule 7 still held, and every jury reader matched them with the later recruit (25 loyalty, "We were in this together", "shared an alliance" on the finalists' read). Rule 7 now asks that every member be in the house, and L.C's bullet says a pact that has lost a member never grows again. The design's "may recruit again, up to the bound" is withdrawn; with it, the bound's and the receipts' "in the house" readings cannot differ from the roster's for a pact that grows.
- **A target agreement struck before a join outlived it.** Rule 8 is new: no join while an active target agreement has its proposer, recipient and target all among the pact-to-be.
- **A shed joiner at the jury** reads 0 toward the founders, C5's reading of a member taken out; recorded in L.F and pinned.
- **`CommitmentReaderScanTests`** names the new raw read (`TargetsWithin`) as a raw kind, target.
- **Tests added:** fixture 15 takes the player into a four with one member gone (three in the house); fixture 16 pins the shed joiner's jury reading; fixture 18 shows a birth comes first on the beat, the weekly pass and the rule alone; fixture 20 shows a threat from outside the pact is still hunted, an ended pact shields nobody, and a pact-mate of one is still a threat to that one and an outsider; fixture 21, `APactThatHasLostAMemberNeverGrowsAgain`, covers a founder at the jury and a four's member evicted, and the shed reading only the joiners in the house; fixture 22, `NoJoinWhileTwoOfThePactToBeHaveAgreedToTargetTheThird`, covers the founders' agreement against the recruit (the join waits, and goes ahead once it has run out) and the recruit's agreement with a member against another member.

**The question 14.K put to the owner:** "Should outsider pacts be allowed to grow past two while a known bloc stands? Default: no, pairs only."

**The owner's answer, verbatim:** "Yes, let outsider pacts grow past two". **Decided by the owner, 2026-10-29.**

**What it changes:**
- The houseguests' own pacts are born as pairs (`Simulation/NpcAlliances.cs:258-272`). That shape is the story session's limit (`Assets/Plans/NPC-AGENCY-PLAN.md:27-28`).
- The answer lifts it for outsider pacts, in alumni seasons only.
- Two limits stand: the cap of one NPC-only pact per three NPCs, and one such pact each (`Simulation/NpcAlliances.cs:88-99`).

**One principle runs through the whole subsection.** A houseguests' pact changes its roster only while nobody outside it knows of it. Once anyone outside knows, it lives and ends whole, as every pact of houseguests does today.

#### L.A Value 5, `AlumniRules.GrowingPacts` (slice S3b)

**The constant.**
- In `Simulation/AlumniHistories.cs`, after S3's `CounterBlocs = 4`, add `public const int GrowingPacts = 5;`.
- `Latest` (`:30`) becomes `GrowingPacts`, and `IsDefined` (`:33`) widens on its own.
- Every term below asks `AlumniRules.At(s, AlumniRules.GrowingPacts)`, never `Latest`.

**Why a value of its own, and not an edit inside the unfrozen value 4:**
1. **The lab can only hold a whole value** (14.G). Inside value 4, the 4−3 row would mix the 7.5 desire, the hunt and growth. A paired 5−4 row isolates growth. Every value-5 term is scoped so that, with no join in a season, it changes nothing (L.F's corner cases are noted).
2. **Withdrawing it is easy.** If the owner turns growth down after S4, S3b's code comes out before S6 and `Latest` stays 4. Nothing from S1 to S5 is merged or synced before S6 (14.J), so no season is saved at 5 outside tests.
3. **The cost is small:** one constant, one lab arm, and the EditMode-only `Latest + 1` pins move to 6 on their own (14.G).

**At S6.** S6 freezes the latest value the owner keeps (14.I gives the fixtures). AS7 takes the next free value after S6. Because a value is a bound, AS7's value carries growth. As built (2026-11-01): the owner kept growth (14.H), so S6 froze 5, and AS7's value was to be 6. The quick fixes took 6 first (2026-11-03, the titled names: `AlumniRules.TitledNames`, words only), so AS7 takes the next free value, 7, in landing order.

**The claim.** Alumni rules 5 is claimed by S3b and released if growth is withdrawn before S6. It is the alumni rules field, not competition rules 5, which is PC5's (§9, `:1045`). Growth was kept (14.H), so the claim stands and S6 froze 5.

#### L.B Which pacts may grow

A pact P takes a recruit only when all of these hold.

1. **The rules are on.** `At(s, GrowingPacts)` and `EpisodeEngine.AgencyOn(s)` both hold (`Simulation/EpisodeEngine.Agency.cs:55`). The cap exists only under agency (`Simulation/NpcAlliances.cs:113`).
2. **A known pair is live.** `KnownBlocs.CounterPactBound(s) > 2` (L.C).
3. **It is the houseguests' own pact.** P is in `NpcOnlyPacts(s)` (`Simulation/NpcAlliances.cs:91-92`), and `NpcAlliances.HouseguestsOwn(P)` holds, meaning its id starts `alliance-npc-` (`:264`).
   - A story's pact (`alliance-story-`, `:172`) belongs to its arc.
   - A houseguests' pact the player joined and later left is held by rule 7, because the player stays a knower of its fact.
4. **Nobody in it is in a live pair.** No member in the house passes `KnownBlocs.InLivePair`. The bloc's own pacts stay pairs: the Hitmen re-forming, or Tyler with an outsider while the Committee is live.
5. **It is below the bound.** `EpisodeEngine.InTheHouse(s, P) < CounterPactBound(s)` (`Simulation/EpisodeEngine.GrowPacts.cs:96-97`).
6. **One change a pact a week.** No member in the house is in this week's `joined` set (`Simulation/NpcSocialActions.cs:204-205`, read from saved acts). The weekly pass's local set does the same within the pass.
7. **It is kept to itself, from an earlier week.** All of these hold:
   - the story's bonds run;
   - P's alliance fact exists and is `FactVisibility.Private`;
   - every knower of the fact is a member;
   - `fact.week < s.week`. The fact's week is set at birth (`Simulation/Story/StorySystems.cs:141`), the same wait leaks use (`Simulation/AllianceLeaks.cs:86`);
   - every member is still in the house (added in S3b's review). Somebody who has left the house has left the pact (X5), but stays on its roster and a knower of its fact, and the jury's readers match juror and finalist by the roster (L.F): a recruit taken in after they left would read as their ally at the finale.

   The week clause makes "none in its founding week" a matter of saved state, whatever path formed the pair.
8. **No target agreement inside it** (added in S3b's review). No active `TargetAgreement` has its proposer, recipient and target all among P's members in the house and the recruit (`NpcAlliances.TargetsWithin`, reading `CommitmentReferences.RawDeals` as `StrategyRules.NominationReluctance` does). The deals are struck as the week turns (`Simulation/EpisodeEngine.cs:482`), before the pact window, and an agreement runs to the end of its week, so one struck against the recruit before the join would outlive it and pull a founder's nomination toward a pact-mate (`TargetPull` 40 against the ally shield's 30). It draws and writes nothing.

   **Why rule 7 is needed.** Every reader joins P's current roster to its one fact:
   - the suspected card (`Simulation/AllianceRead.cs:607-614`);
   - `FinalistRead.AllianceCertainty` (`Simulation/FinalistRead.cs:231-239`);
   - `KnownOdds.KnownPact` (`Simulation/KnownOdds.cs:378-381`);
   - `AllianceThreat` (`Simulation/ThreatAssessment.cs:125-128`);
   - the voter's pact list (`Simulation/WebEvictionVoting.cs:241`).

   Knowers never shrink (`Simulation/AllianceLeaks.cs:105`). Growing a pact someone outside knows of would show them a member they never learned of.

**Rules 3 to 8 are engine checks**, like `PactRoom`'s reading of every private pact (`:95-99`). They feed no score, trust or reader.
- The recruit's side reads only the recruit's own pacts, scores and desire.
- In the fiction, a member brings the recruit in. That is why the sponsor is the act's partner and why the sponsor's bar is the sponsor's own score.

**When the last known pair stops being live** (a departure or a public split), the bound is 0 and nothing grows. A grown pact is never trimmed back to the bound. It sheds members, sours or ends by L.F's rules.

#### L.C The bound: one more than the known pairs, counted pair by pair

New members in `Simulation/AlumniKnownBlocs.cs` (S2's file):

```
public const int CounterPactMargin = 1;
InLivePair(s, id)   = a pair in LivePairs(s) holds id (the player too, seated on a live card)
CounterPactBound(s) = 0 below GrowingPacts, or when LivePairs(s) is empty;
                      otherwise Math.Min(EpisodeEngine.LargestPact, LivePairs(s).Count + 1 + CounterPactMargin)
```

- **How the count works.** One pair counts two people; each further live pair, chained or apart, counts one more; the margin outnumbers that by one.
- **Pairs, never people.** It counts pairs (14.D.2, "No groups"; rule 3.1). Under the cap of 4 it equals "distinct members of live pairs, plus one" in every case.
- **Size.** `LargestPact` is 4 (`Simulation/EpisodeEngine.GrowPacts.cs:60`). It is compared with the number in the house, as the player's bring-in compares it (`:116`).

| House (edition 1) | Live pairs | Bound |
|---|---|---|
| 8 (Tyler not seated) | Hitmen | 3 |
| 9 to 12 (Tyler seated) | Hitmen, Committee | 4 |
| 9 to 12, after the Committee's public split or with Tyler gone | Hitmen | 3 |
| 9 to 12, with Derrick gone | Committee | 3 |
| Cody gone; or an 8-house with Derrick gone or the Hitmen split | none | no growth |

- **When the bound falls,** a pact already above it stays as it is and takes nobody else.
- **A pact that has lost a member, to eviction or to shedding, never grows again** (amended in S3b's review; the design let it recruit again up to the bound while rule 7 held). A shed member stays a knower, and a departed one stays on the roster, so rule 7 never holds again.

#### L.D Who joins, and how

**D.1 The bar**

| Who | Bar | Source |
|---|---|---|
| The recruit | An active non-player NPC. Not in `joined`, not in a live pair, and in no NPC-only pact (one each, `:98`). Fewer than `MaximumEach` = 3 pacts (`:33`). Not `Allied` with any member in the house (no stacking, as `Simulation/EpisodeEngine.GrowPacts.cs:113` refuses) | |
| The recruit toward the **sponsor** | `Warm`: a score of at least `MinimumRelationship` = 25, and `Desire` above `ProposeThreshold` = 25 | `:30,36,149-151`. A proposer's own bar. `Desire` carries temperament (`:84`) and S3's `CounterDesire` |
| The sponsor toward the recruit | A score of at least 25 | Not the sponsor's `Desire`: its −10 a pact carried (`:82`) prices a new pact, and a join adds none to the sponsor |
| Every member in the house and the recruit, both ways | **`JoinWelcomeLine` = `SourLine − NpcSocialActions.RumorImpact` = −5**, and no grudge of `AllianceGrudgeLine` = 40 either way | A new named constant, not C5's −10 (`:129-136`). One rumour (−15 both ways, `Simulation/NpcSocialActions.cs:62,535,682-683`) can never take a fresh edge below `SourLine` (−20, `:55`). A cold pair (−10 or below, NPC-AGENCY-PLAN constraint 3; `Tests/EditMode/NpcAgencyHarnessTests.cs:78`) is never joined |

**D.2 The rule.** In `Simulation/NpcAlliances.cs`, after `TryPropose` (`:230-250`). It is pure, like the rest of the class (`:21-25`). `Form`'s literal at `:264` may use the prefix constant; the string does not change.

```csharp
/// <summary>The houseguests' own pacts are the weekly pass's (Form), never a story's.</summary>
public const string HouseguestPactPrefix = "alliance-npc-";
public static bool HouseguestsOwn(AllianceState pact) =>
    pact?.id != null && pact.id.StartsWith(HouseguestPactPrefix, StringComparison.Ordinal);

/// <summary>14.L: a joiner and every member, both ways, one rumour above SourLine; never a cold pair.</summary>
public const double JoinWelcomeLine = SourLine - NpcSocialActions.RumorImpact;   // -5

/// <summary>14.L: an outsider joins an outsider pact of houseguests while a known pair stands. No roll.</summary>
public static bool TryJoin(EpisodeState state, string npcId, ISet<string> joined, out string sponsorId)
{
    sponsorId = null;
    // First, so a season below 5 reads nothing and plays byte for byte as before.
    if (!AlumniRules.At(state, AlumniRules.GrowingPacts) || !EpisodeEngine.AgencyOn(state)) return false;
    int bound = KnownBlocs.CounterPactBound(state);
    if (bound <= 2) return false;
    var recruit = state.Find(npcId);
    if (recruit == null || recruit.isPlayer || recruit.status != ContestantStatus.Active) return false;
    if ((joined != null && joined.Contains(npcId)) || KnownBlocs.InLivePair(state, npcId)) return false;
    var pacts = NpcOnlyPacts(state);
    if (pacts.Any(a => a.members.Contains(npcId))) return false;
    if (ActiveAlliancesFor(state, npcId).Count >= MaximumEach) return false;

    var pick = pacts
        .Where(p => HouseguestsOwn(p) && EpisodeEngine.InTheHouse(state, p) < bound && KeptToItself(state, p))
        .Select(p => (pact: p, here: p.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList()))
        .Where(x => x.here.All(m => !KnownBlocs.InLivePair(state, m) && (joined == null || !joined.Contains(m))
                                    && !state.Allied(npcId, m) && Welcomes(state, m, npcId) && Welcomes(state, npcId, m)))
        .Where(x => !TargetsWithin(state, x.here, npcId))                 // rule 8, added in S3b's review
        .SelectMany(x => x.here.Where(m => Warm(state, npcId, m) && state.Score(m, npcId) >= MinimumRelationship)
                                .Select(m => (pact: x.pact, sponsor: m, desire: Desire(state, npcId, m))))
        .OrderByDescending(c => c.desire).ThenBy(c => c.sponsor, StringComparer.Ordinal).ThenBy(c => c.pact.id, StringComparer.Ordinal)
        .FirstOrDefault();
    if (pick.pact == null) return false;
    Join(state, pick.pact, npcId);
    sponsorId = pick.sponsor;
    if (joined != null) { joined.Add(npcId); joined.Add(sponsorId); }
    return true;
}

/// <summary>14.L: the pact's fact is private and known only to its members - nobody outside sees its roster.</summary>
private static bool PrivateToMembers(EpisodeState state, AllianceState pact)
{
    if (!EpisodeEngine.StoryAt(state, StoryRules.Bonds)) return false;
    var fact = Knowledge.Of(state, FactKinds.Alliance, pact.id);
    return fact != null && fact.visibility == FactVisibility.Private && fact.knowers.All(pact.members.Contains);
}

/// <summary>14.L.B 7: private to its members, from an earlier week than this one, and every member still in the house
/// (the last clause added in S3b's review).</summary>
private static bool KeptToItself(EpisodeState state, AllianceState pact) =>
    PrivateToMembers(state, pact) && Knowledge.Of(state, FactKinds.Alliance, pact.id).week < state.week
    && pact.members.All(id => state.Find(id)?.status == ContestantStatus.Active);

/// <summary>14.L.B 8 (added in S3b's review): an active target agreement among the pact-to-be. Pure.</summary>
private static bool TargetsWithin(EpisodeState state, List<string> here, string recruitId)
{
    var group = new HashSet<string>(here, StringComparer.Ordinal) { recruitId };
    return CommitmentReferences.RawDeals(state).Any(d => d != null && d.status == DealStatus.Active && d.type == DealKind.TargetAgreement
        && d.proposerId != null && d.recipientId != null && d.targetId != null
        && group.Contains(d.proposerId) && group.Contains(d.recipientId) && group.Contains(d.targetId));
}

private static bool Welcomes(EpisodeState state, string fromId, string toId) =>
    !GrudgeBetween(state, fromId, toId) && state.Score(fromId, toId) >= JoinWelcomeLine;

private static void Join(EpisodeState state, AllianceState pact, string recruitId)
{
    var here = pact.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList();
    pact.members.Add(recruitId);                                   // at the end: the founder stays members[0]
    Knowledge.AddKnower(state, Knowledge.Of(state, FactKinds.Alliance, pact.id), recruitId);  // KeptToItself found it
    foreach (var member in here)
        RelationshipLedger.Record(state, member, recruitId, "alliance-formed", EpisodeEngine.AllianceFormedImpact, pact.name + " was joined");
}
```

**D.3 Where it is called.** The recruit acts on its own pact rung, only after `TryPropose` has failed, so its own birth always comes first. A join can still take an outsider whom someone else would have asked later that week, so births at 5 can fall. S4 counts them.

- **The all-week beat,** `Simulation/NpcSocialActions.cs:206-210`, becomes:

  ```csharp
  if (EpisodeEngine.Window(state) == EpisodeEngine.PactWindow(state, npcId))
  {
      if (NpcAlliances.TryPropose(state, npcId, joined)) { /* :208-209 unchanged */ }
      if (NpcAlliances.TryJoin(state, npcId, joined, out string sponsorId))
          return new NpcActState { kind = NpcActKinds.Pact, actorId = npcId, partnerId = sponsorId };
  }
  ```

- **The weekly pass,** `:155`, becomes `if (NpcAlliances.TryPropose(state, npc.id, joined) || NpcAlliances.TryJoin(state, npc.id, joined, out _)) taken++;`.
- **The rule-statement loop,** `Simulation/NpcAlliances.cs:213`, becomes `if (!TryPropose(state, npc.id, joined)) TryJoin(state, npc.id, joined, out _);`.
- **`Perform(AllianceProposal)`** (`Simulation/NpcSocialActions.cs:361-362`) is unchanged. `Settle` and `Beat` skip that kind (`:164,224`).
- **Below 5,** `TryJoin` returns false before reading anything.
- **Runtime gains nothing.**

**D.4 What a join writes**

**Written:**
- **The members list.** The recruit is appended at the end. The founder stays first (`Simulation/EpisodeEngine.GrowPacts.cs:75`) and still calls the bloc (`Simulation/WebVotingBlocs.cs:94-95`).
- **The fact.** The recruit becomes a knower. The fact stays private, and its actor and subject stay the founders (`Simulation/Story/StorySystems.cs:139-140`).
- **The ledger.** `alliance-formed` at 30, both ways, between the recruit and each member in the house. That is the record `Form` writes (`Simulation/NpcAlliances.cs:271`) and C5's bring-in writes (`Simulation/EpisodeEngine.GrowPacts.cs:196-197`).

**Not written:**
- no `Change`, `Log` or `Remember`;
- no new pact id, name or `AllianceRow` (`Simulation/EpisodeEngine.Ledger.cs:117-120`);
- no new ledger type, fact kind or act kind.

**The act** is `NpcActKinds.Pact`, with the recruit as actor and the sponsor as partner.
- Validation accepts any party as partner (`Simulation/EpisodeNpcSocialValidation.cs:114-116`).
- The engine gives the act its id and its room on the existing keyed coin (`Simulation/EpisodeEngine.AllWeek.cs:216-224,253-258`).
- **From 5, a `Pact` act no longer means a birth.** It cannot carry the pact's id: only `Meet` names a pact (`:116`). Every counter reads births from `alliance-npc-` ids and joins from a rise in `members.Count`.

**D.5 Draws and sequence ids**
- **The join draws nothing.** Everything it reads is saved state.
- **Shedding and the hunt fix draw and mint nothing** (L.F).
- **Downstream draws.** Existing sites may draw more often: the bloc round's compliance roll per member, and a hunt's gossip roll. No draw site is new.
- **Sequence ids:** two per member in the house before the join, so 4 for a third member and 6 for a fourth. They go through the existing writer, at 5 only. This is the recorded exception to §14's rules (edits 3 and 4).

**D.6 Why it does not ratchet**
- A join is aimed at nobody and moves no score.
- Each recruit leaves the pool (one NPC-only pact each).
- The bound caps every pact.
- A pact changes once a week at most, and never in its founding week.
- Joining stops when no pair is live.
- Pact-mates never hunt each other (L.F).
- **What membership repeats is already bounded:**
  - the weekly meeting: +3 per pair, once a pact a week (`Simulation/NpcSocialActions.cs:477-498`);
  - S3's hunt: once a member a week, each listener stopping at 6.

#### L.E The cap, one each, and the player

- **The cap counts pacts** (`Simulation/NpcAlliances.cs:88`). A join adds none.
- **Pairs are still born as pairs.** `PactRoom`, `WouldPropose` and `Form` are untouched (`Tests/EditMode/GrowAndManageAlliancesTests.cs:273-278` holds).
- **One each.** The recruit is in no NPC-only pact before the join and in exactly one after it.
- **The player is never recruited.** A pact with the player in it is never NPC-only (`:92`). C5's bring-in is unchanged.
- **The invitation skip, at 5, for houseguests' pacts only.** `AllyThroughInvitation` adds the player to the inviter's pact with no size check (`Simulation/EpisodeEngine.Strategy.cs:248-257`). The `Where` at `:249` gains:

  `&& !(AlumniRules.At(s, AlumniRules.GrowingPacts) && NpcAlliances.HouseguestsOwn(a) && InTheHouse(s, a) >= LargestPact)`

  - A grown four is passed over, and the player and the inviter start a pact of their own (`:265-279`).
  - Below 4 in the house nothing changes, and a story's four still takes the player in at every value.
  - Without growth, no houseguests' pact without the player ever holds four in the house: the only paths that add a member are the invitation (the player) and C5's bring-in (cap 4, counting the player) (`Simulation/EpisodeEngine.GrowPacts.cs:116,187`; `Simulation/EpisodeEngine.Strategy.cs:257`). So the skip is growth-only.

#### L.F Consumers

| Consumer | With 3 or 4 houseguests | Change at 5 |
|---|---|---|
| Pact act (`Simulation/NpcSocialActions.cs:206-210`) | It assumed the newest pact | The join builds its own act |
| **`CommonThreat`** (`Simulation/NpcDeals.cs:143-150`), read by the hunt (`Simulation/NpcAgendas.cs:54-58`), `Offer`'s target agreement (`Simulation/NpcDeals.cs:200-201`) and `TargetFor` (`:588`) | It excludes only a and b. In a trio, the third member qualifies as soon as they hold two wins and one of the pair reads them below 20. The hunt then costs that listener 6 a week with no stop (`Simulation/NpcSocialActions.cs:419-436`), and a target agreement pulls a nomination by 40 against the ally shield's 30 (`Simulation/StrategyRules.cs:46,52`) | **At 5, a non-player candidate is passed over** when an active houseguests' pact holds a, b and that candidate. See the code below the table. The player in a pact with both is read as at 4. It stops new agreements only; one struck before a join is the next row's |
| A target agreement struck before a join (`NpcDeals.Settle` as the week turns, `Simulation/EpisodeEngine.cs:482`; `StrategyRules.NominationReluctance`, `Simulation/StrategyRules.cs:446-447`) | An agreement runs to the end of its week, so two founders could agree on W at the week's open and W join through one of them before the nominations: the founder as Head of Household is pulled 40 toward his new pact-mate (found in S3b's review) | **The join waits** (L.B rule 8): no pact takes a recruit while an active target agreement has its proposer, recipient and target all among the pact-to-be. Once it has run out, the join may go ahead |
| The jury and finalist readers: `Allegiance.JuryLoyalty` and `SharedToTheEnd` (`Simulation/Allegiance.cs:243-266`), `WebJuryVoting.AllianceLoyalty` and `Reason` (`Simulation/WebJuryVoting.cs:84-93,246-248`), `FinalistRead.Lean` (`Simulation/FinalistRead.cs:285-289`) | Every one matches juror and finalist by `members.Contains`, and somebody who left the house stays on the roster. A recruit taken in after a member left would read as that juror's ally: 25 loyalty, "We were in this together", "shared an alliance" (found in S3b's review) | **None in the readers.** A pact that has lost a member never grows again (L.B rule 7, L.C), so every member a juror shares a roster with was in the house with them |
| A shed joiner at the jury (the same readers) | `TakeOutOfPact` takes the joiner off the roster with no trace, so at the finale they share nothing with the founders: 0, not the 25 of a pact that existed and ended | **None: C5's reading of a member taken out** (C2's cut, C5's leave), now reaching houseguests' pacts at 5. Decided in S3b's review; pinned in fixture 16. S4's shed rows read it so |
| **`Dissolve`** (`Simulation/NpcAlliances.cs:304-329`) | One edge below −20 ends the whole pact (`:325-327`) | **At 5,** before `var members` (`:309`): `if (AlumniRules.At(state, AlumniRules.GrowingPacts)) ShedSourJoiners(state, alliance);`. Code below. Otherwise whole, as now |
| `Intact`, `EndBroken` (`:345-354`) | At least two in the house | None |
| Meeting (`Simulation/NpcSocialActions.cs:488-495`) | +3 for every pair in the house | None |
| Agendas and S3's hunt (`Simulation/NpcAgendas.cs:51-59`) | One pursuit per member a week | None beyond the `CommonThreat` row |
| `Allied`, `Holds`, `Counted`, the +30 shield, the +20 veto pull, the jury's 100 for a standing pact (`Simulation/WebJuryVoting.cs:78-87`) | Any size | None |
| `AllianceThreat` and the vote's threat (`Simulation/WebEvictionVoting.cs:413`) | 4 a head, capped at 20: 12 for a trio, 16 for a four, for knowers only | None |
| `AllianceLoyalty` (`:447`) | 40, 50, 40 for 2, 3, 4 members | None |
| Bloc round (`Simulation/WebVotingBlocs.cs:84-131`) | The founder calls | None |
| Leak odds (`Simulation/AllianceLeaks.cs:61-63`) | 8% for a trio, 11% for a four | None |
| `ListenIn` (`Simulation/EpisodeEngine.AllianceLeaks.cs:146-155`) | Never reveals a pact with 3 or more in the house | None |
| Fact actor and subject, gossip odds (`Simulation/Story/StorySystems.cs:139-140,345-347`) | Keyed on the founders | None |
| Roster-against-fact readers (L.B 7) | Would leak a member | Safe: a pact grows and sheds only while private to its members |
| Story `AllianceLeave` (`Simulation/Story/EpisodeEngine.StoryEffects.cs:202-213`) | Removes the leaver, and grudges of 80 follow | None. The story session is told (L.K) |
| Power Shift casting (`Simulation/Story/StoryCatalog.HouseRemembers.cs:583-587`) | Casts the largest pact first, and every option leaks it to the player (`:600-627`) | None. The story session is told. S4 counts it |
| Validation (`Simulation/EpisodeValidation.cs:216-218`), ledger origin (`Simulation/EpisodeEngine.Ledger.cs:130-132`) | Distinct members; origin read from the id | None |

The `CommonThreat` change:

```csharp
bool grown = AlumniRules.At(state, AlumniRules.GrowingPacts);   // read first: below 5 the predicate below is never evaluated
... .Where(other => other.id != a && other.id != b
                    && !(grown && !other.isPlayer && state.alliances.Any(p => p.active && NpcAlliances.HouseguestsOwn(p)
                         && p.members.Contains(a) && p.members.Contains(b) && p.members.Contains(other.id))))
```

The `ShedSourJoiners` rule, in `Simulation/NpcAlliances.cs`:

```csharp
/// <summary>14.L: a houseguests' pact past two, still private to its members, goes on without a joiner who has
/// soured with anybody in it - the latest such joiner first, as C5's defector rule takes out the one who turned.
/// The first two in its order are its founders; their souring still ends it whole. No ledger entry, no line. Pure.</summary>
private static void ShedSourJoiners(EpisodeState state, AllianceState pact)
{
    if (!HouseguestsOwn(pact) || pact.members.Contains(state.playerId) || !PrivateToMembers(state, pact)) return;
    while (true)
    {
        var here = pact.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList();
        if (here.Count <= 2) return;
        string sour = pact.members.Skip(2).Where(here.Contains)
            .LastOrDefault(j => here.Any(o => o != j && (state.Score(j, o) < SourLine || state.Score(o, j) < SourLine)));
        if (sour == null) return;
        EpisodeEngine.TakeOutOfPact(state, pact, sour);   // more than two in the house: it goes on (GrowPacts.cs:270-276)
    }
}
```

What shedding means:
- **The shed member stays a knower**, so the pact never grows or sheds again. From then on it ends whole.
- **A pact known outside it ends whole.** A known card keeps every face and its dated lines: those lines match by roster names (`Simulation/AllianceRead.cs:580-600`), and inactive pacts still draw cards (`:575`).

Scope:
- **Corner cases.** The `CommonThreat` and `Dissolve` changes touch a houseguests' pact that holds three NPCs only through growth. The one other way is the player's C5 bring-in into an invited pair, and the lab's scripted players never use it.
- **Story trios** keep the `CommonThreat` flaw at every value. That is reported to the story session, not fixed under a growth value.

#### L.G What the player sees (illustrative; S4 measures whether it happens)

- **No new caption, and nothing appended to one.** `SuspectedCardPrefix` and §14.F's "PAST GAMES" are unchanged.
- **One possible 8-house week with the Hitmen live** (Dan–Janelle formed in an earlier week, Will a free outsider):
  1. Will's read says "Will is looking for a partner, and it's Janelle" (`Simulation/NpcAgendas.cs:104`).
  2. At his pact window, Will joins.
     - A sighting reads "You saw Will and Janelle with their heads together in the {room}." (`Simulation/EpisodeEngine.AllWeek.cs:328-333`).
     - A paid listen-in hears "Will and Janelle agreed to work together." (`Simulation/NpcActKinds.cs:73`). It teaches no more, because three are in the house.
  3. A hunt line may follow (`Simulation/NpcAgendas.cs:103`).
  4. A leak names all three (`Simulation/AllianceLeaks.cs:160-161`).
  5. Once the player knows, the suspected card shows three faces under "A, B and C" (`Runtime/Episode/EpisodeHud.Alliances.cs:92-130`; `Runtime/Episode/EpisodeDirector.Alliances.cs:126`).
- **This depends on the cap.** If the Hitmen re-form, the cap of 2 leaves room for one outsider pair. Whether any join fires in the 8-house is unmeasured.
- **Learning a pact means learning its roster as it stands then.** A pact anyone outside has learned of never grows. S4 counts how often that holds a pact back, and why.

#### L.H Balance

**Expected at 5, not measured:**
- **Joins may be rare in the 8-house.** Births run 1.0 to 1.5 a season (`Tests/EditMode/NpcAgencyHarnessTests.cs:129-131`). A join needs all of these:
  - a pact from an earlier week;
  - a free outsider at 25 or more both ways, with desire above 25. Build stops at 25 (`Simulation/NpcAgendas.cs:77-83`);
  - both Hitmen in the house and unsplit.
- **A complying trio can carry the vote.** From week 2 an 8-house has four voters, and three from week 3 (`Simulation/EpisodeEngine.cs:836`). A trio voting whole is three of four, then all of them.
- **Births at 5 can fall,** because a join takes an outsider someone else would have asked later.
- **Known-pair members leave earlier.** Stop condition 2 is the one most likely to fire.
- **Grown pacts are brittle once known.** They end on any of their 3 or 6 edges. While private, they shed instead.
- **The 12-house has a pile-on risk.** A story's `AllianceLeave` from a four gives three grudges of 80 at once.
- **Power Shift draws grown pacts first** and leaks them.

**The report** (`Tests/EditMode/BalanceLabAlumniReport.cs`):
- **Arms:** 5 (shipped), held 4, 3, 2, 1, and 0.
- **Size and time:** 6 × 8 cells × 100 seeds = 4,800 seasons, about 36 minutes, run alone.
- **Comparisons:** 5−4 and 5−0 are added.

**A per-command `PactWatch`** (built in S4: 14.H's as-built note) sits beside `PariahWatch` (`Tests/EditMode/BalanceLab.cs:259`). It is kept out of the hashed rows (`:119`) and added to `BalanceLabParts.RoundTrip` (`Tests/EditMode/BalanceLabParts.cs:103`). It reports, per cell:
- births (from `alliance-npc-` ids) and joins (from `members.Count` rises, player-free);
- seasons with at least one join, and the week of the first join;
- bloc-live weeks (bound above 2), and joins per bloc-live week;
- the largest outsider pact (a distribution over 2, 3 and 4), and the shares reaching 3 and reaching 4;
- pact-weeks held below the bound only because the pact was known outside it, split by cause: whisper, listen-in, story (Power Shift named), other;
- joiners shed per season; how grown pacts end (the ledger's `why`), the weeks they lasted, and whether they were known at the end;
- NPCs in NPC-only pacts as a share of the house (weekly mean and maximum); seasons with two grown pacts at once;
- whether the player was invited into a grown pact, and the week the player first learned of one;
- evictions carried by a complying outsider bloc of three or more;
- pile-on targets, classed as known-pair member, grown-pact member, shed joiner or other, with `PariahDecomposition`'s columns (`Tests/EditMode/BalanceLabDiagnostics.cs:282`);
- cold pairs at week 3; hunts per week on one target;
- hunts and target agreements aimed at a pact-mate of both. At 4 this sizes the pre-existing story-trio flaw.

**Must be zero at 5:**
- a join with no live pair;
- a join above its bound;
- more than 4 of a pact in the house;
- a live-pair member in a grown pact;
- a join into a pact that was not `alliance-npc-`, not private to its members, or in its fact's week;
- a join into a pact with a member out of the house, or while a target agreement stands among the pact-to-be (rules 7 and 8, added in S3b's review);
- a hunt or target agreement aimed at a non-player pact-mate of both in a houseguests' pact;
- cap breaches and double-booking (`Tests/EditMode/NpcAgencyHarnessTests.cs:50-56`).

A nonzero must-be-zero row is a bug, fixed before S6. It is never an owner question. (S4's full run at `20f665b7`: every must-be-zero row zero, so S6 had none to fix.)

**If the 8-house rate is near zero (decided now).** If fewer than one season in ten of any 8-house cell at 5 holds a join:
- the report's first line says so, gives the 9-to-12 rates, and states that the call shows mostly in houses of 9 or more;
- nothing is retuned before S6;
- the lever named for a later value, built only on the owner's word, is the recruit's bar toward its sponsor (score 25, desire above 25).

#### L.I Tests

New file `Tests/EditMode/CounterPactGrowthTests.cs`:
- with a hand-written meta, added to `Tools/SimulationTests/SimulationTests.csproj` near `:293`;
- public members only;
- seasons from `AlumniHistoriesTests.AllStars(size, seed, player, value)`, always naming `value`;
- real pairs from `NpcAlliances.TryPropose` in an earlier week, never the `alliance-test-` helper;
- beats as in `Tests/EditMode/AllWeekCadenceTests.cs:622-647`, **without** its `agencyRulesStartWeek = 0` (`:634`), which turns agency off and fails `TryJoin`'s first check.

Four test helpers become `internal`:
- `AllWeekCadenceTests`: `InWindow` (`:1039`) and `Score` (`:1098`);
- `AlumniHistoriesTests`: `Saved` and `Load` (`:766-768`).

The fixtures:
1. `AnOutsiderJoinsACounterPactAtFiveWhileAKnownPairIsLive`: the default cast, `AllStars(8, seed, value: 5)`, through `Beat` at the recruit's `PactWindow`.
   - Members are [founder, mate, recruit], with the same id, name and row.
   - The recruit is a knower; `NpcOnlyPacts.Count` is unchanged; the act names the sponsor.
2. `BelowFiveNothingGrowsAndTheStateIsByteIdentical` (TestCase 4, 3, 2, 1, 0): `TryJoin` is false, `Dissolve` and `CommonThreat` read as before, and the Json is unchanged.
3. `ARegularSeasonNeverGrowsAPact`.
4. `NoPactGrowsOnceNoKnownPairIsLiveAndAGrownPactStands`: Derrick evicted; a public split.
5. `NoLivePairMemberJoinsSponsorsOrIsGrown`: in a 9-to-12 house; Tyler may join once the Committee has split in public.
6. `APactGrowsToTheBoundAndNeverPastFour`: 3 in the 8-house; 4 in a 9-to-12 house; after the Committee splits, a four stays a four.
7. `OneChangeAPactAWeekAndNoneInItsFoundingWeek`: through `Beat`, and also for a pair formed by a direct `TryPropose` that leaves no act. Pact acts: at most one per NPC on either side.
8. `TheRecruitMustWantInAndEveryMemberMustWelcome`. Each of these is refused:
   - desire at exactly 25; the recruit's score at 24; the sponsor's at 24;
   - a member at −6 (−5 is accepted);
   - a grudge of 40 either way;
   - a −10/−10 pair;
   - a recruit already `Allied` with a member, at `MaximumEach`, or in an NPC-only pact.
9. `TheRecruitsEdgesMatchABirths`: two `alliance-formed`/30 entries per new edge; none on the founders' edge; trust matches a birth's; no score moves.
10. `AJoinDrawsNothingAndMintsTwoSequencesAMember`: `randomState` unchanged; +4 for a trio, +6 for a four.
11. `TheCapAndOneEachStillHold`.
12. `APactAnyoneOutsideKnowsOfNeverGrows`: the player listened in; a whispered fact; a public fact; a story leaver who knows; a shed member. After a join, the knowers are exactly the members.
13. `OnlyTheHouseguestsOwnPactsGrow`: a `FormFromStory` pact; a pact the player left.
14. `ThePlayerIsNeverRecruited`.
15. `AnInvitationNeverTakesAHouseguestsFourPastFour`: at 5, a grown four is passed over and a new pact forms; at 5 and at 4, a story's four still takes the player in.
16. `AGrownPactShedsASourJoinerWhileKeptToItselfAndOtherwiseEndsWhole`:
    - one rumour on a −5 edge ends nothing;
    - at −21 the joiner is out, the founders stand, the row stays open, and the pact never grows again;
    - a founders' edge below −20 ends it as `npc/soured`;
    - once whispered, a joiner's sour edge ends it whole, and a knower's card keeps three faces and its dated lines;
    - at 4, a hand-built trio ends whole.
17. `AGrownPactVotesAsOneBlocWithItsFounderCalling`: leak odds 0.08; threat 12 for a knower and 0 for anyone else.
18. `TheWeeklyPassAndTheRuleAloneJoinAsTheBeatDoes`.
19. `JoinsAreDeterministicAndReplayAcrossASave`.
20. `AGrownPactNeverHuntsOrTargetsItsOwnMember`: a recruit with 2 wins and a non-sponsor founder at 10 toward them.
    - At 5: `CommonThreat(F1, F2)` passes the recruit over, no `Hunt` targets them, and `Offer` gives no target agreement against them.
    - The player in the pact is read as at 4.
    - At 4, a hand-built trio still names its member, pinning the old behaviour.
21. `APactThatHasLostAMemberNeverGrowsAgain` (added in S3b's review): a founder at the jury, and a four's member evicted; the juror reads the would-be recruit as a stranger; the shed reads only the joiners in the house.
22. `NoJoinWhileTwoOfThePactToBeHaveAgreedToTargetTheThird` (added in S3b's review): `NpcDeals.Settle` strikes the founders' agreement against the recruit, the join waits that week, and goes ahead once it has run out; an agreement of the recruit and a member against another member bars it too.

**Existing tests:**
- **Every S1 to S3 behaviour test names its own value** (`Loyalty`, `Reputation`, `CounterBlocs`). Only the writer, replay and validation cases use the `AllStars` default of `Latest`.
  - S3b's builder greps `AllStars(` without `value:` in `AlumniLoyaltyTests`, `KnownBlocsTests` and `CounterBlocTests`, and pins what it finds.
  - `TheOrdinaryCommonThreatStillComesFirst` is held at 4. `TheScoreFloorAndThePactCapStillHold` gains a sibling at 5 with a size clause.
- **`AlumniHistoriesTests`.** The replay and validation cases at `Latest` (`:627-670`) pass unedited.
- **EditMode-only `Latest + 1` pins** move on their own. Re-read them through the Mono harness.
- **Unchanged:** every Regular pin, including `Tests/EditMode/NpcAllianceTests.cs:142` and `Tests/EditMode/AllWeekCadenceTests.cs:457-501,714-715`, plus `StoryPacingTests`, `BalanceLabGoldens` and `OldSaveAcceptanceTests`.
- **The floors** in `Tools/baseline.txt` rise in S3b's commit.

#### L.J Slice S3b (after S3, before S4)

**Files:**
- `Simulation/AlumniHistories.cs`: the constant, `Latest`, and the doc at `:12-16`;
- `Simulation/AlumniKnownBlocs.cs`: `CounterPactMargin`, `InLivePair`, `CounterPactBound`;
- `Simulation/NpcAlliances.cs`: the new members (`HouseguestPactPrefix`, `HouseguestsOwn`, `JoinWelcomeLine`, `TryJoin`, `PrivateToMembers`, `KeptToItself`, `Welcomes`, `Join`, `ShedSourJoiners`); the `Dissolve` call at `:309`; `:213`; the docs at `:87`, `:94`, `:153-157`, `:276-302`;
- `Simulation/NpcDeals.cs:143-150`;
- `Simulation/NpcSocialActions.cs:155,206-210`;
- `Simulation/EpisodeEngine.Strategy.cs:249`;
- the tests above and the visibility edits to the four test helpers.

**Proof:** the dotnet subset, plus the Mono harness for the EditMode-only pins. No Unity run.

**Later slices:**
- S5 adds captures of three-face and four-face suspected cards at Standard and Largest text.
- S6 freezes 5, or 4 if the owner withdraws growth. Built 2026-11-01: 5, growth being kept (14.J's as-built note for S6).

#### L.K Defaults, and notes for the lead

**Defaults:**
- value 5, removable before S6; kept by the owner's call and frozen at S6 (14.H, 14.J);
- pacts grow in place, only while private to their members, from an earlier week, with every member in the house and no target agreement among them (the last two added in S3b's review), and a pact that has lost a member never grows again;
- only houseguests' own outsider pacts grow;
- the bound is min(4, live pairs + 2);
- one change a pact a week;
- the three-tier bar, with the members' line at −5;
- shed while private, whole once known;
- pact-mates never a common threat;
- no log line;
- the invitation skip for houseguests' fours;
- no one-at-a-time limit (it is measured);
- the near-zero rule in L.H.

**Questions for the owner:** none.

**For the lead:**
- **Tell the story session:**
  - their constraint 2 is amended;
  - the Power Shift comment (`Simulation/Story/StoryCatalog.HouseRemembers.cs:583-584`) goes stale, and the story now casts grown pacts first and leaks them (`:586-627`);
  - their `AllianceLeave` edits a grown pact's members (`Simulation/Story/EpisodeEngine.StoryEffects.cs:209-210`): their call;
  - story trios can hunt and target their own member through `CommonThreat` at every value: their call, under their own gate.
- **Tell AS7** to take the next free value after S6: 6, since S6 froze 5 (2026-11-01). Its value-6 commit also rewords the `AlumniRules` summary (`Simulation/AlumniHistories.cs:23-25`), which went stale at S6, to say that 5 was kept and frozen at S6 (2026-11-01) and that AS7 is 6 (14.J's as-built note for S6). **Superseded (2026-11-03):** the quick fixes took 6 for the titled names and reworded that summary; AS7 takes 7 and carries 6's words.
- **Tell S1 to S3's builders now** to name `value:` in their behaviour tests.

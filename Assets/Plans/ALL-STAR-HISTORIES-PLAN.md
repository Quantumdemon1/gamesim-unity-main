# All-Star player histories: the alumni's past games, rebuilt

Design plan, slice H0 (documents only), 2026-10-25, on `claude/all-star-histories` from main `b208d46d` (role moments
R0 to R6 and R5b, the dialogue V2 foundation, schema 29). No code changes with this slice.

**Revised the same day (H0's fix)** to agree with the lead's master plan for dialogue, props and competitions, and
All-Star (2026-10-25, against main `b208d46d`; "the master plan" below). What changed:
- schema 30 is **pooled**: one bump, built once on this branch, that carries every field the other tracks need, each
  inert until its own slice (section 5). It is its own slice, SCH30, and the histories build on it;
- the order follows the owner's choice of histories first (section 1);
- the parts of the histories that move balance wait for B9, and section 9 names them.

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
- The joint design note, `Assets/Plans/DIALOGUE-PLAN.md` on `claude/dialogue-v2` at `4f01a559` (the master plan's P0;
  "the joint note" below). Its §6, "Schema 30: fields dialogue needs", is marked final and lists none. Its §2
  defines dialogue rules 2 and later only at schema 30 (`Rules2Schema` = 30), and its decision 7 lands D2 after the
  pooled bump.

**Status words** follow ROLE-MOMENTS-PLAN: *shipped*, *gated*, *intent*, *missing*.

---

## 0. The short version

- **What:** in a fresh season that seats the real alumni, the house starts with what the alumni did to and with each
  other on the show. Old partners start closer, people who were nominated or evicted by another start warier of them,
  and the Hitmen (Derrick and Cody) walk in with their final two. After B9, a winner's record also makes them a bigger
  target.
- **What it is not:** no grudge, act or arc of this season, no romance or personal life, no knowledge a houseguest
  could not have, no guaranteed alliance and no forced result. Everything after week one is played as usual.
- **Facts:** only on-show, public game acts already in the project's research (section 3). Edition 1 uses six pairs
  from shared Big Brother seasons and a career record for all twelve.
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
  `UpgradeV29ToV30` adds the pooled off literals. Old seasons replay byte for byte (section 8).
- **Balance:** the opening state (standings, trust receipts, the Hitmen row) ships with the histories and is measured,
  not tuned. Every new term in a decision rule waits for B9: career reputation, pair risk, the vote shield, Will's
  throw (through the competition rules), the repertoires and the stat overrides (section 9).
- **Slices, histories first:** H1a (the edition, inert), SCH30 (the pooled schema, every writer inert, qualified alone),
  H1b (the histories' opening state, at alumni rules 1), the optional H1c (shown), then after B9 H1d (the parts that
  move balance). The campaigns AS2, AS3 and on follow H1b (section 11).

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
   that plan had it.
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
| 2 | **Career reputation** | how big a threat each alumnus's record makes them look to the house | missing; Codex: candidate only | H1d, after B9 (section 9) |
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
4. **Game register.** Nothing romantic, personal or about conduct, and no source whose subject is any of those.
5. **Direction matters.** Who nominated whom decides whose trust moves.

### 3.2 Edition 1's pairs

Edition id `allstar-histories-1` (immutable: a later change is a new edition, section 5.4). Six pairs, all from
shared Big Brother seasons. Numbers are provisional (3.6): set in H1a, reported on in H1b and measured again with B9.

| Fact id | People | On-show act (research row) | Shared standing (`Move`) | Directed trust (`RecordOneWay`, impact) |
|---|---|---|---|---|
| `bb16-hitmen` | Derrick, Cody | The Hitmen alliance reached the BB16 final two (`hitmen-bb16`) | +20 | each toward the other +15 (alliance, at half weight) |
| `bb22-committee` | Cody, Tyler | The Committee alliance in BB22; Cody targeted Tyler in the endgame (`cody-tyler-bb22`) | +10 | each toward the other +15; Tyler toward Cody a further -12.5 (the target, as a nomination at half weight) |
| `bb22-tyler-nominated-janelle` | Tyler, Janelle | Tyler nominated Janelle in BB22 (`janelle-tyler-bb22`) | none | Janelle toward Tyler -12.5 |
| `bb7-danielle-nominated-janelle` | Danielle, Janelle | Danielle nominated Janelle in BB7 (`danielle-janelle-bb7`) | none | Janelle toward Danielle -12.5 |
| `bb7-janelle-evicted-will` | Janelle, Will | Janelle evicted Will at BB7's final four (`will-janelle-bb7`) | none | Will toward Janelle -10 (a vote against, at half weight) |
| `bb7-chilltown-renomination` | Will, Danielle | Will's Chilltown pushed the replacement nomination that put Danielle up before her eviction (`will-danielle-bb7`) | none | Danielle toward Will -12.5 |

Reach by table order (`Simulation/SeasonBuilder.cs:156-160`): a default 8-house All-Stars season with the newcomer
seats Dan, Will, Derrick, Janelle, Cody, Danielle and Vanessa, so it holds four of the six pairs (Hitmen and the three
BB7 rows). The two BB22 rows need Tyler, who is seated from nine houseguests.

### 3.3 Held out of edition 1

| Research row | Why it is held out | Could a later edition take it? |
|---|---|---|
| `will-janelle-bb7`'s "close game relationship ... still described him as a friend" | Rule 3 and 4: an interview reading, and the row's only source is an article about showmances. Edition 1 keeps the eviction only. | No |
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
| Janelle Pierzina | played BB6, BB7, BB14 and BB22; the competitor the house plans around | played: +2 |
| Cody Calafiore | runner-up BB16; won BB22 | winner: +4 |
| Danielle Reyes | runner-up BB3; played BB7 | finalist: +3 |
| Vanessa Rousso | third in BB17 | played: +2 |
| Tyler Crispen | runner-up BB20; played BB22 | finalist: +3 |
| Chelsie Baham | won BB26 | winner: +4 |
| Rachel Reilly | played BB12; won BB13 | winner: +4 |
| Xavier Prather | won BB23 | winner: +4 |
| Jun Song | won BB4 | winner: +4 |

The record is the edition's own table, never `LoreLegacy`'s live `wonSeason`. Lore sheets are read live by key from
schema 16, so a later lore edit must not move a saved season's history. The reputation values are read only by H1d's
career reputation (7.5), after B9. H1a only pins the table, and H1c may show the record's words.

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

These are named constants in the edition. H1b reads the first two only. Career reputation and pair risk are H1d's,
after B9 (section 9). They are never retuned in place: a retune is a new edition or a later alumni rules value, for
fresh seasons only.

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
| `tuningRulesVersion` | root | int | 0 | **B9**, the field `BALANCE_BASELINE.md` plans for it (Q2's row: "B9 (`tuningRulesVersion`): any formula change moves the rules-off digests"). B9's first tuning slice defines its first value. If B9 ends up needing none, the field stays 0, which costs far less than a schema 31 | 0 |
| **Dialogue** | — | — | — | **None.** The joint note's §6, final | — |
| AS3's commitment association, **only if** its derivation fails (below) | the unified commitment row | a reference | null | **All-Star**, AS3 | null |

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
`4f01a559`, and reopens only if the owner's approval of the joint note changes it before SCH30's step 2.

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
  the association (if added) null. No `alumni-history` receipt anywhere.
- **From H1b:** `alumniRulesVersion` is 0 or 1. The edition is null at 0, and a known edition at 1. Later slices widen
  the range to the values they define.
- At 0: no `alumni-history` receipt anywhere.
- At 1 and every later value, every receipt:
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
when its cast seats at least one identity-intact, unique alumnus (3.5). So:
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
   through the NPC deal writer (`NpcDeals.Strike`, `Simulation/NpcDeals.cs:519-556`). That writer also records its
   ordinary `deal_accepted` +18 both ways; that record fades as usual.

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
| `RelationshipLedger.RecentImpact` (receipts within two weeks) | a week-1 receipt would count as recent in weeks 1-3 | exclude `alumni-history`: history is not recent. No reader outside the ledger today, but keep it truthful |
| `WeeklyRecap.Movements` (`Runtime/Presentation/WeeklyRecap.cs:256-275`) | would show an NPC's receipt about the player as a week-1 move of their read of you | exclude the type |
| `RelationshipWeb` history column (`Runtime/Presentation/RelationshipWeb.cs:1023-1033`) | shows the player's own edge only | none (no receipt sits there) |
| Every other reader that sums or lists ledger entries (Allegiance, BlockSpeeches, FinalistRead, HouseguestNotes, SeasonAutopsy, WebJurySentiment, YourWeek and others) | — | H1b audits each one: filtered by type, reading trust as intended, or excluded |

**The 512-entry prune.** `Append` drops an edge's oldest entry past 512 (`Simulation/RelationshipLedger.cs:186`), and
a receipt is the oldest. H1b measures the busiest NPC edge in 12-house All-Stars seasons. If it can reach 512, the
prune skips `alumni-history` under alumni rules only; old seasons hold no receipt, so they are unaffected.

### 7.4 A breach ends inherited goodwill (reader rule)

A positive receipt stops counting once the same edge holds a permanent negative entry written this season: a broken
promise, a broken word or a betrayal between the two, as the engine's own breach writers record them. Otherwise an
old partner's betrayal would hurt less than a stranger's. With a +15 receipt, an alliance betrayal (-50) leaves trust
at about 38, outside the "dangerous" band below 35; without it, 33.

- It is a pure function of the saved edge: no write, no new value.
- A ledger entry names no actor. Where the engine records a breach on both edges, the inherited goodwill ends both
  ways: the old partnership is broken either way. H1b lists which breach writers record one way and which both.
- It inherits the engine's knowledge rules. A hidden ballot writes no entry on the holder's edge until it is known.
- Negative receipts are never cancelled: an old grievance does not heal by being betrayed again.

### 7.5 Career reputation and pair risk (H1d, after B9)

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

All of this section is SCH30's. H1b adds only the alumni-on cell (8.7) and the observer's alumni rules 1.

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
  - D2 lands after the bump, built against 29 now and rebased.

  So no save, shipped or not, holds rules 2 at 29, `veto_if` included (validation refuses it below rules 4). Frozen29
  refuses all of it, and the parity matrix (8.3) has no exception. A frozen reader is never widened after the fact:
  if the order changed and rules 2 reached main at 29 first, the reader would be cut only after that version froze.
- **Story rules** 0 to 11: 0 to 9 and 11 today (`StoryRules.IsDefined`, `Simulation/Story/StoryState.cs:291-309`), and
  10 too if the story session defines it at 29 before the reader is cut.
- **Role-moments values**: `veto_hold` deals and the keep-the-block and save ids only in weeks played under Roles, the
  pitch board's three pitchers a week and the recorded impacts, the pawn conversation's and quiet tears' storylines,
  the story favours and grudges, the 92 and 100 storyline ceilings; and R7's and R8's values if they land at 29
  first.

### 8.3 How it is built and proven

- **Build:** carry Frozen28 forward by exactly the live validators' changes since its basis (`76371982`, the 2026-10-18
  re-diff, OLD-SAVE-ACCEPTANCE §6). Between that basis and `b208d46d` the live delta is small: `DialogueRules.cs` (457
  lines) and `DialogueState.cs` (189), about 50 lines across `EpisodeValidation.cs`, `EpisodeLedgerValidation.cs`,
  `EpisodeStoryValidation.cs` and `EpisodeSaveValidation.cs`, and the role-moments predicates they call
  (`HoHPitches.RecordedImpact`, `NpcDeals.IsKeepTheBlockId`, `DealKind.RolesOnly`, `EpisodeEngine.RolesOn` and
  `RolesOnIn`).
  - Reuse Frozen28's components where a domain did not change; give a changed domain a frozen copy.
  - Fork a component only if it cannot be reused cleanly. SCH30 decides after reading `FrozenV28ReadOnly.Core`.
  - If D1 lands on main first, its live changes join the delta (`>= 29` at `DialogueRules.cs:19` and `:97`, and any
    validator line it touches), and the reader is re-diffed against the commit that cuts it.
- **Prove, before the bump, at the live schema 29** (the B+ precedent, OLD-SAVE-ACCEPTANCE §6 step 1):
  `PrepareCurrentPayload` runs `FrozenEpisodeV29.Validate` on every direct-29 load.
  - Every fixture and every copy of the owner's saves passes it, input unchanged.
  - A parity matrix holds it to the live validator: every schema-29 fixture, a Roles opt-in season, a dialogue rules-1
    season (a chip season, if D1 has shipped), a rules-2 payload at 29 (both refuse), and the mutations Frozen28's
    tests use. Both accept or both refuse.
  - A malformed 29 is refused by the dispatch itself, and the test fails if the call is removed.
  - Measure the added load time, as on 2026-10-22.

### 8.4 The upgrade path

```
PrepareCurrentPayload(original):
  30          -> clone, as it is
  29          -> UpgradeV29ToV30(original)                              migrated = true
  1 .. 28     -> UpgradeV29ToV30(UpgradeV28ToV29(the existing chain))   migrated = true
  otherwise   -> refused

UpgradeV29ToV30(original):
  FrozenEpisodeV29.Validate(original)      before any clone or literal
  clone; add every pooled field's off value (5.1) as a hand-written literal:
    "alumniRulesVersion": 0, "alumniCatalogEdition": null, "tuningRulesVersion": 0,
    and the association (if added) null on every commitment row
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
| `Simulation/ShippedRules.cs:74-104` (`Fields`) | — | lists `alumniRulesVersion` and `tuningRulesVersion`; its completeness test moves with it. The edition is content, not a rule field |

### 8.6 The EditMode pin list (the Unity-free subset runs none of it)

From `adding-a-saved-field-needs-a-schema-version` and a grep at `b208d46d`:
- **Schema pins**: about 126 lines in 56 test files name schema 29, and 19 `"schema 29 in memory"` message pins sit
  in `PersistenceV3`, `V4`, `V7` to `V12` and `V22` to `V28MigrationTests`. Replace only on lines that mention
  `schemaVersion`.
- **`ShippedRulesTests.TheLiveSchemaIs29AndAShippedSeasonStartsOnDialogueRules0`** becomes 30. New pins beside it:
  - at SCH30, every fresh season, Regular and All-Stars alike, starts with every pooled field at its off value, and
    its state equals the schema-29 build's apart from the schema number and the off literals;
  - from H1b, a Regular season without alumni still starts at alumni rules 0 with a null edition, and an All-Stars
    season starts at 1 with edition 1.
- **The future-schema constant** in `PersistenceV4MigrationTests` (30 becomes 31). **`AddedSinceV3`** (`:199`) and its
  V4 counterpart name every pooled root field. **`CurrentDispatch_…`** (`PersistenceV3MigrationTests.cs:109`) and
  `CurrentDispatchTraversesFrozenContracts…` (`PersistenceV4MigrationTests.cs:83`) take the new dispatch.
- **The "validated as the save it becomes" chains** in `PersistenceV13`, `V14` and `V15MigrationTests` call
  `UpgradeV29ToV30`.
- **`StripSchema30`** in `PersistenceMigrationTests`, which `StripSchema29` (`:283`) calls first. It strips every
  pooled field, a nested one row by row.
- **`FrozenEpisodeV28IntegrationTests`**, `PersistenceV26`, `V27` and `V28MigrationTests`: chains now end at 30.
- **`OldSaveTrace`**: every one of `UpgradeV29ToV30`'s literals joins `Additions`, and `Newest`
  (`Tests/EditMode/OldSaveTrace.cs:131`) becomes 30. `CurrentOldSaveTrace`'s observer admits the off values at SCH30,
  and H1b widens it to alumni rules 1. `Tools/OldSaves` compiles the file into the historical recorders, so the
  additions stay plain literals over JSON.
- **Dialogue's pins:** none move for a field, because dialogue adds none. No test pins the
  `"Unsupported dialogue schema."` message (a grep at `b208d46d`), and D1's own pins are D1's.
- **`Tools/SimulationTests/SimulationTests.csproj`** compiles the Frozen29 sources beside Frozen28's, and the frozen
  reader keeps a warnings-as-errors compile proof of its own. **`Tools/baseline.txt`** takes the new floors.

### 8.7 Old-save acceptance at 30 (OLD-SAVE-ACCEPTANCE §6 step 5's pattern)

- **The three schema-29 fixtures** (`s29-default-w2-social`, `s29-mode1-allweek-w3-veto-meeting`,
  `s29-mode2-allweek-w3-campaign`) replay through `UpgradeV29ToV30` to their recorded digests. So do all the older
  fixtures. No script, byte or digest changes; the manifest only gains rows.
- **New fixtures written by the SCH30 build**, every writer inert:
  - the default scene (mode 0, alumni 0);
  - a shipped Regular director season (mode 2, alumni 0);
  - a shipped All-Stars director season at alumni rules 0. At SCH30 an All-Stars season plays as it did at 29.
- **The alumni-on cell, written by the H1b build**: a shipped All-Stars director season (mode 2, alumni 1, edition 1,
  eight houseguests, saved mid-season with the Hitmen row live).
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
and the association's null if it is added):
- `BalanceLabGoldens` holds Regular cells only and hashes the lab's rows, not the state, so not one of them may move.
  That is SCH30's regression test, and H1b's again;
- any digest that hashes the whole state moves for the new keys only, and is re-recorded with that reason;
- no draw, sequence, event or log line changes.

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
impressions are. It ships with H1b, and it is measured there, not tuned.

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
- **Who throws** is Will's policy, with Codex's `ShouldThrow` as the reference: to protect a trusted partner on the
  block, never in a final, never with six or fewer left. It is read only when both gates hold, the competition rules
  version and H1d's alumni value. A season below either keeps today's competitions.
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
- **H1b's report** goes to the owner with its PR. If it shows the opening state moving the player's headline rates in
  alumni seasons beyond the lab's seed noise, the lead brings it to the owner before the merge rather than tuning it
  in place.

A retune is a new edition or a later alumni rules value, for fresh seasons only. A saved season keeps its numbers.

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
- **Not claimed:** no command kind (the next free stays 66), no story milestone (10 stays the story session's, 11 is
  Roles), no dialogue rules version (the joint note claims 2 to 5 in dialogue's own field), and no competition rules
  version (5 is PC5's; the throw's is claimed with the competitions topic at H1d).

**With dialogue:**
- **The joint note's final §6 is SCH30's input.** It adds no dialogue field to 5.1, and it fixes 8.2's dialogue scope
  at rules 0 and 1.
- **The order is the joint note's decision 7**, which this plan adopts (D-H6):
  - SCH30 cuts the reader from main as it stands, with dialogue rules 0 and 1;
  - D2 lands after the bump, built against 29 now and rebased onto 30, where the store has the same shape;
  - D1 may land before the bump or after it.

  It is also the alternative the master plan asks P0 to weigh ("landing SCH30 before D2 keeps Frozen29 to rules
  {0,1}"), and the histories-first order needs it: SCH30 does not wait for D2, and the reader is never redone after
  it. A preview build's schema-29 save at rules 2 is refused, with a note in OLD-SAVE-ACCEPTANCE §7. It is never
  admitted by widening a frozen reader after the fact.
- **D1 changes `DialogueRules.cs:19` and `:97` to `schemaVersion >= 29` itself** (the joint note's §6 item 1). It is
  harmless at 29 and saves a conflict in a file both lines touch.
- **The joint note gives the live schema checks to "H1b"** (`EpisodeValidation.cs:88`, `EpisodeSaveValidation.cs:13`).
  It read this plan at `cacc59e0`, where H1b held the bump; they are SCH30's now (8.5).

**With B9:** `tuningRulesVersion` is in 30 at 0, and B9 defines its values. If B9's plan needs another field, it
joins the pool before the list closes.

**With role moments:** R7 (enrolment) and R8 (the holder's offer, a new origin value) wait for B9. If either lands at
29 before SCH30, its values join the frozen reader in the same step.

**With the competitions topic:** PC5 is competition rules 5, and PC6's minigames take 6 or later. Will's throw takes
the next free value when H1d lands, and is measured with PC5's lab runs.

**With the story session** (tell it when it next runs):
- schema 30 is pooled and built here;
- a new ledger receipt type, `alumni-history`, exists;
- no story milestone is used;
- story rules 10 stays theirs, and Frozen29 accepts it only if they define it at 29 before the reader is cut.

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
| **H1a The edition, inert** | `AlumniEditions` with `allstar-histories-1`: the frozen identity table (3.5), the six pairs (3.2) with sources and acts, the career record (3.4), the named constants (3.6). Pure readers: identity resolution, pairs seated in a state, career value. Pins: exact roster, ids and names, pair uniqueness and direction, no RNG or state API, read-only lists, held-out rows absent. Called by nothing in play | none | no | S; stackable | an edition read live from `CastTemplates` or `LoreLegacy` (3.4, 3.5) |
| **SCH30 The pooled schema 30, every writer inert** | The joint note's list is final and adds no dialogue field; step 2 starts once AS3's derivation proof has run (5.1). **Step 1**, at the live schema 29: `FrozenEpisodeV29` on every direct-29 load, scoped to the widest legal schema-29 state (8.2), with its parity proof (8.3). **Step 2**, the bump: every pooled field (5.1) at its off value, `UpgradeV29ToV30` with the hand-written literals, `PrepareV29Payload`, the live literals made bounds (8.5), live validation of the off values (5.3), the pin list (8.6), the SCH30 fixtures (8.7), and the regression that ordinary seasons are unchanged (8.8). No writer: `ApplyFresh` is untouched | schema 30: every field in 5.1 | no | L-XL; qualified alone, one or two sets (step 1 can qualify first) | the reader's scope (8.2); `DialogueRules.On` (8.5); EditMode-only pins; a field found missing later costs a schema 31 |
| **H1b The histories' opening state, alumni rules 1** | `EnableAlumniHistories` as `ApplyFresh`'s last step (section 6); `AlumniRules.Histories`; the writers (7.1, 7.2, 7.6); the receipts' readers and the breach rule (7.3, 7.4); the prune check (7.3); validation of alumni rules 1 (5.3); the observer's alumni rules 1 and the alumni-on fixture (8.6, 8.7); the lab option for the player on an alumni card and the balance report (section 9) | no new field: new values in SCH30's | yes, in fresh seasons with alumni: opening relationships and behaviour | L; one or two sets. It adds no field, but it enrols fresh seasons, so a set of its own is recommended | receipts in week-keyed readers (7.3); the prune (7.3); the report's size (section 9) |
| **H1c Past games, shown** (optional) | A read-only line on the notebook's houseguest page for a seated pair: "Played together before: Big Brother 16, the Hitmen, to the final two". Wording comes from the edition, game register only. Captions are a contract; checked at Standard and Largest text | none | yes | S; stackable after H1b | the real-alumni rule; Inter's short-box truncation |
| **H1d The parts that move balance** (after B9) | Under one new alumni rules value, for fresh seasons only, measured with B9: career reputation and pair risk (7.5); a trusted, bigger threat as an early shield in votes; Will's conditional throw through the competition rules (section 9, with the competitions topic); per-alumnus NPC repertoires; the web's All-Star stat overrides, if the owner wants them (never `personalityProfile`) | values only (alumni rules, and a competition rules value for the throw) | yes | M-L; one or two sets | each one moves B9 numbers; the throw re-rolls boards in the seasons that start under its competition rules |

**Then the strategic campaigns**, as the feature research sliced them (`Research/AllStars`; the design review's five
gates). They follow H1b.
- **AS1a**, the bio audit, is the master plan's Stack 1 slice, already approved (story plan 20, decision 10), and
  outside this track. Rachel's displayed bio quotes a romance-related catchphrase (`Simulation/CastTemplates.cs:236`),
  and two other bios were flagged. It also keys `WebIntroductions`' alumni lines by template id. First freeze the
  schema 2 to 15 lore cast in `UpgradeV15ToV16`, or the 48 old-save cases turn red (OLD-SAVE-ACCEPTANCE §3).
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

## 12. Decisions for the lead, two of them the owner's

| # | Question | Recommendation |
|---|---|---|
| D-H1 (owner) | Edition 1's scope: shared Big Brother seasons only, or also Reindeer Games, Snake in the Grass and The Traitors? | **Big Brother seasons only** (3.3). Other shows are allowed by the owner's rule and can come as a later edition; owner's call if wanted |
| D-H2 | What enrols a season: the cast (at least one intact, unique alumnus) or the All-Stars tab? | **The cast** (section 6): the state stores no roster, and `ApplyFresh` takes only the state |
| D-H3 | Pairs with the player on an alumni card | **The NPC's view only**, through one receipt; no `Move`, no `Change`, no arc, no seeded row (7.2) |
| D-H4 | Will and Janelle's row | **The eviction only**; the "close relationship" reading is dropped (3.3) |
| D-H5 (owner) | An in-game on/off switch for histories (Codex's `playerHistoriesEnabled`) | **None for now**. If the owner wants one, it is alumni rules 0 on a fresh season and needs no field |
| D-H6 | Order against dialogue's D2 for the frozen reader | **The joint note's decision 7: SCH30 first.** The reader is cut from main as it stands, with dialogue rules 0 and 1, and D2 lands after the bump, rebased onto 30 (8.2, section 10). This replaces H0's first answer ("D2 on main first"), which the histories-first order overtakes. The joint note leaves the order to the lead, so the lead confirms it |
| D-H7 | Confirm the pooled schema-30 claim with the story session, dialogue and B9 | Dialogue has accepted it (the joint note's §6 and §10). Confirm with the story session and B9 before SCH30's step 2, and record it in section 10 (the master plan's decision 6) |
| D-H8 | Where the line falls for "the parts that move balance wait for B9" | **New terms in a decision rule wait** (career reputation, pair risk, the vote shield, Will's throw, the repertoires, the stats). **The opening state ships with H1b**, measured and not tuned (section 9) |
| D-H9 | AS3's commitment association, if its derivation proof has not run when SCH30's list closes | **Run the proof first**: it needs no Unity. If it cannot run in time, the lead chooses between adding the null field to 30 as insurance and accepting a later schema 31 for it |
| D-H10 | Reserve B9's `tuningRulesVersion` in 30 before B9 has a plan | **Yes**, at 0, with validation accepting only 0 until B9 defines a value. An unused int costs far less than a schema 31 (5.1) |

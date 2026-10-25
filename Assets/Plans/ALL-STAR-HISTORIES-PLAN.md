# All-Star player histories: the alumni's past games, rebuilt

Design plan, slice H0 (documents only), 2026-10-25, on `claude/all-star-histories` from main `b208d46d` (role moments
R0 to R6 and R5b, the dialogue V2 foundation, schema 29). No code changes with this slice.

**Evidence base**
- Unity: `D:/GamesimAllStars` at `b208d46d`. Code paths are relative to `Assets/Gamesim/`. Documents at the repository
  root are named bare (`BALANCE_BASELINE.md`).
- Codex's isolated candidate, `origin/codex/allstar-characterization-20261012` (`3a00079e`, `24baba83`, on the frozen
  snapshot `0a1e00b9`), read with `git show` and `git diff`. **It is a reference to port, not a branch to merge.** Its
  files are named by path on that branch (`Simulation/AlumniCatalog.cs`, `Assets/Plans/ALLSTAR-RESEARCH.md`).
- Research: `Research/AllStars/README.md`, the content brief and design review of 2026-10-21; the cast templates
  (`Simulation/CastTemplates.cs:177-249`) and the legacy lore sheets (`Simulation/Story/LoreLegacy.cs:8-119`).
- The save gate: `Assets/Plans/OLD-SAVE-ACCEPTANCE.md` (sections 1, 6 and 7); `handoffs/OWNER-DECISIONS-2026-10-22.md`
  §4-§5; `Assets/Plans/ROLE-MOMENTS-PLAN.md` §4.6.

**Status words** follow ROLE-MOMENTS-PLAN: *shipped*, *gated*, *intent*, *missing*.

---

## 0. The short version

- **What:** in a fresh season that seats the real alumni, the house starts with what the alumni did to and with each
  other on the show. Old partners start closer, people who were nominated or evicted by another start warier of them,
  a winner's record makes them a bigger target, and the Hitmen (Derrick and Cody) walk in with their final two.
- **What it is not:** no grudge, act or arc of this season, no romance or personal life, no knowledge a houseguest
  could not have, no guaranteed alliance and no forced result. Everything after week one is played as usual.
- **Facts:** only on-show, public game acts already in the project's research (section 3). Edition 1 uses six pairs
  from shared Big Brother seasons and a career record for all twelve.
- **Saved state:** two root fields at **schema 30**, `alumniRulesVersion` and `alumniCatalogEdition`. No per-pair
  field: the pairs live in the existing relationship scores and ledger (section 5).
- **Writers:** `RelationshipLedger.Move` for the shared standing of an NPC pair, `RelationshipLedger.RecordOneWay` for
  each directed part, the NPC deal writer for the final two. Never `EpisodeEngine.Change`, never a roll (section 7).
- **When:** once, at the end of `ShippedRules.ApplyFresh`, on an unstarted season, before its first save. Never on load,
  migration, recovery or import (section 6).
- **Old saves:** a whole-contract `FrozenEpisodeV29` reader that accepts every schema-29 shape main can write, then
  `UpgradeV29ToV30` adds the two off literals. Old seasons replay byte for byte (section 8).
- **Balance:** the histories move balance in seasons with alumni, and only there. They are measured with B9, not tuned
  here (section 9).
- **Slices:** H1a (the edition, inert), H1b (schema 30, qualified alone), then the optional H1c (shown) and H1d (play
  styles, after B9), then the campaigns AS2 (framework), AS3 (Cody) and on (section 11).

---

## 1. The owner's decisions, 2026-10-25 (binding)

Recorded as given to this work's brief:

1. **Implement now.**
2. **Keep the twelve alumni's real names.**
3. **Keep the alumni photos and the UMA resemblance as they are.**
4. **Build the All-Star player histories first, before the strategic campaigns, "rebuilt properly" with their own
   schema.** This is the owner's 15 Oct wish (`handoffs/HANDOFF-2026-10-18-claude.md`, "Later work" and item 3:
   own schema, NPC-to-NPC changes only through RelationshipLedger; `Research/AllStars/README.md`).
5. **Use only on-show, public game facts already present in the project's research** (`Research/AllStars`, the cast
   templates and the Codex candidate branch). **Never add off-show personal-life details about the real people.**

What these settle, and what they leave:
- The real-person questions the feature research behind this brief put to the owner (names, photos, likeness) are
  closed: all stay. That research's AS1 shrinks to the bio audit and keying `WebIntroductions` by template id, if
  the owner wants them (section 11).
- The 15 Oct "research stays out of the public repo" was replaced on 22 Oct (OWNER-DECISIONS-2026-10-22 §1); decision 5
  counts the Codex branch's research as project research.
- The standing real-alumni rule still holds: game arcs only, no romance, secret, conduct or removal, for every
  identity-intact alumni card, the player's included (`Simulation/Story/StoryPeople.cs:7-15,43-49`).
- Still open, and not this plan's: the campaigns' route (dialogue layer or story catalogue), their pacing caps,
  personalised juror wording, the content-version policy for card and lore edits, the web stat overrides, reach.
  Section 12 lists them where a slice needs them.

---

## 2. What the player histories are

Three things, all read from one immutable catalogue edition (section 3):

| # | Part | What changes in a fresh season with alumni | Status today |
|---|---|---|---|
| 1 | **Prior relationships** between two seated alumni | the pair's opening standing (both ways, shared alliances only) and each side's trust in the other | missing on main; Codex: candidate only |
| 2 | **Career reputation** | how big a threat each alumnus's record makes them look to the house | missing; Codex: candidate only |
| 3 | **One opening commitment**: Derrick and Cody's final two | one ordinary `final_two` deal between them when both are NPCs | missing; Codex: candidate only |

What the player meets: the opening relationships and the NPCs' behaviour (who allies, deals and targets whom). A
read-only "past games" line is a later, optional slice (H1c).

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
shared Big Brother seasons. Numbers are provisional (3.6) and are set in H1a, measured with B9.

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
schema 16, so a later lore edit must not move a saved season's history.

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

These are named constants in the edition. They are never retuned in place: a retune is a new edition or alumni rules
2, for fresh seasons only.

---

## 4. The Codex candidate: kept, rebuilt, deferred or dropped

| Candidate piece | Verdict | Where |
|---|---|---|
| Immutable catalogue with an edition id, provenance per fact, read-only lists, no RNG or state API (`AlumniCatalog.cs`) | **Kept, re-derived** from the research under section 3's rule, with the frozen identity table | H1a |
| Exact template identity, renamed and duplicate alumni excluded (`AlumniHistory.Identities`) | **Kept** | H1a |
| Honorific-aware names (`CharacterNames`) | Already on main | — |
| Directed pair priors as a relationship score plus `priorTrustOffset` (`AlumniHistory.Seed` writes `edge.score` directly) | **Rebuilt**: shared standing through `Move`, directed parts as one-way trust receipts. The direct score write and the nested `RelationshipState.priorTrustOffset` field are dropped | H1b |
| Root `alumniRulesVersion` and `alumniCatalogVersion` | **Kept**, renamed `alumniCatalogEdition`, at schema 30 | H1b |
| Root `playerHistoriesEnabled` and the cast screen's on/off choice | **Dropped** for now: histories are part of an alumni season. An off switch, if the owner wants one, is a fresh season at alumni rules 0 and needs no field (D-H5) | — |
| Public `prior-history` and `prior-career` story facts (`Knowledge.SeedAlumniFact`) | **Dropped**: the record is public, so readers derive it from the edition and the cast; nothing is stored | — |
| Initialisation once, on an unplayed season, before the first save, without randomness | **Kept** | H1b |
| `CareerRisk` in reputation threat | **Kept**, from the edition's record (3.4) rather than a hand list | H1b |
| `KnownPairRisk` in alliance threat | **Kept** for the two shared alliances, inside the cap | H1b |
| `InvalidatePositivePrior` on a known current breach | **Kept as a reader rule** (7.4): no write, no field | H1b |
| Derrick and Cody's opening final two, NPC pair (`NpcDeals.SeedOpeningPair`) | **Kept**: one ordinary row through the existing writer | H1b |
| The same with the player on either card: an opening proposal that takes one of the week's three offer seats (`SeedOpeningProposal`, the `fillOpeningReservation` change to `Propose`) | **Dropped**: the NPC's trust receipt about the player feeds the ordinary offer pass, which decides | — |
| `ShieldValue` in eviction votes (`WebEvictionVoting` factor `alumniShield`) | **Deferred** to H1d, after B9 | H1d |
| Will's conditional throw (`ShouldThrow`, score share capped at .5) | **Deferred** to H1d, after B9 and with the competitions topic | H1d |
| Per-alumnus NPC repertoires (`AlumniStrategy.Repertoire`) | **Deferred** to H1d, after B9 | H1d |
| The "past games" conversation (`AskForIntel` with `history:<fact>:<approach>` text, `EpisodeEvent.historyCommandRef`, authored replies through `Change`) | **Deferred** to the campaigns or the dialogue layer; the event field is dropped | AS-series |
| `AlumniVoice` ordinary lines | **Deferred**; `WebIntroductions` already voices the alumni | — |
| `GAMESIM_ALLSTAR_CANDIDATE` compile symbol | **Dropped**: behaviour is gated by saved rules | — |
| The candidate's "schema 29", refusing every schema 1-28 save | **Dropped**: schema 30 with the full upgrade chain | H1b |
| About 1,060 lines of EditMode tests and one PlayMode file | **Reference** for cases (identity, copy isolation, no RNG on refusal, catalogue pins), rewritten against the new design | H1a, H1b |

---

## 5. Saved state: schema 30

### 5.1 Two root fields

| Field | Type | Off value (every migrated season, and every fresh season without an alumnus) | On value |
|---|---|---|---|
| `alumniRulesVersion` | int | 0 | 1 (`AlumniRules.Histories`) |
| `alumniCatalogEdition` | string | null | `"allstar-histories-1"` |

**No per-pair field.** The design does not need one: each pair's shared standing is an ordinary score, and each
directed part is a one-way ledger receipt on the existing edge. That avoids Codex's `priorTrustOffset`, a nested-DTO
field that every relationship row of every save would have to migrate.

### 5.2 Existing storage, and the new values in it

| What | Where it lives | New value |
|---|---|---|
| Shared standing | `relationships[].score`, both directions | none |
| Directed trust | `relationships[].events[]`, one receipt on the holder's edge, `decayable = false` | event type `alumni-history` |
| Hitmen final two | `deals`: a `final_two` is not a vote-family kind (`UnifiedVoteFamilyValidation.cs:637`), so even in mode 2 it is an ordinary deal row, `deal-npc-<sequence>`, active, open-ended | none |
| Career reputation, pair risk, the breach rule | not stored: pure readers of the edition, the cast and the ledger | none |

No command kind (the next free stays 66), no story milestone, no dialogue rules version, no event kind and no
storyline.

### 5.3 Live validation at schema 30

- `alumniRulesVersion` is 0 or 1. The edition is null at 0, and a known edition at 1.
- At 0: no `alumni-history` receipt anywhere.
- At 1, every receipt:
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

---

## 6. Fresh-season initialisation

**Where.** The last step of `ShippedRules.ApplyFresh` (`Simulation/ShippedRules.cs:26-65`):
`EpisodeEngine.EnableAlumniHistories(fresh)`.
- It comes after the commitment rules and the unified mode, so the Hitmen row is written under the season's own deal
  rules; and after agency's first impressions, so standing adds to them.
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

### 7.5 Career reputation and pair risk

- **Career reputation**: the edition's record value (3.4), added inside `ReputationThreat` before its clamp to 0-15.
  Every evaluator sees it, because the record is public. A newcomer player has none.
- **Pair risk**: the two shared alliances, while both members are active, count to every evaluator outside the pair
  as a bloc of two, unless a current alliance the evaluator can see already holds both. It is added inside
  `AllianceThreat`'s cap of 20.
- Both are read only under alumni rules 1.

### 7.6 The Hitmen row after week one

It is an ordinary `final_two`: it shows wherever deals show, it can be kept or broken under the commitment rules, and
it is settled by its existing owners. Validation accepts it as it would any week-1 NPC deal. It is not tagged as
history and needs no tag. An offer pass that would strike a second final two between them refuses as it already does.

---

## 8. Old saves: the frozen V29 reader and the upgrade

### 8.1 Why the reader is a whole contract

Schema 29 gained values after it froze, without a schema number, as schema 28 gained mode 2 (OLD-SAVE-ACCEPTANCE §6:
"Mode 2 joined schema 28 without a version, so no overlay on 27 can describe it"):
- story rules 11 (`StoryRules.Roles`);
- the role moments' `veto_hold` deals, the `deal-keep-` and `deal-save-` ids and the pitch board's bounds
  (`Simulation/EpisodeValidation.cs`, `Simulation/EpisodeLedgerValidation.cs`, ROLE-MOMENTS-PLAN §4.1, §4.6);
- if the dialogue V2 workflow's D2 lands at 29, dialogue rules 2.

The first two sit inside collections Frozen28 already validates, and Frozen28 refuses them: its pinned story rules end
at Threads. An overlay that projects a 29 payload down to 28 cannot describe them. The dialogue store, at rules 0 and
1 from the freeze and possibly 2, is a whole domain of its own that Frozen28 never saw. So `FrozenEpisodeV29` is a
whole contract in Frozen28's form: a frozen 29 DTO hydrated from the JSON, and a frozen copy of the whole schema-29
validator. It uses BCL and Newtonsoft only, never a live DTO.

### 8.2 Scope: every schema-29 shape main can write

The reader accepts exactly what the live schema-29 validator accepts at the commit that cuts it (H1b step 1),
including:
- **The schema-29 root**: every schema-28 field, `dialogueRulesVersion`, `dialogue` (episodes, grants and veto
  occasions) and `conditionalVeto` on every commitment row.
- **Commitment modes 0, 1 and 2; hearing rules 0 and 1**; everything Frozen28 covers (C0-C9, Wave D, the economy, the
  finale, the vote family).
- **Dialogue rules 0** (empty store, every shipped season), **1** (the pilots, and the dialogue V2 workflow's D1
  preview, which writes 1 into new schema-29 saves) and **2**, if D2's rules-2 foundation is on main by then (section 10).
- **Story rules** 0 to 9 and 11 (`StoryRules.IsDefined`, `Simulation/Story/StoryState.cs:291-309`). Also 10, if the
  story session defines it at 29 first.
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
  - Fork a component only if it cannot be reused cleanly. H1b decides after reading `FrozenV28ReadOnly.Core`.
- **Prove, before the bump, at the live schema 29** (the B+ precedent, OLD-SAVE-ACCEPTANCE §6 step 1):
  `PrepareCurrentPayload` runs `FrozenEpisodeV29.Validate` on every direct-29 load.
  - Every fixture and every copy of the owner's saves passes it, input unchanged.
  - A parity matrix holds it to the live validator: every schema-29 fixture, a Roles opt-in season, a dialogue rules-1
    season, and the mutations Frozen28's tests use. Both accept or both refuse.
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
  clone; add "alumniRulesVersion": 0 and "alumniCatalogEdition": null as hand-written literals; schemaVersion = 30
```

- **`PrepareV29Payload`**: the frozen dispatch that returns a schema-29 payload for 1 to 29, per the checklist.
- **Nothing is inferred:** dialogue rules, story rules, commitment mode and every role-moments value carry over as
  they are. An old All-Stars season stays at alumni rules 0 and plays exactly as it did.
- `EpisodeSaveStore` is unchanged: checksum first, then dispatch. Load, backup checks, recovery and the staged file
  all use `TryRead`.
- A schema-29 build refuses a schema-30 save. Copy the owner's save folder in full before the play copy updates, as
  for 29.

### 8.5 Live code pinned to 29

| Where | Today | At schema 30 |
|---|---|---|
| `Simulation/EpisodeState.cs:247` | `schemaVersion = 29` | 30, plus the two fields |
| `Simulation/EpisodeValidation.cs:88` | `!= 29` | `!= 30` |
| `Runtime/Persistence/EpisodeSaveValidation.cs:13` | `== 29` | `== 30` |
| `Simulation/DialogueRules.cs:19` (`On`) and `:97` (`TryValidate`) | `== 29` and `!= 29` | `>= 29`: the version that introduced the store. Pinned to 29, the bump would switch dialogue off for good, and `TryValidate` would refuse every season |
| `Simulation/ShippedRules.cs:74-104` (`Fields`) | — | lists `alumniRulesVersion`; its completeness test moves with it. The edition is content, not a rule field |

### 8.6 The EditMode pin list (the Unity-free subset runs none of it)

From `adding-a-saved-field-needs-a-schema-version` and a grep at `b208d46d`:
- **Schema pins**: about 126 lines in 56 test files name schema 29, and 19 `"schema 29 in memory"` message pins sit
  in `PersistenceV3`, `V4`, `V7` to `V12` and `V22` to `V28MigrationTests`. Replace only on lines that mention
  `schemaVersion`.
- **`ShippedRulesTests.TheLiveSchemaIs29AndAShippedSeasonStartsOnDialogueRules0`** becomes 30. New pins beside it:
  - a Regular season starts at alumni rules 0 with a null edition;
  - an All-Stars season starts at 1 with edition 1;
  - a fresh Regular season's state equals the schema-29 build's apart from the schema number and the two literals.
- **The future-schema constant** in `PersistenceV4MigrationTests` (30 becomes 31). **`AddedSinceV3`** (`:199`) and its
  V4 counterpart name both root fields. **`CurrentDispatch_…`** (`PersistenceV3MigrationTests.cs:109`) and
  `CurrentDispatchTraversesFrozenContracts…` (`PersistenceV4MigrationTests.cs:83`) take the new dispatch.
- **The "validated as the save it becomes" chains** in `PersistenceV13`, `V14` and `V15MigrationTests` call
  `UpgradeV29ToV30`.
- **`StripSchema30`** in `PersistenceMigrationTests`, which `StripSchema29` (`:283`) calls first.
- **`FrozenEpisodeV28IntegrationTests`**, `PersistenceV26`, `V27` and `V28MigrationTests`: chains now end at 30.
- **`OldSaveTrace`**: `UpgradeV29ToV30`'s two literals join `Additions`, and `Newest` (`Tests/EditMode/OldSaveTrace.cs:131`)
  becomes 30. `CurrentOldSaveTrace`'s observer admits alumni rules 0 and 1. `Tools/OldSaves` compiles the file into
  the historical recorders, so the additions stay plain literals over JSON.
- **`Tools/SimulationTests/SimulationTests.csproj`** compiles the Frozen29 sources beside Frozen28's, and the frozen
  reader keeps a warnings-as-errors compile proof of its own. **`Tools/baseline.txt`** takes the new floors.

### 8.7 Old-save acceptance at 30 (OLD-SAVE-ACCEPTANCE §6 step 5's pattern)

- **The three schema-29 fixtures** (`s29-default-w2-social`, `s29-mode1-allweek-w3-veto-meeting`,
  `s29-mode2-allweek-w3-campaign`) replay through `UpgradeV29ToV30` to their recorded digests. So do all the older
  fixtures. No script, byte or digest changes; the manifest only gains rows.
- **New fixtures written by the 30 build**:
  - the default scene (mode 0, alumni 0);
  - a shipped Regular director season (mode 2, alumni 0);
  - **a shipped All-Stars director season** (mode 2, alumni 1, edition 1, eight houseguests, saved mid-season with the
    Hitmen row live). This is the alumni-on cell.
- **Extra schema-29 cells first.** Before the bump, the 29 build records any shipped schema-29 cell that has no
  fixture yet. If D1's preview ships dialogue rules 1 at 29 before H1b, its own build writes that fixture; the
  dialogue workflow extends `CurrentOldSaveTrace`'s rules-0-only boundary for it (OLD-SAVE-ACCEPTANCE §7).
- **A sweep of copies of the owner's saves** on the 30 build, never the originals: load, migrate, replay, save at 30,
  reload, recover.

### 8.8 Fresh seasons without alumni

They are unchanged apart from the schema number and `alumniRulesVersion: 0`, `alumniCatalogEdition: null`:
- `BalanceLabGoldens` holds Regular cells only and hashes the lab's rows, not the state, so not one of them may move.
  That is H1b's regression test;
- any digest that hashes the whole state moves for the three keys only, and is re-recorded with that reason;
- no draw, sequence, event or log line changes.

---

## 9. Balance

**The histories move balance in seasons that seat alumni, and only there. They are measured later with B9, not tuned
here.** B9 is unresolved: the studier wins about 95% of weekly competitions against a 60-70% target, and 73.5% of
8-house seasons against 25-35% (`BALANCE_BASELINE.md`, Q3).

What moves:
- the opening standings of the Hitmen and the Committee;
- trust, and through it alliance desire, information deals and the reputation bands;
- the player's deal odds with an alumnus NPC when the player plays an alumni card;
- reputation threat, by career;
- alliance threat, by pair risk;
- the Hitmen row's protection and its breach.

The likeliest effect: a newcomer player in an All-Stars house reads as the smallest threat in the room. Q4 (the
newcomer's nomination rate) and the winner rates move.

How it is measured:
- **`BalanceLab`'s All-Stars grids at 8 and 12** (`Tests/EditMode/BalanceLabReports.cs:26,66,104`), paired seeds,
  alumni rules 1 against 0. A harness may set rules 0 on a fresh season for the control; shipping never does.
- **NPC-to-NPC traffic through fixtures.** Sweeps measure the engine's decisions, not the NPC world's week, so
  NPC-to-NPC outcomes are pinned with fixtures (`InstallDiaryFixture` style), never from season walks.
- **Not stacked unmeasured** with agency's first impressions: the report shows both together and the histories alone.
- **Pacing**: the histories add no interaction, so the 45-minute target for 8 players is unaffected. Report it anyway.

A retune is a new edition or alumni rules 2, for fresh seasons only. A saved season keeps its numbers.

---

## 10. Claims and coordination

**Schema 30: claimed here, 2026-10-25 (H0), for the All-Star histories.** It covers `alumniRulesVersion` and
`alumniCatalogEdition` on the root, migrated through `FrozenEpisodeV29`.
- **Provisional until confirmed** with the story session and the sessions running dialogue V2
  (`claude/dialogue-v2`) and B9. The lead records the confirmation here. A peer session's decision needs the owner,
  recorded as "delegated call, confirmed by the owner".
- **Dialogue V2 needs no schema**: D1 (the preview opt-in, rules 1) and D2 (the rules-2 foundation) stay inside the
  schema-29 `DialogueState` store.
- **Later fields take 31 and on, in landing order**: B9's planned `tuningRulesVersion` (`BALANCE_BASELINE.md`, Q2's
  row), a field for AS3's commitment association if derivation fails, and any role-moments record that needs more
  than the existing store.
- **If another persisted field must land before H1b**, the sessions agree first who takes 30. Whoever lands second
  renumbers before landing, never after. A step that adds a persisted field is qualified alone (OWNER-DECISIONS §3's
  amendment).
- **Not claimed**: no command kind (the next free stays 66), no story milestone (10 stays the story session's, 11 is
  Roles), no dialogue rules version.

**With dialogue V2** (tell its workflow before H1b starts):
- **The frozen V29 reader copies the dialogue validator as main has it when H1b cuts the reader.** D2 should land on
  main first. If it cannot, D2 rebases onto schema 30, where its rules-2 shape lives in the same store, and preview
  builds' schema-29 saves at rules 2 are refused, with a note in OLD-SAVE-ACCEPTANCE §7. They are never admitted by
  widening a frozen reader after the fact.
- **Ask D1 to change `DialogueRules.cs:19` and `:97` to `schemaVersion >= 29` itself.** It is harmless at 29 and
  saves a conflict in a file both lines touch.

**With role moments**: R7 (enrolment) and R8 (the holder's offer, a new origin value) wait for B9. If either lands at
29 before H1b, its values join the frozen reader in the same step.

**With the story session**: tell it when it next runs: schema 30 is the histories', a new ledger receipt type
`alumni-history` exists, and no story milestone is used.

---

## 11. Slices

Each slice is built, run in full on the suites it touches, independently reviewed, committed and pushed. Stacking
follows OWNER-DECISIONS §3's amendment; H1b adds a field and is qualified alone. Proof inside the slices, before the
lead's full sets: the offline compile (`Tools/offline-compile.ps1 -WithoutUma`), `dotnet test Tools/SimulationTests`,
EditMode fixtures under Unity's Mono where feasible, and careful reading.

| Slice | Scope | Persisted | Player-visible | Size, qualification | Risks |
|---|---|---|---|---|---|
| **H0** | This plan | none | no | XS; review only | — |
| **H1a The edition, inert** | `AlumniEditions` with `allstar-histories-1`: the frozen identity table (3.5), the six pairs (3.2) with sources and acts, the career record (3.4), the named constants (3.6). Pure readers: identity resolution, pairs seated in a state, career value. Pins: exact roster, ids and names, pair uniqueness and direction, no RNG or state API, read-only lists, held-out rows absent. Called by nothing in play | none | no | S; stackable | an edition read live from `CastTemplates` or `LoreLegacy` (3.4, 3.5) |
| **H1b The histories, schema 30** | Step 1, at the live schema 29: `FrozenEpisodeV29` on every direct-29 load, with its parity proof (8.3). Step 2, the bump: the two fields, `UpgradeV29ToV30`, `PrepareV29Payload`, the live pins (8.5), `EnableAlumniHistories` in `ApplyFresh` (section 6), the writers (7.1-7.2, 7.6), the readers (7.3-7.5), live validation (5.3), the pin list (8.6), the fixtures (8.7), the balance report (section 9) | schema 30: two root fields | yes, in fresh seasons with alumni: opening relationships and behaviour | L-XL; qualified alone, likely two sets (step 1 can qualify first) | the reader's scope (8.2); `DialogueRules.On` (8.5); EditMode-only pins; receipts in week-keyed readers (7.3); the prune (7.3) |
| **H1c Past games, shown** (optional) | A read-only line on the notebook's houseguest page for a seated pair: "Played together before: Big Brother 16, the Hitmen, to the final two". Wording comes from the edition, game register only. Captions are a contract; checked at Standard and Largest text | none | yes | S; stackable after H1b | the real-alumni rule; Inter's short-box truncation |
| **H1d Play styles** (optional, after B9) | Each under alumni rules 2, for fresh seasons only, measured with B9: Will's conditional throw (with the competitions topic), a trusted bigger threat as an early shield in votes, per-alumnus NPC repertoires, and the web's All-Star stat overrides if the owner wants them (never `personalityProfile`) | rules value only | yes | M; one set | each one moves B9 numbers |

**Then the strategic campaigns**, as the feature research sliced them (`Research/AllStars`; the design review's five
gates):
- **AS1** (reduced, the owner's call): the bio audit under the real-alumni rule. Rachel's displayed bio quotes a
  romance-related catchphrase (`Simulation/CastTemplates.cs:236`); two other bios were flagged. Also key
  `WebIntroductions`' alumni lines by template id. Names, photos and the UMA likeness stay. First freeze the schema 2
  to 15 lore cast in `UpgradeV15ToV16`, or the 48 old-save cases turn red (OLD-SAVE-ACCEPTANCE §3).
- **AS2 Campaign framework, inert.** First record the route: the dialogue layer riding dialogue V2's enrolment, or a
  story milestone above 11. Then:
  - the governor (at most 2 a season, 1 active, 1 opening a week, once per alumnus, decline final);
  - close-on-departure endings, revalidation at each choice, a read-only settlement reader;
  - eligibility keyed by template id through H1a's identity rule.
- **AS3 Cody: One Vote at a Time.** One targeted, consented canonical vote promise with a single debit, settled from
  `UnifiedVoteObligations`, with a private recap. Its commitment association is derived if provably unique; otherwise
  it is a field at schema 31, qualified alone.
- **AS4-AS6**: Danielle, Janelle and Xavier, each naming its command, receipt, visibility reader and cost first.
- **AS7**: enrolment and measurement.
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
| D-H6 | Order against dialogue V2's D2 for the frozen reader | **D2 on main before H1b cuts the reader**; otherwise D2 rebases onto 30 (section 10) |
| D-H7 | Confirm the schema-30 claim with the story session, dialogue V2 and B9 | Confirm before H1b's step 2; record it in section 10 |

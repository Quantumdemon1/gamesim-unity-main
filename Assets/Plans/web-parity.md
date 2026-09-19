# Porting plan: what the web build still does better

Written against `Quantumdemon1/gamesim` — the reference source itself, not the markdown appendices
this port worked from until now. 1,157 files: 557 components, 147 systems, 80 contexts, 41 utils,
40 hooks, 35 avatar, 28 models, 23 game-states, 77 test files.

Every claim here comes from reading that tree and grepping this one. Where a number was checked it is
given; where something was not read it says so.

---

## 1. The organising insight

**This port has a habit of shipping the consumer of a system without the producer.** Four times:

| system | consumer here | producer here |
| --- | --- | --- |
| alliances | `WebVotingBlocs` coordinates blocs from them | only in Phase C |
| promises | `WebEvictionVoting` weighs them | only in Phase C |
| **deals** | `WebEvictionVoting.DealObligation` / `PairDealValue` weigh them | **never** |
| **storylines** | `WebFinalSpeeches` lets a finalist cite them | **never** |

`deals.Add` and `new WebVoteDeal` appear nowhere in `Assets/`. The eviction vote asks every houseguest
what deals oblige them, gets an empty list, and has since it was written.

`WebSaveImporter.EmptyStateSlots` is the same finding from the other direction — the list of web save
fields this port discards on import reads as an inventory of the gap:

    deals · houseEvents · activeStorylines · activeModifiers · npcMemories · pendingNPCProposals
    playerPerceptions · threatMatrix · playerPersona · grudgeLedger · pendingStorylineEvent
    lastEavesdropIntel · phaseEventSocialBonus · phaseEventCompBonus · weekFocusModifier

These are the cheapest quality in the project: the hard, well-tested half already exists.

## 2. The constraint on everything below

This port is ahead of the reference on four things, and anything ported has to arrive under them.
That, not file size, is what makes these expensive.

- **Determinism.** Every draw goes through `EpisodeState.randomState`. The reference calls
  `Math.random()` freely — including in `buy_action_point` and `selectTalkTarget`. Replays here are
  reproducible; theirs are not.
- **The save contract.** Nine schema versions with frozen validators, migrations and fixture sweeps.
  `SaveJson.CheckDtoShape` matches stored objects field-for-field, so **any new persisted field forces
  a schema version**. Batch them.
- **Rules versioning.** `blocRulesStartWeek`, `npcSocial.rulesStartWeek`, `socialBudgetRulesStartWeek`
  let rules change without invalidating a season in progress. No counterpart in the reference.
- **Tests.** 931 EditMode + 148 PlayMode against 77 test files.

Read `memory/removing-a-command-re-rolls-the-season.md` before touching anything that draws.

---

## Tier 0 — Not really ports (days, not weeks)

### 0.1 The shipped cast does not match the reference

`src/data/character-templates.ts` carries every houseguest's full card and `CastTemplates` disagrees
for most of them:

| | reference | this port |
| --- | --- | --- |
| Alex Chen | Marketing Executive · Strategic + Social | Software Architect · Analytical + Manipulative |
| Jordan Taylor | Sales Representative · Social + Sneaky | Sales Representative · Charming + Social |
| Quinn Martinez | Social Media Influencer · Confrontational + Social | Content Creator · Charming + Deceptive |
| Avery Thompson | Police Officer · Loyal + Competitive | Firefighter · Loyal + Stubborn |
| Sam Williams | Restaurant Owner · Strategic + Loyal | Site Foreman · Competitive + Loyal |
| Blake Peterson | Architect · Analytical + Sneaky | Night Auditor · Introverted + Sneaky |

**Seven of twelve have different traits; nine have different occupations.** Traits are not cosmetic —
`WebTraits.CreateStats` derives the whole stat block from them and `NpcSocialActions.Repertoire` reads
the lead trait to choose behaviour. These houseguests play *and* act differently from the same people
in the reference.

All twelve also carry `hometown` and `bio`. `CharacterDraft.From` currently says those are blank
because "the cast table has never held either" — true of this port's table, wrong about the source.
That comment goes, and `CastTemplates.Template` gains the two fields, which makes the creator's card
copy real.

**Decide first:** changing traits changes stat blocks, and replay fixtures are anchored to them.
Either the `rulesStartWeek` pattern again, or a deliberate fixture re-record.

### 0.2 Reconcile the authored numbers

Several constants in `NpcAlliances`, `NpcSocialActions` and `ThreatAssessment` are marked *authored*
because the markdown did not give them. At least one is wrong to be so marked: `alliance_meeting` is
`relationshipChange = 3` in `npc-social-behavior.ts` — exactly the value guessed. Also worth checking
`NpcAlliances.SourLine`, and the `ThreatAssessment` quirks (potential cap unreachable, total labelled
0–100 but capping at 112) which may be faithful or may be misreadings.

### 0.3 There is no CI

`.github/workflows/ci.yml` exists there. **This repository has none** — a PR reports
`statusCheckRollup: []`. Every check is somebody remembering to run `scratchpad/sync-and-run.sh`.

### 0.4 There is no sound

`HouseAudio.cs` is 200 lines that **synthesise tones** from a `frequency, duration, start, gain`
struct. `Assets/Gamesim` ships **zero audio files**. The reference ships `bbtheme.mp3` (767 KB) and
`background_music.mp3` (2.8 MB) with `useIntroAudio`, `useBackgroundMusic` and `useGameSFX`.

`GameSim-Game-Flow_v2.md` specifies music starting when the intro theme ends. The opening sequence in
commit `3c1a111` implements the five beats and none of the audio, because there is no theme to follow.

## Tier 1 — Producers for consumers that already exist

### 1.1 Deals (`deal-system.ts`, 31 KB) — **DONE, schema 10**

Proposal, a counter-offer table (`COUNTER_OFFER_MAP`), trait modifiers (`getTraitDealModifiers`), then
per-phase resolution — `evaluateNominationDeal`, `evaluateVoteDeal`, `evaluateVetoDeal`,
`evaluateFinalTwoDeal` — with `applyDealOutcome` updating alliance stability and `spreadBetrayalInfo`
propagating a break to third parties.

Already calibrated for here: `deal_fulfilled` +35 and `deal_broken` −50 sit in the interaction table
this port implements, against `promise-kept` +25 and `promise-broken` −40.

`PromiseState` is the template; `NpcPromises` is the shape; Phase C's rules boundary handles fixtures.

**What landed.** `DealState` (the ten types, seven statuses and four trust weights, spelled as
`WebEvictionVoting` and `WebSaveImporter` already read them), `NpcDeals` (the house bargaining among
itself and putting offers to the player, roll-free), `PlayerDeals` (`evaluatePlayerDeal` term for
term, including the five relationship tiers and the whole trait table), `DealResolution`
(`deal-action-rules.ts` plus `applyDealOutcome` and `spreadBetrayalInfo`), and the two commands
`ProposeDeal` / `RespondToDeal` with their controls in the conversation panel. `dealRulesStartWeek` is
the fourth use of the rules boundary. **The line that mattered most is one line**: `FromNative` now
copies `deals` across, so the eviction vote's deal weights — written before this and tested ever
since — finally multiply something other than an empty list.

Resolution hangs off the four actions the source resolves and no others: the nomination ceremony
(target agreements and safety pacts), the veto decision, the eviction vote (voting blocks only), and
the final two. A `vote_save` is deliberately *not* settled by the ballot — the vote evaluator already
scores it at +35, and settling it here would count the same promise twice.

One deliberate divergence in direction. `applyDealOutcome` moves the *proposer's* view of the
recipient whoever acted, which reads as the person who broke their word thinking less of the person
they wronged. This port writes it the way `SettlePromise` already does — the wronged party's view of
whoever acted — and moves both ways only for a voting block, where neither party is the one who
decided it.

**Deliberately not ported, and why.**

- **`COUNTER_OFFER_MAP`.** A counter-offer is a second proposal aimed back at the player, and there is
  nowhere for one to wait until the pending-proposal queue below is ported. Inventing a store the
  save format has no room for is worse than a declined deal being simply declined.
- **The pending-proposal queue for the player's own asks.** The reference queues a proposal and
  resolves it later; here asking and hearing the answer is one committed command. The resolution is a
  single roll against a number computed entirely from present state, so this produces the same
  distribution with one fewer thing to persist. NPC-to-player offers *do* wait, because those the
  player has to be able to see before answering.
- **Alliance stability.** `applyDealOutcome` nudges an alliance's stability by ±3 or ±10.
  `AllianceState` has no stability field, and adding one is another schema version for a number
  nothing else in this port reads. The relationship and betrayal effects, which everything does read,
  are ported.
- **`deal.context._voteTracking`.** The source accumulates each partner's ballot on the deal itself.
  `EpisodeState.votes` already holds exactly that by the time the house has voted, so the rule reads
  those instead of persisting a second copy.

### 1.2 Storylines (`storyline-system.ts` 37 KB, `branching-story-system.ts` 37 KB)

Lower priority than deals — largely reactions to the event layer, so it wants Tier 4 first.

## Tier 2 — Presentation over data that already exists

### 2.1 Weekly recap (`src/utils/recap/`, ~40 KB) — **DONE**

Four builders: weekly, finale, story-context, event-formatter. This port has `SeasonReport` for the
end of a season and nothing for the end of a week — over an `EpisodeEvent` log that already records
week, phase, kind and audience. Same shape as `SeasonReport`; no simulation change.

**What landed.** `WeeklyRecap`, a plain static class that reads a week back out of committed state,
and `WeeklyRecapScreen`, which draws it. The screen opens itself when an eviction has finished being
narrated — it waits on the cards' own `IsPlaying` rather than on a timer, so reduced motion and
batchmode cost one frame — and every week that has closed stays reachable from the notebook.

The recap derives nothing the save does not already hold, so it cannot contradict the season, and
adding it required no simulation change. Two constraints shaped it:

- **It only tells the player what their character knows.** A relationship movement is reported only
  where the player is one end of it, and a turning point only where they were in its audience. Two
  houseguests falling out privately is not theirs to read — the same line the notebook and the diary
  room already draw.
- **Three readings of the veto, not two.** No meeting at all is not the same as a veto left in the
  box, so `vetoUsed` is nullable and the screen says "No meeting" for the third case.

`SeasonReport` had private copies of the two parsers this needed — the name out of "Competition
winner: X · Mental." and the subject of an eviction line. They are now one implementation in
`WeeklyRecap`, public and tested, because two parsers for one sentence is one too many and the
second is always the one that drifts.

**Not ported.** The reference's season-level sections — winner, jury votes, rivalries, power
alliances, blindsides, competition beasts — are `SeasonReport`'s job here and it already draws the
ones this simulation can support. Rivalry and blindside detection lean on event flags
(`significance: major`, `blindside: true`) that nothing in this port sets; the turning-points
section stands in for them by reading the event kinds that only ever fire at a turning point, which
is honest about what is actually recorded rather than guessing at a flag that does not exist.

## Tier 3 — Gameplay breadth

### 3.1 Every competition plays the same minigame — **DONE (three of five, on purpose)**

The reference routes five by competition type: `EnduranceHold`, `WordScramble`, `MemoryMatch`,
`ReactionTap`, `DiceRoll`, plus `NPCScoring` and a `MiniGameRouter`.

This port has **one** — a timing bar, three attempts (`EpisodeDirector.StartChallenge`) — used for
every competition. `WebRules.WeightedCompetitionScore` already takes a category, so the simulation
distinguishes competition types that the player experiences identically.

**What landed.** `CompetitionMiniGames` holds the scoring on the reference's own 0–10 scale;
`MiniGameRun` holds one attempt as data rather than as a screen, taking its own delta so a
thirty-second competition is a loop in a test instead of half a minute of the suite's life. Three
panels in the director, each with keyboard and button controls.

**Three, not five, and the reason is the rotation.** `EpisodeEngine.CompetitionCategory` only ever
produces Skill, Mental and Endurance. `WebRules` also weights Physical and Crapshoot and nothing
asks for them, so a word game and a dice game would be screens no player could reach. Widening the
rotation to produce those two is a rules change that re-rolls every seeded season — a separate
decision with a fixture cost, not something to smuggle in behind a presentation change. The timing
bar stays as the fallback for any category without a game of its own.

**No simulation change.** A minigame produces the same `performance` the timing bar already
produced, and the engine weighs it the same way. The board's shuffle and the targets' placement come
from a generator each run owns, seeded from the wall clock — a minigame's draws are not part of the
committed command, so spending `randomState` on them would re-roll everything that follows, which is
the same reason `SeasonBuilder` takes the cast in table order rather than shuffling it.

**A scoring crossover, kept and pinned.** Seven of eight pairs scores 8.75; a board *cleared* on the
buzzer scores 8, because a cleared board starts at eight and earns the rest from the clock. Matching
almost everything quickly therefore beats finishing at the last moment. That is the reference's own
shape, it looks like a bug the first time anybody sees it, and correcting it would be a rules change
rather than a port — so `MatchingAlmostEverythingBeatsClearingItOnTheBuzzer` states it outright.

**Not ported.** `NPCScoring` generates opponents' scores for the minigame's own results screen. This
port already scores every competitor through `WebRules.WeightedCompetitionScore` from the season's
seed, and `CompetitionResult` already shows the standings — a second, unseeded scorer would be a
second answer to a question already answered.

### 3.2 The social vocabulary is still collapsed — **DONE**

Confirmed against `player-action-reducer.ts` (44 KB, 40 actions) rather than the design document:

- `Talk` is one verb where the source has five: `small_talk`, `personal_chat`, `discuss_game`,
  `strategic_discussion`, `relationship_building`.
- `ShareInformation` is one where the source has two: `share_secret`, `share_true_intel`.
- Absent entirely: `spread_rumor_strategic`, `house_meeting_strategic`, `progress_storyline`,
  `deal_coordination_respond`, `fast_forward`, `simulate_weeks`.

### 3.3 Social actions can be bought — **DONE**

`buy_action_point` trades relationship damage for another action — `costType` of `random_one` (one
houseguest takes the hit) or `spread_all` (everyone takes a smaller one); `buy_action_point_free` is a
storyline reward. `SocialActionBudget` here is a hard ceiling with no way past it.

A pressure valve with a real cost, and in none of the other plans in this folder.

**What landed for 3.2 and 3.3.** `WebSocialVocabulary` holds every number; nine appended commands
carry them. Five ways of having a conversation where there was one, plus `ShareSecret`,
`SpreadRumor`, `HouseMeeting` and `BuyActionPoint`. `Talk` keeps its ordinal — recorded seasons
depend on it, and it remains the plain conversation the five are variations on.

A risky conversation draws **twice**, for the gate and then the amount, exactly as the source does;
drawing once and reusing it would tie whether it worked to how well, which is a different game.
A whisper moves the listener's opinion of the subject rather than the player's, which is the whole
point of whispering it.

`buy_action_point` has a ceiling of six here where the reference has none. The reference's budget is
a counter it can decrement indefinitely; this save format bounds everything it stores, and an
unbounded purchase count is a season that eventually will not load. Validation's per-counter bound
moved with it: the legacy flat allowance of eighteen plus six purchases reaches twenty-four in one
phase, and a bound of eighteen would have refused a migrated season that had done nothing wrong.

**Still absent from 3.2, with reasons.** `progress_storyline` belongs to Tier 5 and has nothing to
progress yet. `deal_coordination_respond` landed as `RespondToDeal` with the deal system.
`fast_forward` and `simulate_weeks` skip play rather than add it — this port has
`SimulateCompetition` for the same impulse at a scale that does not hand whole weeks to the
simulation unwatched.

### 3.4 Contextual actions and lobbying

`contextual-action-generator` (37 KB) generates the available actions from context rather than
offering a fixed list. `veto-lobbying-system` (22 KB) is a whole phase of play — working the veto
holder — with no counterpart. Plus `player-activity-chains` (20 KB), `player-activity-system` (16 KB),
`intel-system` (10 KB).

### 3.5 Room-level interaction

`use-pull-aside-listener`, `useRoomActionListener`, `useNpcRoomBehavior`, `use-quick-action-cutscene`
hang actions off where people physically are. Pulling somebody aside has no counterpart here — and
this port owns the harder half already: a real 3D house, `HouseRoomQuery`, proximity, and a
conversation scheduler.

## Tier 4 — The event layer (~105 KB, six systems) — **house events DONE**

`phase-event-system` (23 KB) · `house-event-system` (20 KB) · `proximity-event-system` (17 KB) ·
`ambient-event-system` (13 KB) · `emergent-event-system` (11 KB) · `mid-week-crisis-system` (21 KB)

Nothing in `Assets/` matched any of these. This is what makes one week feel unlike the last: things
happen to the house rather than only because the player pressed something. `phaseEventSocialBonus` and
`phaseEventCompBonus` in the importer's discard list show they feed competition and social outcomes,
so this is not decoration. (Those two do have a producer here — the diary room writes them — so they
were never the gap; the gap was situations arriving unbidden.)

**What landed.** `house-event-system.ts`, as `HouseEvents` plus the `ResolveHouseEvent` command. Its
six templates, word for word and number for number, cast from the season's own state through the
reference's role placeholders — `{ALLY}` is whoever the player is warmest with, `{RIVAL}` whoever
they are coldest with, and the power roles fall back to somebody rather than printing a placeholder.
One situation a week, drawn at the transition where the house settles its other business, surfaced
above the ordinary controls and readable again afterwards in the weekly recap.

**The AI route is not ported and will not be.** The reference asks a language model for a bespoke
situation and falls back to the catalog when that returns nothing. A save whose content came from a
network call cannot be replayed, and every season here has to reproduce exactly from its seed. The
fallback is the part that works offline and deterministically, so the fallback is what was ported.

**Choices are resolved to people when the event is offered, not when it is answered.** A situation
left open for three weeks still moves whoever it was about rather than re-aiming at whoever the
player has since fallen out with — which is also why the choices are persisted rather than derived.

**`trustChange` goes to the ledger.** Trust here is `ThreatAssessment.TrustScore`, which reads
weighted ledger impact. A choice that costs trust has to leave a mark the ledger can see, or it
costs nothing at all.

**The cost, paid knowingly.** Drawing from the season's generator re-rolls every later draw, so
three existing tests moved. Two were fixtures that quiet the house to count effects exactly and
simply did not know about the new system; one was a parity-gap test asserting a warmth bound that a
re-roll flipped without anything warming up by design, now restated around the claim that actually
matters. This is the same class of change as removing a recorded command and was not avoidable:
events are both persisted and replayed, so the draw belongs in the recorded stream.

**Still absent from Tier 4.** `proximity-event-system`, `ambient-event-system`,
`emergent-event-system` and `mid-week-crisis-system`. The proximity one is the most tractable of
those here, because this port owns the harder half already — a real 3D house, `HouseRoomQuery` and a
conversation scheduler that knows who is standing where.

## Tier 5 — Narrative and the text bank

`social-cutscene-system` (74 KB, largest file in the project) · `dialogue-tree-engine` (17 KB), plus
the generation layer: `ai-thought-generator` (28 KB), `campaign-cutscene-generator` (22 KB),
`speech-generator` (13 KB), `allstar-dialogue` (12 KB), `quick-action-responses` (10 KB) — about 85 KB
against `HouseDialogue.cs` at 19 KB.

Ranked below agency deliberately: a cutscene about a week where nothing happened is still about
nothing. But note this is the size of the template bank behind the Phase E honesty note in
`npc-behaviour.md` — reflections will read flatter here partly because the bank is a quarter the size.

## Tier 6 — Layer 3 cognition — **the one real gap is closed**

`ai/memory-manager` (19 KB) · `ai/npc-decision-engine` (17 KB) · `ai/fallback-generator` (23 KB).
Already scoped as Phase E of `npc-behaviour.md`; assessment unchanged.

`fallback-generator` is worth having regardless of the rest — the source's own note is that it is
reproducible, which is what makes NPC behaviour testable without network access.

**Reading it against this port turned up less than the size suggests.** The generator's job is to
decide for an NPC when the AI call fails, and almost all of those decisions already exist here, most
of them in richer form:

| Its fallback | Here |
| --- | --- |
| Nomination, veto, replacement | `EpisodeEngine`, already weighted on the relationship |
| Eviction vote | `WebEvictionVoting` — a full evaluator with bloc directives and deal obligations |
| Alliance proposal and response | `NpcAlliances`, ported in Phase C |
| Dialogue | `HouseDialogue` |
| **Jury vote** | **Nothing. `relationship + a roll`, and that was all.** |

So Tier 6's honest scope was one function rather than three files, and it is now `WebJuryVoting`.

**Why it mattered more than its size.** A juror voted on how much they liked a finalist plus a ten
point jitter. The finalist who had been nicer won, and how either of them actually played was worth
zero — which makes winning competitions, surviving the block and outmanoeuvring people pointless the
moment the finale starts. The reference weighs four things: thirty per cent relationship, **forty per
cent respect for how they played**, fifteen for an alliance and fifteen for what was agreed. Respect
outweighing affection is the design, and it is what lets a jury crown somebody it does not much
like — an outcome the old rule could not produce at all.

No schema version: every term reads something already recorded. `Obligations` reads the deals and
promises systems, so it is a function that could not have been written before them.

`memory-manager` and `npc-decision-engine` stay unported. Their jobs are done by
`RelationshipLedger`, `ThreatAssessment` and `WebEvictionVoting`, and porting them would be adding a
second answer to questions this project already answers.

---

## Decisions rather than work

**The 21 Supabase edge functions** (`npc-decision`, `npc-reflect`, `npc-threat-rankings`,
`generate-storyline`, `social-cutscene-narrative`, `story-recap`, …) are Layer 4, and this project has
always said it will not use a service. That stays right — and `fallback-generator` is why it costs
little: every one of them has a deterministic local path.

**Avatars.** `src/avatar/` (35 files) plus `src/components/avatar-3d/` (149) plus `tools/` (a
MakeHuman pipeline, `gen.mjs` at 46 KB, an atlas dilator) against UMA, `CharacterPresentation` and a
creator previewing a wardrobe colour and a silhouette. The reference ships 20 `.glb`, 3 `.vrm` and 19
`.webp` portraits. A defensible divergence — but it should be a decision, not an oversight.

**End-to-end coverage.** 15 Playwright specs (~180 KB) covering `local-save-recovery`,
`autosave-cancellation`, `live-ceremony-receipts`, `nomination-accounting`, `startup-identity`,
`dialogue-resilience`. This port's equivalent is `PortVerification.Season.cs` — one scripted
walkthrough — plus section E of `ACCEPTANCE_MATRIX.md`, still 0/5 because it needs people. Better than
the reference at the unit level, worse at "does the built game work".

**Not read:** `src/components/game-phases` (186 files) and `src/components/avatar-3d` (149). Both are
React presentation; the phase components may hold flow logic worth a look before Tier 3.

---

## Sequence

| # | Work | Why here |
| --- | --- | --- |
| 1 | Cast data (0.1) | Data only; fixes the stats and behaviour of most of the cast. Needs the fixture decision first. |
| 2 | Authored numbers (0.2) | Cheap; raises confidence in everything already shipped. |
| 3 | CI (0.3) | The suites exist and nothing runs them. |
| 4 | Audio (0.4) | Two files and wiring; completes an opening sequence already built. |
| 5 | ~~Deals (1.1)~~ **done** | Best value of any system; the consumer exists and is tested. Landed as schema 10. |
| 6 | ~~Weekly recap (2.1)~~ **done** | Presentation over an existing event log. No schema change. |
| 7 | ~~Minigames (3.1)~~ **done** | Every competition currently plays identically. Three games, one per category the engine produces. |
| 8 | ~~Social vocabulary + action points (3.2, 3.3)~~ **done** | Player-facing breadth; append-only to `EpisodeCommandKind`. Landed on schema 11. |
| 9 | Event layer (Tier 4) — **house events done** | Unlocks storylines, which is why they are not earlier. Four of six systems remain. |
| 10 | ~~Storylines (1.2)~~ **done** · narrative (Tier 5) outstanding | Storylines landed on schema 12. The text bank is untouched and is a content job, not a systems one. |
| 11 | ~~Phase E (Tier 6)~~ **the real gap closed** | The jury now weighs how a finalist played. The rest of Tier 6 was already here in richer form. |

Items 1–4 are days rather than weeks and only one is a port. Items 8 and 9 both add persisted state —
**batch them into one schema version**, per the constraint above.

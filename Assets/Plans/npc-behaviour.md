# What the houseguests do when nobody is playing them

A plan for NPC behaviour, rewritten against `GameSim-NPC-Behavior.md` (the web build's own
description, 2,552 lines). An earlier version of this plan was written from a survey of the Unity
code alone; the source corrects it in several places, and those corrections are called out below.

## The architectural finding, which changes the scope

The source describes four layers, and states the rule that governs them:

> **Layer 2 is the source of truth for outcomes, Layer 4 is the source of truth for voice.**

| Layer | Where it runs | Status here |
| --- | --- | --- |
| 4 — LLM narrative cognition | server | **Out of scope, and costs nothing.** Language only |
| 3 — Cognitive state: memory stream, retrieval, reflection, self-model, goals | client, in the save | **Absent** |
| 2 — Numeric strategy: threat, decision engine, obligations, ledger, arcs | client, deterministic | **Half ported** |
| 1 — Embodied autonomy: motive decay, activity advertising, pathing | client, real time | **Ported** |

This is better news than the earlier plan assumed. I had treated NPC intelligence as something that
might need a model behind it. It does not: the source says every layer runs without the one above,
that Layers 1–3 are client-side, and — explicitly — that *"the fallback generator is therefore
reproducible, which makes AI behavior testable without network access."*

So the whole of NPC decision-making is portable, deterministically, with `HouseDialogue` supplying
the templated language the source falls back to. Nothing here needs a service this project has
always said it will not use.

## What is already ported

Layer 1 is done, and Layer 2's *voting* half is done well: `WebEvictionVoting`,
`WebNativeEvictionRound` and `WebVotingBlocs` carry threat/alliance/relationship/deal weights, bloc
coordination, and the split between public and private reasoning. The conversation scheduler
(`NpcSocialState`, `EpisodeNpcSocial`, `WebNpcConversations`) handles who talks to whom, about what,
for how long, with pair memory in both directions.

## What is missing

### 1. Houseguests cannot form alliances or make promises (still the biggest)

`alliances.Add` and `promises.Add` each appear exactly once in the entire simulation, both on the
player's path. No houseguest has ever formed an alliance with another houseguest in any season this
project has run — so the bloc system, which reads alliances to decide how a bloc coordinates, has
only ever coordinated blocs the player personally built.

The source gives both rules outright. **Alliance desire:**

```
score = relationship*0.4 + sharedThreats*15 + strategicValue*0.15 + (trust-50)*0.3 - alliances*10
propose when relationship >= 25 and score > 25
```
where `sharedThreats` counts houseguests both parties rate below −20 — the engine's "enemy of my
enemy", worth 15 points each and the standout term in the formula. `strategicValue` is
`compWins*5 + social*3`. Capped at 3 alliances per person.

**Promise type follows game position, not affection** — nominee begs for votes, an unallied player
courts the HoH, a warm unallied bond becomes alliance loyalty, and past relationship 60 at six or
fewer an NPC starts shopping for a final two.

### 2. No threat assessment

The earlier plan called this "no standing threat ranking" and left the shape open. The source
defines it precisely: a 0–100 composite of five **capped** components, where capping is the point —
a competition beast maxes at 40 and still needs social capital and alliance power to read as an
extreme threat.

| Component | Cap | Driven by |
| --- | --- | --- |
| competition | 40 | HoH wins ×8, veto wins ×6 |
| social | 30 | Average relationship with the active house |
| alliance | 20 | Total alliance membership size ×4 |
| potential | 10 | competition/10×3 + strategic/10×2, +2 for the social+strategic jury combo |
| reputation | 15 | Broken deals ×3 (cap 8), trust <35 → +5, trust >70 → −3, rivalry arcs |

Everything this needs already exists on `ContestantState` except trust.

### 3. The interaction ledger exists but its asymmetry does not

This is the correction I most want to flag, because the structure looked done and is not.

`RelationshipEventState` already carries `type`, `impactScore` and a `decayable` flag — close to the
source's `TrackedInteraction`. But **`decayable` is written `true` at the single call site and never
read anywhere**, and there is no weekly relationship decay pass at all. Only *motive* decay exists,
which is Layer 1 and unrelated.

The source's headline for this system is the asymmetry:

> **Betrayals never decay** — that asymmetry is the main reason the house develops long memories and
> grudges.

So the house currently has no long memory of betrayal, and also no forgetting of anything else. Both
halves are missing, and they only mean something together: decay without the exception makes
betrayal cheap, and the exception without decay makes nothing special.

### 4. No trust score

The source's `interactionTracker.getTrustScore` (default 50) feeds alliance desire, reputation
threat, and vote weighting. Nothing here computes it. It is derivable from the ledger above rather
than being new stored state.

### 5. No cognitive layer at all (Layer 3)

`MemoryState` stores memories; nothing scores or retrieves them. The source has three-factor
retrieval (recency, importance, relevance), periodic reflection producing insights, a weekly
self-summary with short- and long-term goals, and a dual-track split between private reasoning and
public statement. None of it exists here. It is also the layer the LLM would normally decorate —
but the source is clear that the numbers, not the model, decide outcomes.

### 6. NPCs take no autonomous social actions

`NPC_ACTIONS_PER_SOCIAL_PHASE: 3`. NPCs here converse through the scheduler and do nothing else —
no proposing, no promising, no gossiping, no eavesdropping. The source maps traits to verbs:
`Sneaky` gossips and eavesdrops, `Confrontational` picks fights, `Floater` mostly chats, and so on
across ten traits.

This compounds the asymmetry the player's new vocabulary just created: a houseguest who can be lied
to but cannot lie is not playing the same game.

## Order of work

**Phase A — the ledger asymmetry and decay.** Moved to first, ahead of alliances, and that is a
change from the earlier plan. It is the smallest change here — one flag written correctly at a
handful of call sites, plus a weekly decay pass — and everything downstream reads it: trust scores
come off the ledger, reputation threat comes off trust and broken deals, alliance desire takes trust
as a term. Building alliances first would mean building them against a trust value that is
permanently 50.

**Phase B — trust score and threat assessment.** Both are pure functions of state that already
exists, so both are derived rather than stored, and neither needs a schema version. Threat is
independently useful the moment it lands: the HUD can finally show why the house is looking at
someone.

**Phase C — alliances and promises between houseguests.** The formulas are given. Writes through the
same `AllianceState` and `PromiseState` the player's path uses, so the bloc system needs no changes
and inherits the whole thing.

**Phase D — autonomous social actions.** Three per social phase, chosen from trait repertoires,
using the vocabulary that now exists on the player's side.

Three things came out of doing it that were not visible from the plan:

- **It is the first NPC pass that spends the season's generator**, and it has to be. `selectTalkTarget`
  is weighted sampling, and the source says plainly that is what stops a houseguest talking to their
  closest friend every single time. A deterministic port would have deleted the variety the system
  exists to produce. It sits behind `npcSocial.rulesStartWeek`, which both recorded witnesses already
  declare themselves past, so nothing on file had to move.
- **Acts between two houseguests must not go through the engine's `Change`.** Not to save rolls —
  because `Change` feeds `relationshipArcs`, and an arc is keyed by one houseguest with no record of
  who it is *with*. Every line it renders reads "Your rivalry with X". Feeding it from conversations
  the player was never in would accumulate the house's own traffic into the player's feuds and
  friendships, and both `ThreatAssessment` and the eviction vote read those. `RelationshipLedger.Move`
  exists for this, and the rule is: the player in it, use the engine; neither of them, use the ledger.
- **The whole existing suite stayed green**, which is not by itself good news — a pass that is never
  invoked looks exactly the same. `ASeasonPlayedThroughTheEngineLetsTheHouseActOnItsOwn` is the test
  that tells the two apart. The reason nothing broke is that the full-season fixtures assert shape
  (the season finishes, four people sit on the jury, week four ends it) rather than who won.

And writing that test immediately paid for itself, because its first version failed: **no NPC
alliance forms in the shipped scenario at all.** The floor is a relationship of 25 before anybody
proposes; houseguests start at zero with each other, and in a season driven by commands the only
thing that warms them is this pass's own conversations at four points each. Four weeks of that,
spread across the house by weighted sampling, does not get near twenty-five.

The reference build warms its house through a conversation system that runs continuously. This port
has one — `WebNpcConversations`, and it does move pair relationships — but it only turns while
somebody is walking around in free roam, so a headless season never gets it. **So the bloc system
still only ever coordinates blocs the player built, in exactly the seasons the tests can see.** That
is the same finding Phase C opened with, one layer further in, and it is not closed.

Closing it means either warming the house faster or lowering the source's floor, and both are
changes to the source's own numbers rather than ports of them — so it is stated rather than quietly
fixed. `AColdHouseNeverReachesTheSourcesAllianceFloor` pins the shortfall and says in its own note
what to do when it starts failing.

Two Phase C corrections went in alongside it, both now that the house can act on the player:

- **The player was being enrolled in alliances they never agreed to.** Acceptance is decided here by
  mutual desire, and the player's side of that is computed from their scores rather than asked of
  them. The source excludes the player from autonomous alliance and promise generation outright, and
  now so does this. `FormAlliance` remains how a player joins one.
- **Both positional promise branches were reading a week that had already finished.** A week's social
  phase belongs to its *end*, so the block is still standing in state and the title still sits with
  somebody after the vote that settled both. The surviving nominee spent the following week begging
  for votes counted days ago, and the whole house courted an outgoing Head of Household who had
  already nominated, already lost the veto and already seen the vote come in. Both branches belong to
  campaigning, which is the other moment the pass runs, so both are now guarded on an unsettled
  block. The second one was costing every houseguest a turn a week on a promise worth nothing.

**Open, and deliberately not closed here:** nine of this project's seventeen traits have no repertoire
in the source's ten, and six of the twenty-four shipped houseguests lead with one of them — so a
quarter of the cast plays the default. Mapping `Charming` to `Social`, `Deceptive` to `Sneaky` and so
on is plausible, but it is authoring a personality system rather than porting one, and this project's
standing rule is that the web build is the reference. `TraitsThisProjectHasAndTheSourceDoesNotFallToTheDefault`
pins the gap so it stays visible; it is the test to change if somebody decides to map them.

**Phase E — the cognitive layer.** Largest, least load-bearing, and the only part that would feel
thin without a model behind it. Worth doing last and worth being honest about: reflections and
self-summaries expressed through templated language will read as flatter than the source's.

## Constraints

- **Every draw comes off `randomState`.** The source calls `Math.random()` freely; that cannot be
  copied. `selectTalkTarget`'s weighted sampling, in particular, must roll through `Roll(s)`.
  *(Held in Phase D.* `npcSocial.randomState` looked like the tidier home for it and is not: that
  stream is advanced by real-time free-roam ticks, which are not in the command record, so its value
  at any given Advance depends on how long the player spent walking around. A pass drawing from it
  would not replay.)
- **Knowledge boundaries (A9).** NPC reasoning may use what the NPC knows. Nothing may surface to
  the player except through a private `MemoryState` they own — the channel eavesdropping already
  uses.
- **Prefer derived over stored.** Trust and threat are pure functions; keeping them derived avoids a
  schema version. If Phase E forces stored state, batch every field it needs into one bump.
- **Recorded fixtures declare their rule version.** Any change to NPC decisions moves the replay
  witnesses; `socialBudgetRulesStartWeek` is the precedent for handling that without editing them.

## Verification

Per phase: offline compile, then both suites via `scratchpad/sync-and-run.sh`. Baseline to beat is
**EditMode 802, PlayMode 128**.

Two properties deserve dedicated tests:

- **A season with no player input produces a plausible house.** Run a spectator season to the end
  and assert alliances formed and dissolved, that threat rankings moved as people won competitions,
  and that the winner was not simply the highest-stat houseguest.
- **NPC decisions are replayable.** Same seed and command list, same house, including every alliance
  formed and every grudge held. This is the test that catches a stray `new Random()`.

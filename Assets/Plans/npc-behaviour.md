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

**Phase E — the cognitive layer.** Largest, least load-bearing, and the only part that would feel
thin without a model behind it. Worth doing last and worth being honest about: reflections and
self-summaries expressed through templated language will read as flatter than the source's.

## Constraints

- **Every draw comes off `randomState`.** The source calls `Math.random()` freely; that cannot be
  copied. `selectTalkTarget`'s weighted sampling, in particular, must roll through `Roll(s)`.
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

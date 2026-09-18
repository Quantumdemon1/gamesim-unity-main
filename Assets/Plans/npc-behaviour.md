# What the houseguests do when nobody is playing them

A plan for NPC behaviour, written after porting the player's social vocabulary and finding the
asymmetry that created.

## The headline finding

**Only the player can form an alliance or make a promise.** `alliances.Add` and `promises.Add`
appear exactly once each in the whole simulation, both inside the player's social-action path in
`EpisodeEngine`. No houseguest has ever formed an alliance with another houseguest, or promised one
anything, in any season this project has ever run.

Every alliance in the house is one the player is in. Every promise is one the player made or
received. The voting-bloc system — which is substantial, ported faithfully, and well tested — reads
alliances to decide how a bloc coordinates its votes, so it has only ever coordinated blocs the
player built. The NPCs are not playing the game; they are reacting to someone who is.

The reference build's own description of the social week is the contrast: *"houseguests move on their
own, seek each other out, form and break alliances, gossip, remember what you did, and react."*
Three of those six are done.

## What is already ported, and is good

This is not a bare simulation. The parts that exist are real and tested:

| System | Where | What it does |
| --- | --- | --- |
| Motives and decay | `WebNpcMotives`, `WebNpcActivityRules` | Each houseguest carries motives that decay and are satisfied by activities |
| Where they go | `WebNpcActivityCatalog`, `HouseMeetingCoordinator` | Room choice, rendezvous points, world leases so two pairs cannot claim one venue |
| Who they seek | `WebNpcSeekCandidate`, `EpisodeNpcSocial` | Conversation partner selection, cooldowns, pair memory |
| What they talk about | `WebNpcConversations` | Trait-weighted topic choice, duration, completion effects |
| How they vote | `WebEvictionVoting`, `WebNativeEvictionRound`, `WebVotingBlocs` | Threat/alliance/relationship/deal weights, bloc coordination, public vs private reasons |
| What they feel | `WebRelationshipArcs`, `WebJurySentiment` | Arcs with escalation levels, jury sentiment across the season |
| Oath ripples | `WebLoyaltyOaths` | How a declaration moves third parties |

The conversation scheduler in particular (`NpcSocialState`, `EpisodeNpcSocial`) is careful work —
clock ticks, issued sequences, pair memory in both directions, world-readiness evidence separated
from simulation state. None of that needs redoing.

## What is missing

Measured against the reference build's §10 "Houseguest intelligence" row and §6's social week.

### 1. Houseguests cannot form or break alliances (biggest)

The bloc system is built to coordinate alliances that mostly do not exist. Until NPCs pair up on
their own, its most interesting path is unreachable in a season the player does not personally
construct.

Needs: a rule for when two houseguests form a pact (the source uses a relationship floor), and one
for when it dissolves (the source dissolves automatically if any pair sours badly). Both must run
off the season's generator, and both must write through the same `AllianceState` the player's path
uses so the bloc system needs no changes at all.

### 2. Houseguests cannot use the vocabulary the player just got

The player can now ask for intel, eavesdrop, lie, vent, scheme and plan a backdoor. Houseguests can
talk, and that is all. An NPC who can be lied to but cannot lie is not a player in the same game.

The honest framing: this is not "give NPCs six commands". Most of those actions are *player*
affordances — eavesdropping is a thing you do because you are standing somewhere. The ones that make
a house feel alive are lying, venting and scheming, because they produce relationship movement the
player can notice and misread.

### 3. No standing threat ranking

`WebEvictionVoting` carries threat *weights* inside a single vote evaluation, but there is no threat
ranking the house holds between votes, and nothing implements the source's *"threat logic shifts
sharply at five players or fewer"*. That shift is what makes an endgame feel different from week
three, and its absence is why the late season currently plays like the early one.

### 4. No storylines, crises or story beats

Absent entirely — no file mentions them. The source spawns narrative arcs from betrayals and intense
relationships, interrupts the week with crises and war-room moments, and carries modifiers forward.
`WebRelationshipArcs` is the nearest thing and tracks intensity without ever spending it on an event.

### 5. Houseguests do not reflect

The player has a diary room that shapes a persona. Houseguests have memories written about them and
never look back at them. The source has them reflect on past events; here, memory is only ever read
by the vote evaluator.

## Order of work, and why

**Phase A — alliances between houseguests.** Highest value by a distance, because it is the one gap
that makes an existing, expensive, well-tested system (voting blocs) reachable. It is also the
smallest of the five: a formation rule, a dissolution rule, and the generator discipline to keep
both replayable. Nothing downstream needs changing — the bloc system reads `AllianceState` and does
not care who wrote it.

**Phase B — a standing threat ranking.** Second because it is what makes Phase A's alliances *aim*
at something, and because the five-or-fewer shift is a concrete, testable rule rather than an open
design. Likely a derived value rather than persisted state, which would avoid a schema version.

**Phase C — the social vocabulary for houseguests.** Lying, venting and scheming, driven by motives
and threat. Deliberately after B, because an NPC that schemes without a threat model schemes at
random, and random malice reads as noise rather than as a house with opinions.

**Phase D — reflection.** Houseguests reading their own memories and letting them shift behaviour.
Cheap once C exists, because the same memories are already being written.

**Phase E — storylines and crises.** Largest and least load-bearing. The season is complete without
it; this is the layer that makes a season memorable rather than correct.

## Constraints that shape all of it

- **Every draw comes off `randomState`.** Consuming the generator anywhere new shifts every result
  after it, which is why `SeasonBuilder` takes its cast in table order and `DrawVetoPlayers` returns
  early when everyone plays. Any NPC decision that rolls must roll through `Roll(s)`.
- **Knowledge boundaries (acceptance A9).** NPCs may act on what they know. Nothing they do may
  surface to the player as narration of a room the player's character was not in — the channel for
  that is a private `MemoryState` the player owns, which is how eavesdropping was built.
- **New persisted state means a schema version.** Two were spent in one sitting already. Prefer
  derived values; if state is unavoidable, batch every field the whole plan needs into one bump.
- **Recorded fixtures declare their rule version rather than being edited.** Any change to NPC
  decisions will move the replay witnesses. `socialBudgetRulesStartWeek` is the precedent.

## Verification

Per phase, and the same discipline as the rest of the port: offline compile, then both suites via
`scratchpad/sync-and-run.sh`. Baseline to beat is **EditMode 778+, PlayMode 128**.

Two properties deserve their own tests, because both are easy to break and silent when broken:

- **A season with no player input still produces a plausible house.** Run a spectator season to the
  end and assert alliances formed, dissolved, and that the winner was not simply the highest-stat
  houseguest.
- **NPC decisions are replayable.** The same seed and command list must produce the same house,
  including every alliance formed and broken. This is the one that catches a stray `new Random()`.

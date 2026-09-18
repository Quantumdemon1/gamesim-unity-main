# What the web build still does better

Written against `Quantumdemon1/gamesim` (private, TypeScript, pushed 2026-09-17) — the actual
reference source, not the markdown appendices this port has been working from. 1,157 files, 147 of
them under `src/systems`, 77 test files.

Every claim below is from reading that tree and grepping this one, not from the design documents.

## The pattern worth naming first

**This port has a habit of shipping the consumer of a system without the producer.** It has happened
three times:

| system | consumer ported | producer ported |
| --- | --- | --- |
| alliances | `WebVotingBlocs` reads them to coordinate a bloc | only in Phase C, months later |
| promises | `WebEvictionVoting` weighs them | only in Phase C |
| **deals** | `WebEvictionVoting.DealObligation` / `PairDealValue` weigh them | **never** |
| **storylines** | `WebFinalSpeeches` lets a finalist cite them | **never** |

`deals.Add` and `new WebVoteDeal` appear nowhere in `Assets/`. The eviction vote asks every
houseguest what deals oblige them, gets an empty list every time, and has done since it was written.
Same for storylines in the final speech.

This is the cheapest quality available in the whole project: the hard, well-tested half already
exists, and nothing downstream needs changing. Phase C proved the shape of the work.

A second confirmation sits in `WebSaveImporter.EmptyStateSlots` — the list of web save fields this
port drops on import. It reads as an inventory of what the web has and Unity does not:

    deals · houseEvents · activeStorylines · activeModifiers · npcMemories · pendingNPCProposals
    playerPerceptions · threatMatrix · playerPersona · grudgeLedger · pendingStorylineEvent
    lastEavesdropIntel · phaseEventSocialBonus · phaseEventCompBonus · weekFocusModifier

## Tier 1 — Deals (`deal-system.ts`, 31 KB)

The single highest-value gap, and the one with an existing consumer.

`DealSystem` covers proposal, a counter-offer table (`COUNTER_OFFER_MAP`), trait modifiers on
acceptance (`getTraitDealModifiers`), and then per-phase resolution: `evaluateNominationDeal`,
`evaluateVoteDeal`, `evaluateVetoDeal`, `evaluateFinalTwoDeal`. Outcomes run through
`applyDealOutcome`, which updates alliance stability and calls `spreadBetrayalInfo` — a broken deal
propagates to other houseguests rather than staying between two people.

Deals are also *stronger than promises* in the interaction table this port already implements:
`deal_fulfilled` +35 and `deal_broken` −50, against `promise-kept` +25 and `promise-broken` −40. So
the ledger and trust systems are already calibrated for a system that does not exist yet.

Dependencies: none. `PromiseState` is the template, `NpcPromises` is the shape to copy, and the
Phase C rules-boundary precedent handles the replay fixtures.

## Tier 2 — The event layer (~105 KB, six systems, none present)

`phase-event-system` · `house-event-system` · `proximity-event-system` · `ambient-event-system` ·
`emergent-event-system` · `mid-week-crisis-system`

Nothing in `Assets/` matches any of these names. This is the layer that makes one week feel unlike
the last: things happen to the house rather than only because the player pressed something.
`phaseEventSocialBonus` and `phaseEventCompBonus` in the importer's discard list show these events
feed back into competition and social outcomes, so it is not decoration.

Worth doing before the narrative tier, because storylines are largely reactions to events.

## Tier 3 — Player agency breadth (~105 KB)

`contextual-action-generator` (37 KB) · `player-activity-chains` (20 KB) · `player-activity-system`
(16 KB) · `veto-lobbying-system` (22 KB) · `intel-system` (10 KB)

This port has fourteen fixed social verbs on `EpisodeCommandKind`. The web generates the available
actions from context instead, and chains them, so what a player can do on Tuesday of week five is
not the same list as Monday of week one. `veto-lobbying-system` is a whole phase of play — working
the veto holder — that has no counterpart here at all.

## Tier 4 — Narrative and presentation (~165 KB)

`social-cutscene-system` (74 KB, the largest single file in the project) · `storyline-system`
(37 KB) · `branching-story-system` (37 KB) · `dialogue-tree-engine` (17 KB)

This is the "it reads like a show" layer, and it is the biggest thing the web has that this port
does not. `HouseDialogue` is the nearest equivalent and is a templated line bank, not a cutscene
system or a branching tree.

Deliberately ranked below agency: a cutscene about a week where nothing happened is still a cutscene
about nothing.

## Tier 5 — Layer 3 cognition (~59 KB)

`ai/memory-manager` (19 KB) · `ai/npc-decision-engine` (17 KB) · `ai/fallback-generator` (23 KB)

Already scoped as Phase E of `npc-behaviour.md`. Unchanged assessment: largest, least load-bearing,
and the part that reads thinnest without a model behind it. The `fallback-generator` is the piece
worth having regardless — the source's own note is that it is reproducible, which makes NPC
behaviour testable without network access.

## Also missing, smaller

- `accepted-consequences/` (32 KB with its own tests) — a consequence framework the port has no
  equivalent of.
- `relationship-evolution` (11 KB) — relationships that change shape over a season rather than only
  accumulating a score.
- `nomination-evidence-record` (12 KB) — why a nomination happened, kept as a record.
- `fast-forward` (13 KB) — skipping ahead; `FastForward` appears in `Assets/` only inside
  `WebSaveImporter`.

## Where this port is already ahead, and should stay that way

Worth stating so none of the above is taken as "rewrite it to match".

- **Testing.** 931 EditMode plus 148 PlayMode tests against 77 test files. The determinism and
  persistence suites in particular have no web counterpart.
- **Determinism.** Every draw goes through `EpisodeState.randomState`; the web calls `Math.random()`
  freely. Replays here are reproducible and the web's are not.
- **Persistence.** Nine schema versions with frozen validators, migrations and fixture sweeps. The
  web has a save format; this has a save *contract*.
- **Rules versioning.** `blocRulesStartWeek`, `npcSocial.rulesStartWeek`, `socialBudgetRulesStartWeek`
  let rules change without invalidating a season already in progress. Nothing like it in the web.

Any port from the tiers above has to arrive under those four properties, which is most of the work
and is why raw file size is a bad estimate of effort.

## Suggested order

1. **Deals.** Highest value, existing consumer, no dependencies, proven shape.
2. **Reconcile what is already ported against real source.** Several numbers in `NpcAlliances`,
   `NpcSocialActions` and `ThreatAssessment` are marked *authored* because the markdown did not give
   them. At least one is wrong to be marked so: `alliance_meeting` is `relationshipChange = 3` in
   `npc-social-behavior.ts`, exactly the value guessed. Cheap, and it raises confidence in
   everything already shipped.
3. **Event layer**, then **agency**, then **narrative**.
4. **Phase E** last, as already planned.

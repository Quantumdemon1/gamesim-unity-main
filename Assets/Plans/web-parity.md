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

## Second pass: what a systems-only survey missed

The first pass read `src/systems` — 147 of 1,157 files. The rest of the tree holds the two most
actionable findings in this document.

### The shipped cast does not match the reference (data only, high value)

`src/data/character-templates.ts` carries every houseguest's full card, and this port's
`CastTemplates` disagrees with it for most of the cast:

| | web | this port |
| --- | --- | --- |
| Alex Chen | Marketing Executive · Strategic + Social | Software Architect · Analytical + Manipulative |
| Jordan Taylor | Sales Representative · Social + Sneaky | Sales Representative · Charming + Social |
| Quinn Martinez | Social Media Influencer · Confrontational + Social | Content Creator · Charming + Deceptive |
| Avery Thompson | Police Officer · Loyal + Competitive | Firefighter · Loyal + Stubborn |
| Sam Williams | Restaurant Owner · Strategic + Loyal | Site Foreman · Competitive + Loyal |
| Blake Peterson | Architect · Analytical + Sneaky | Night Auditor · Introverted + Sneaky |

Seven of twelve have different traits and nine have different occupations. **Traits are not
cosmetic** — `WebTraits.CreateStats` derives the whole stat block from them, so these houseguests
play differently from the same houseguests in the reference build, and `NpcSocialActions.Repertoire`
reads the lead trait to decide what they do with a social turn. Changing Alex Chen from Analytical to
Strategic changes his stats *and* his behaviour.

All twelve also carry `hometown` and `bio` in the web table. `CharacterDraft.From` currently says
those are left blank because "the cast table has never held either" — true of *this* port's table,
and wrong about the source. That comment should go, and `CastTemplates.Template` should gain the two
fields, which also makes the character creator's card copy real instead of empty.

This is data, not architecture. It is the cheapest parity win available and it touches the most
visible content in the game.

### There is no CI, and the web has end-to-end coverage this port does not

- **`.github/workflows/ci.yml` exists on the web. This repository has no CI at all** — a PR here
  reports `statusCheckRollup: []`. Every check in this project is somebody remembering to run
  `scratchpad/sync-and-run.sh`.
- **`e2e/` holds fifteen Playwright specs, about 180 KB**, and they cover exactly the areas this port
  treats as most dangerous: `local-save-recovery` (21 KB), `autosave-cancellation` (21 KB),
  `live-ceremony-receipts`, `nomination-accounting`, `startup-identity`, `dialogue-resilience`,
  `results-containment`, `final-hoh-commit`.

This port's equivalent is `PortVerification.Season.cs` — one scripted walkthrough of a standalone
build — plus section E of `ACCEPTANCE_MATRIX.md`, which is still 0/5 because it needs people. The
tests here are better than the web's at the unit level and worse at the "does the built game
actually work" level.

Neither of these is a port. Both are process, and both are cheap next to the system tiers above.

### Avatars are a real pipeline there and a placeholder here

`src/avatar/` is 35 files: `CreatorStudio.tsx` (24 KB), `FacePainter.ts` (15 KB), `MorphAvatar.ts`,
`createHumanoidBase.ts`, a casting director, and Ready Player Me integration. `tools/` adds the
generator behind it — `gen.mjs` (46 KB), a MakeHuman pipeline with body specs, and a texture atlas
dilator.

This port has UMA, `CharacterPresentation`, and a character creator whose live preview is a wardrobe
colour and a silhouette, with a comment explaining that a silhouette is the honest answer before a
body exists. That comment is right, and the web answers the question differently: it builds the body.

Worth knowing before anyone plans more work on the creator — the reference is not a colour picker.

### Smaller models with no counterpart here

- `relationship-tier.ts` — relationships as named tiers, not only a number.
- `player-perception.ts` — how the house reads the player (`playerPersona` is in the importer's
  discard list, and the LLM prompt in the design document takes it).
- `conversation-topic.ts` — topics as a model; this port has a string and a validator.
- `houseguest/mental-state.ts` — a real mood and stress model with `updateHouseguestMentalState`
  driving it. This port has the two fields and writes them from exactly one place
  (`EpisodeEngine.cs:446`), so the values exist but almost nothing moves them.

### Documentation

`docs/` has thirteen documents including `QUALITY_EVIDENCE.md` (40 KB) and `AAA_VERTICAL_SLICE.md`.
This port has `Assets/Plans/` and `ACCEPTANCE_MATRIX.md`, which is comparable — noted only so nobody
assumes the web is undocumented and rewrites what already exists.

### Not yet read

`src/contexts` (80 files) and `src/hooks` (40) were not opened. In a React codebase these often hold
real game logic rather than only wiring, so there may be more here.

## Suggested order

1. **Reconcile the cast data.** Pure data, no architecture, and it fixes the stat blocks and
   behaviour of most of the shipped houseguests. Add `hometown` and `bio` to the template while
   there.
2. **Reconcile the authored numbers** against real source. Several in `NpcAlliances`,
   `NpcSocialActions` and `ThreatAssessment` are marked *authored* because the markdown did not give
   them, and at least one is wrong to be marked so: `alliance_meeting` is `relationshipChange = 3` in
   `npc-social-behavior.ts`, exactly the value guessed. Cheap, and it raises confidence in everything
   already shipped.
3. **CI.** The suites exist and nothing runs them automatically.
4. **Deals.** Highest-value system gap, existing consumer, no dependencies, proven shape.
5. **Event layer**, then **agency**, then **narrative**.
6. **Phase E** last, as already planned.

The first three are days, not weeks, and two of them are not really ports at all.

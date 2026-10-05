# Wave C E2 - reply-card trade-offs

Implemented above isolated gameplay commit `48e1dd603ae7724ba06d8fbf825d4e71001471e5`.
This finishes E2's reply behavior in source, not its full social-verb balance gate.
It is not merged into the frozen QA candidate or the live project. Native runtime,
desktop, visual, performance and human acceptance remain separate and pending.

## Source and compatibility

`Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md:454-460` requires distinct reply
payoffs. The original web `src/systems/ai/npc-social-behavior.ts` supplies the
three reply families and base relationship values retained in `ReplyCards.cs`.
The added assessments and negotiated safety proposal are the approved native
extension, not a claim that the web already records those outcomes.

The additions require the existing unreleased schema23 fresh-season
`EconomyRulesOn` boundary. Disabled and delayed rules keep the complete old
simulation outcome and original descriptions. No saved field, schema number,
migration, command kind, reply key or claim-source vocabulary changes here.

## Material changes

- `EpisodeEngine.ReplyPayoffs.cs` runs after the original reply effect, memory,
  receipt and vote-promise path in `EpisodeEngine.Strategy.cs`. Incoming replies
  remain free, even in an exhausted action window.
- Deflecting a confrontation learns one speaker-owned assessment of another
  active NPC: the lowest directed score, with ordinal-ID tie breaking. Neither
  the player nor speaker can be the subject.
- Confronting gossip learns the speaker's assessment of the named listener;
  refusing a plea learns their assessment of the other actual current nominee.
  Missing, self, player or evicted subjects provide no replacement information.
  Plea information additionally requires an unresolved campaign and two actual
  nominated identities. No hidden ballot or vote intention is copied.
- Assessments use existing Told standing rows, player-owned private memories
  and player/speaker-only information events. They retain the standing512,
  event256 and owner-memory30 limits. Information replies use no extra season
  RNG draws, read attempts or AskThisWeek entitlement. Additional fresh events
  can affect later sequence-keyed stories; legacy outcomes remain unchanged.
- Escalation keeps its original trust loss, then offers mutual safety for THIS
  WEEK through the existing proposal reducer. That reducer owns eligibility,
  the player40-deal cap, the NPC's real acceptance roll, accepted/refused trust
  changes, optional counter-offer, expiration and obligations. No guaranteed
  agreement and no synthetic consent roll. It adds no action cost to the reply.
- If safety is unavailable, the reply still resolves with its original effect
  and a private explanation; it creates no deal and consumes no proposal roll.
- `ReplyCardPayoffs.cs` supplies detached consequence descriptions using only
  public identity/status and proposal eligibility. It does not rank hidden
  opinions, expose acceptance odds, mutate static reply templates or roll RNG.

| Card | Answer | Distinct consequence |
| --- | --- | --- |
| Confrontation | Apologize | Repair trust without an obligation |
| Confrontation | Deflect | Lose trust, gain an assessment |
| Confrontation | Escalate | Risk a larger loss to propose a real mutual safety deal |
| Gossip | Confront | Lose trust, learn their assessment of the listener |
| Gossip | Let it slide | Smallest immediate trust loss |
| Gossip | Gossip back | Damage the listener's view of the gossiper |
| Plea | Promise support | Gain trust and make a real vote promise when eligible |
| Plea | Stay noncommittal | No new loss or obligation |
| Plea | Refuse | Lose trust, learn their assessment of the other nominee |

- All three director adapters (free-time board, campaign board and legacy/final-
  three column) show the descriptions while keeping existing caption contracts.
- New `EpisodeDirector.ReplyChoices.cs` binds callbacks to one rendered view,
  load generation, current revision and pending card. Closed, replaced, reloaded
  or consumed views cannot answer. The callback consumes its view authority
  before the durable write: a failed write requires a new deliberate click on
  the newly rendered view. `EpisodeDirector.cs` clears that authority on close,
  accepted commands and render. This view safety applies to old seasons too;
  it does not change their saved rules or accepted-command outcomes.
- `ReplyCardPayoffTests.cs` adds89 pure/Edit cases; the pure project registers
  them. New PlayMode partial adds10 tests. Floors become Edit2917 / Play958 /
  UMA77 / pure1925. Integration must also retain eda's11 separate QA Edit cases,
  for2928 combined. Five new Unity metadata GUIDs are unique.

## Evidence

Root: `D:/CodexGamesimEvidence/integration-20261004`.

- Focused pure01 passes83 cases; pure02 passes89. Final full pure03 passes
  **1925/1925 executed**, zero failed. Discovery1938 includes13 explicit reports
  not executed; this is not1938 passes. Final artifact
  `wave-c-replies-pure-03/wave-c-replies-full.trx`, SHA256
  `dae055515f8cc33b03e037cb19981ad37fa9f72bd760ff843ed3294c5efab361`.
- Pure cases cover all replies, casts3-16 where legal, exhausted budgets,
  hidden-independent previews, caps, missing subjects, atomic invalid commands,
  stale/duplicate answers and exact save-shaped managed replay. Later actual
  ballots both fulfill and break the promised support; actually nominating a
  safety partner breaks the agreement with the correct actor/week recorded.
- Forty actual escalation/replayed-proposal comparisons produce17 agreements
  and23 refusals, matching the existing reducer's deals, relationships and RNG.
  A separate720-outcome harness finds no strictly dominated answer in each
  nominal first-opportunity reply set across warmth, information, negotiated
  safety, listener damage and freedom from obligations. It excludes already-
  known information, unavailable deals, future utility, win rate and human
  preferences. This does NOT establish E2's complete all-verb balance gate.
- Final offline02 compiles5/5 fresh NoUMA assemblies, zero errors, reading only
  idle `D:/GamesimNoUma` response files. Output:
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/e5fcf25d347c40e597422ef20405e201`.
  `wave-c-replies-offline-02.log`, SHA256
  `dbf3c9ac7ee549234331dcef19d4cc778e359bff7ab1f98b738d1ce812fed71d`.
  Retained offline01 failed a Play test's Rect/RectTransform helper argument;
  the argument was corrected without removing or weakening the layout check.
- Ten new Play tests are authored and compiled, NOT executed. They cover actual
  controls, all reply families, exact durable replay/reload, double clicks,
  close/reopen, same-revision load, locked writes, both text sizes/keyboard
  reachability, legacy descriptions and final-three column routing.
- Root manual review only: independent reviewers remain quota-unavailable.
  No source fixtures/goldens or checks changed. No fresh full source-drift
  attestation, UMA compile or native gameplay pass is inferred from these runs.

## Remaining completion gates

1. Finish E2's whole-action expected-value and dominance comparison, including
   known-information repetition, eligibility and demand/threat trade-offs.
2. E4 player-HoH pitch cards with free Feel them out; E5 bounded speech influence
   on persuadable NPC voters, never authority over the player's ballot.
3. All four REQUIRED Wave D systems: unified commitments, all-week NPC strategy,
   negotiated alliance meetings and deeper secret leaks/double-dealing.
4. Combine the gameplay and separate QA work without losing either test set;
   run exact-candidate native suites, migrations/save recovery and a separately
   verified desktop build. The eda baseline run contains none of these changes.
5. Complete and integrate coherent real assets, then visual/accessibility
   review and actual1920x1080 at60FPS GTX1060 profiling. Old900p evidence is not
   acceptance for this target.
6. Human E1-E5 acceptance, including three first-time players, pacing,
   consequential choices, a loss ending and visual coherence.

No live scene, source edit, font, settings, recovery, save, build or remote was
overwritten. The separate UMA full-suite candidate/controllers stay frozen.

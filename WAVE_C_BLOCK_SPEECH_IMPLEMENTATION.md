# Wave C E5 - a bounded appeal from the block

2026-10-05 UTC. Implemented above isolated gameplay commit
`625d191a7487f4930453a67dd124f18a6163c993` on `codex/wave-c-economy`.
This is staged implementation, not live promotion, Unity runtime or desktop acceptance.
The separate schema-22 QA candidate and live C: project were not mutated by E5.

## Source and compatibility boundary

`Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md:474-477` requires content/audience-sensitive
speech influence on persuadable NPCs. `STRATEGY-LOOP-PLAN.md:155,169-170` defers speech
by approach. The original web `NomineeSpeeches.tsx:187` saves prose; its watcher
reactions at line120 are cosmetic. `src/systems/eviction-vote-round.ts:86` and the
vote evaluator do not consume that prose. This approved native addition is NOT
a web speech formula or literal fixture parity. The existing four LobbyApproach
keys and TraitFit supply trait responses; the +/-4 term and baseline margin20
are explicitly native bounded policy, not recovered web constants.

Only fresh economy-enabled seasons with active lever, agency and strategy rules
use this adapter. Existing receipt-less schema23 speeches are inert: loading,
projection and voting never infer a receipt or approach from saved prose. Legacy
rules preserve the prior speech log, text, RNG and sequence behavior. The source
fixture option defaults remain off and retain their original ten-factor votes.
No DTO, schema version, command enum, migration, historical serializer/fixture,
RNG generator or source fixture is changed.

## Material changes

- New `Simulation/BlockSpeeches.cs` and unique metadata define explicit Emotional,
  Strategic, Deal, Pressure and Quiet approaches. Deal is rhetoric, not an
  automatically made pact/promise. The exact saved prose is preserved; no NLP,
  keyword classifier or private effect/odds preview is introduced.
- `EpisodeEngine` records one typed public `block-speech:{approach}` event IN
  PLACE OF the prior single speech log. Speaker-first audience is the active
  house plus an inactive local player watching the broadcast. Physical seating
  is not broadcast authority. Quiet has an empty saved speech and the exact
  receipt `No speech was given.`; it has zero influence. Fresh NPC speeches
  preserve the existing plea and add an authored closing matching their appeal;
  legacy NPC speech/log behavior is unchanged. No extra RNG draw/log sequence,
  action budget or promise is spent by this adapter.
- The existing bounded256-event FIFO retains at most two current-week speech
  receipts while their speech rows are live, including Results and same-week
  Social. The oldest unprotected event is removed instead. Next week clears
  speech rows and unpins old receipts; historical public quotes remain inert.
- `WebEvictionVoting` receives transient native-only receipt/approach inputs.
  Complete baseline scores, INCLUDING the actual bloc directive, are evaluated
  first, once. Both nominee terms use the same immutable baseline pair. Only
  regular eligible NPC voters with baseline margin strictly below20, a heard
  receipt and no alliance with the opponent are susceptible. Per-nominee term
  is clamp(1 + TraitFit/10, -4, 4); pair margin can move at most8. HoH, nominees,
  inactive players, the player's actual ballot and saved NPC ballots are not
  overwritten. Projection remains causal for already-recorded NPCs; the real
  command writes only missing ballots and duplicate votes are still refused.
- `VoteRead` always hides the speech factor and counts its private placeholder
  even at zero, on both nominees, independent of traits, alliances, eligibility,
  approach or already-cast status. Public unknown-factor counts cannot reveal
  susceptibility or actual NPC voting progress.
- `EpisodeValidation` adds a closed reserved-receipt contract: allowlisted key,
  enabled rule weeks, Eviction delivery, bounded unique audience including the
  player, exact speaker/nominee/text/quiet relationship, once per speaker and
  at most two per week. Current receipts reject Campaign and pre-speech
  Interaction. Unresolved delivery requires the exact deterministic audience;
  resolved same-week Social and older inert public history are supported.
- `JuryHouseRead`, `StoryText`, `WeeklyRecap.Ledger`, `WeeklyRecapScreen` and HUD
  chrome recognize typed actor identity, exact public words and speech styling.
  Quiet is not treated as a plea quote. Legacy name-prefixed logs still work.
- New `EpisodeDirector.BlockSpeeches.cs` with unique metadata supplies the actual
  four-approach HUD, selected label/honest influence scope and public readback.
  `EpisodeHud`, `EpisodeDirector` and its Diary Room partial support initial
  drafts and a change callback, without changing default finale behavior.
  Draft+approach are scoped to session/player/week and shared across station,
  Diary Room, Escape and rerender; new week/slot resets them. Finale draft is
  separate. Full2000-character multiline prose and literal rich-text characters
  are retained, not truncated or interpreted.
- Durable speech callbacks capture state, revision/phase, load generation,
  activation and a per-render consumed token. They do not read a newly mounted
  field when a stale callback fires. Retirement happens on render/close; physical
  Diary Room authority is checked. Token is consumed BEFORE save commit, so a
  failed write needs a new deliberate view, not retrying an old callback. Shared
  `SubmitEvictionSpeech(text)` compatibility callers retain Emotional default.
  Diary copy distinguishes immediate durable speech submission from normal
  confirmation; saved words/approach are read back without private ballot hints.
- Pure test registration includes only `BlockSpeechInfluenceTests`. Suite floors
  increase to Edit3229 / Play988 / UMA77 / pure2182. Combining c272 QA must KEEP
  its separate11 Edit/4 Play additions: Edit3240 / Play992 / UMA77 / pure2182.

## Test coverage and independent review

138 authored cases: **91 pure/Edit**, **32 native SaveStore Edit**, **15 Play**.

Pure tests cover all four approaches, bounds and exact margin edge, post-bloc
ordering, simultaneous nominee order, roles/ally exclusion, stable private read
counts, actual player ballot authority, receipts/replay/legacy/no backfill, full
FIFO, RNG/log preservation and real8/16-cast rollover/jury/story public quotes.
JSON replay is not native SaveStore evidence.

Native Edit tests exercise production SaveStore exact shape/bytes, full2000 text,
Quiet once-only, malformed candidate/checksummed disk, explicit recovery with
damage archived, real Results/Social/next week/FIFO, receipt-less23 inertia,
retained actual NPC ballots and synthetic sealedV22 forgery rejection without
rewriting/resealing the golden fixture. Unique guarded temporary slots only.
These cases are compiled, NOT executed in Unity.

Play cases exercise both surfaces/cast/text sizes, real typing/Tab/Escape, draft
retention and reset/no finale bleed, full literal prose/readback/reload, Quiet,
stale/double/load/scene/revision callbacks, failed save preserving both copies,
physical diary seating/copy, legacy and actual saved ballot preservation. These
cases are compiled, NOT executed; no layout/runtime acceptance is inferred.

Independent read-only review prompted fixes for stale callback reads, draft loss,
save/confirmation wording and privacy-count leakage. Final authority/lifecycle/
save/scope review found no blocker. Reviewers did not rerun root artifacts.
The review missed a missing final namespace brace: offline compilation caught it;
the final correction adds ONLY `}` and newline, independently byte-verified with
unchanged assertions and balanced lexical shape. Static review is not compilation.
All five new metadata GUIDs are32 lowercase hexadecimal and unique across hidden
and ignored Assets metadata, checked by root and independently by a peer.

## Retained implementation evidence

Under `D:/CodexGamesimEvidence/integration-20261004`:

- Initial E4 smoke with `--no-restore` and a new external obj path stopped with
  NETSDK1004 (assets file absent); zero tests ran. Normal restore fixed invocation,
  not production/checks. `wave-c-speeches-smoke-02/e4-regression.trx`:80/80 pass,
  SHA256 `0b743132bad33f39ad9d020ca5464b9ea679b9f7803ed2068c5f1f6344fd0d67`.
- Initial focused01:87/87 pass before four root lifecycle/history cases;
  SHA256 `865741d402bc59baa85b78981c3c10c31f64ee21b22fab135d49ea22e1512292`.
  Final `wave-c-speeches-focused-02/speeches.trx`:91/91 pass,
  SHA256 `9af57707f3cc39ff7cc89102da8f3961aaee66b01dd6dbad89d5c17cd655dca9`.
- Earlier full01:2178 executed passes, before those four cases;
  SHA256 `2676d322fad8e1b9280bba2386aa2239576931752fa6a2f9fcca3bd1b0e98dae`.
  **Final `wave-c-speeches-full-02/full.trx`:2182/2182 executed pass, zero failed**.
  Discovery2195 includes13 explicitly unexecuted reports, not2195 passes.
  Root50186 CLOSED0; SHA256
  `5c1860fadd821cf8d1b32f08d9f2aeb0b40ffd3a48ce7f78175a7a92be38d8c3`.
- Offline01 retained FAILED1, missing Persistence import in the new Play file;
  `wave-c-speeches-offline-01.log` SHA256
  `23a61eee903cd0d76f1002b2929a48b07c86b136e2e8ff16dd976ea3278f0422`.
  Offline02 retained FAILED1, CS1513 missing final native-test namespace brace;
  SHA256 `ae7814bb939fd416a430f7c35521e848264093c75d017dd3d0efe716bd5d3b10`.
  Corrections were import/brace only; no assertions were reduced or checks disabled.
- **Final `wave-c-speeches-offline-03.log`:8/8 fresh assemblies, zero errors**,
  including final native/Play source; root34542 CLOSED0. Explicit Windows
  PowerShell5.1, read-only references from idle D:/GamesimAcceptance, outputs
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/63ca68ed7db941b8907e59f46993056e`.
  SHA256 `5f041db0d564e0c8d4b87e0975dbe4e9eb9ad2a488a37b1cba999a3f2dacd4a3`.
  No active NoUMA cache read/write or native test launch during E5 development.
- Final diff check clean apart from informational Git LF/CRLF notices. No E5
  changes to Runtime/Persistence, historical Fixtures, Scenes, ProjectSettings,
  Packages, CI, art/vendor or source-fixture bytes. This is scoped inspection,
  not a standalone player build or whole-branch source-drift attestation.

## Separate baseline QA and remaining acceptance

Frozen c272 UMA g22u2 previously closed green: Edit2540, generalPlay935, UMA77.
Same-pin NoUMA g22n4 root72031 now CLOSED0 at2026-10-05T12:51:01.6891535Z,
elapsed5377.1840983s. Actual retained XML: Edit2540/2540 and Play907/907, zero
failed/skipped. Whole controller has no drift, timeout, cleanup errors, unowned
descendants or failure; owned native processes exited0. Terminal status
PassedNativeTechnicalChecksVisualReviewPending, visualAccepted=false.
Terminal SHA256 `d831bb266226d9fd6314d58add5e28393fde42d096696c10524abcedcd21c0b7`.
Runroot `D:/CodexGamesimEvidence/orchestration/full-suite-runs/g22n4-20261005T112125168Z-b0ec08c4`.
NONE of Wave C is present in these baseline runs. Controllers/helpers remained
unchanged through closure. Do not poll closed native/test/compile handles again.

After closure, read-only MCP GetProjectRoot matched the live C: project;
EpisodeHouse is loaded/saved, editor idle/not compiling/updating. Console read
succeeded: four retained historical MCP argument errors (GetActiveScene and
Types string conversion), not a clean Console. No entries were cleared.
Live remains b25edcb4 with retained room/UMA edits, fonts, scene/settings and
recovery/InitTestScene files; integration remains c272 with only local UMA settings
dirty. No live promotion, user-save/build deletion, remote publishing or accounts.

Required next gates: deliberate QA+Wave C integration and exact-checkout native
SaveStore/UI/full suites; wider E2 contextual/room/bargain balance; all four owner-
required Wave D systems (authoritative commitments, week-long NPC strategy,
negotiated alliance meetings, deeper secrets/double-dealing); consistent actual
assets; matching desktop build; separate visual and actual windowed1920x1080
at60FPS GTX1060 profiling; three first-time human playtests and pacing acceptance.
The port and AAA quality are NOT complete.

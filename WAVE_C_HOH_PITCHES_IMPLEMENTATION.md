# Wave C E4 - the player HoH hears the house

2026-10-05 UTC. Implemented above isolated gameplay commit
`ce10f3e1644b8513c28246c0f652a9663fab1768` on `codex/wave-c-economy`.
This is a staged implementation, not a live promotion or native/desktop acceptance.
The separate schema-22 QA candidate and the live C: project were not mutated.

## Source and compatibility boundary

`Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md:469-472` requires pitch cards from
NPCs courting a player HoH and a free assessment per card. The original web's
`src/systems/ai/npc-social-behavior.ts:204` supplies safety-courting behavior;
`src/systems/contextual-action-generator.ts:95-140` describes nomination-target
questions and offering safety. The existing native Court agenda, threat ranking,
reply machinery and safety promises supply the actual rule dependencies.

The new delivery policy, deterministic recommendation, three answers and free
Told disclosure are the approved native adapter, NOT literal web-fixture parity.
The +8/0/-5 base response payoffs reuse existing plea trade-offs, not the web
generator's estimated success percentages. Safety is a unilateral existing
PromiseState, not a reciprocal deal or a guaranteed future NPC ballot.

Pitch behavior requires the existing fresh-season economy boundary, active
strategy/agency rules, a player HoH and the pre-nomination phase. Legacy seasons,
NPC HoHs and the three old card families retain their prior behavior. No schema,
DTO, command enum, migration, RNG generator or historical fixture was changed.
Schema-22 validation rejects the new kind rather than retroactively admitting it.

## Material changes

- New simulation files `HoHPitches.cs` and `EpisodeEngine.HoHPitches.cs`, with
  unique metadata. The existing NPC Court action retains its warmth/log effect
  and offers one card per eligible speaker that nomination week. IDs are
  `reply-pitch-{week}-{speaker}`; recommendation uses the speaker's existing
  ranking, excluding the player and allies. A safety-only card permits no target.
  Offers consume no extra RNG and cannot respawn after inspection/final reply.
- `ReplyCards`, `ReplyCardPayoffs` and the strategy reducer add the Pitch kind,
  title, message, three final answers and the separate free `feel-out` key.
  Promise safety has +8 base warmth; hear them out is neutral; turn them down
  has -5. Existing relationship machinery applies those base effects. Final
  answers remove only that card and never pick the nominees for the player.
- One free assessment per exact card is recorded in the existing reply ledger.
  It keeps the card open, spends no action or RNG, and does not spend the weekly
  chance-based ReadPerson entitlement. The speaker discloses their own opinion
  band as Told information to the player; no ballot, secret pact, threat weight
  or estimated odds are exposed. Rendering uses the learned standing, never a
  fresh hidden-score read; an evicted standing points back to the notebook.
- Safety uses existing promise capacity (200), duplicate, breach and expiry
  semantics through next week. An existing promise is reaffirmed, not duplicated;
  a full record disables a new promise but permits the other replies. The saved
  ReplyRow.promised flag remains vote-only and is false for every pitch row.
- `EpisodeValidation` and `EpisodeLedgerValidation` impose kind-specific phase,
  identities, keys, magnitudes, recommendation and receipt limits. Only the
  oldest pending card can be answered. At most 24 pending cards and 48 pitch
  rows per nomination week keep an assessment marker within the 512-row reply
  ledger during its reachable lifetime, even if the ledger began full. Nominate
  clears pending pitches after authority/eligibility checks; no ignored pitch
  applies a response effect. Malformed or revived answered cards are rejected.
- `HouseguestNotes` and `JuryHouseRead` distinguish inspected pitches from final
  replies and from vote pleas. `WaitingOnYou` appends a nomination-set lapse
  without changing old enum values or legacy card expiry. Advance does not
  incorrectly claim that it expires a pre-nomination pitch.
- The existing nomination Picker presents a fitted four-choice pitch card:
  three answers and Feel them out (free). Assessment becomes disabled once
  recorded. Back to nominees and Hear houseguest pitches are view-only routes;
  neither spends a move or resets draft nominees. The existing plan row shares
  space with reopening pitches without colliding with candidate comparison.
  The tracker stays within its existing four-tile cap.
- Diary Room uses the same durable ReplyChoice authority and supports dismiss,
  reopen and retained draft nominees. Copy distinguishes immediately saved pitch
  replies from nomination confirmation; pending-card expiry is stated in picker,
  footer, combined safety warning and the actual nomination review summary.
  Leaving Diary Room clears its view-only status. Generic reply views also offer
  the free assessment and learned result, with honest promise capacity/scope.
- Shared nominee HUD pickers accept optional initial drafts and a selection
  callback without changing default callers. Render retires pitch-navigation
  tokens; callbacks are additionally gated by activation, load generation,
  revision and phase. ReplyChoice still uses the shared consumed-token guard.
  `PortVerification.Season.cs` answers a pending HoH pitch through the real UI
  before automated nominee selection, rather than bypassing the new view.
- Legacy payoff tests now explicitly enumerate the original three kinds. Their
  original nine answers and fixtures are preserved; Pitch has separate coverage.
  The pure project registers the new pure test file. Suite floors only increase:
  Edit3106 / Play973 / UMA77 / pure2091. Combining c272 QA work must retain its
  separate 11 Edit and 4 Play additions: combined3117 /977 /77 /2091.

## Tests and independent review

New coverage totals 118 cases:

- **80 pure/Edit cases** in `HoHPitchTests`: real 8/16-cast HoH transitions,
  recommendation policy, null recommendation, assessment per card, replay,
  full ledgers, weekly entitlement independence, privacy/learned readback,
  safety duplication/cap/breach, ignored expiry, invalid authority/receipts,
  and legacy/NPC-HoH Court parity. JSON replay is not native SaveStore evidence.
- **23 native Edit cases** in `HoHPitchPersistenceTests`: actual SaveStore
  save/reload, exact-byte preservation on invalid input, strict checksummed
  malformed payloads, once-only assessment, safety settlement, ignored expiry,
  and explicit backup recovery retaining a corrupt primary. A synthetic v22
  forgery first proves the old Confrontation control loads, then rejects Pitch
  while retaining archived fixture bytes. These cases are compiled, NOT executed.
- **15 PlayMode cases** in `EpisodePlayModeTests.HoHPitches`: both cast sizes and
  text sizes, keyboard/fitted layout, durable assessment and answers, draft
  retention, stale/double/load/scene/revision callbacks, failed save writes,
  Diary Room/cancel/actual nomination, and legacy/NPC-HoH absence. Compiled,
  NOT executed in Unity; no layout or runtime acceptance is inferred.

Independent read-only review found and prompted fixes for lost Diary Room draft
nominees, misleading confirmation copy, stale view-only navigation and incomplete
expiry/status messaging. Final independent scope/authority/privacy/save review
reported no blocker. Reviewers did not independently rerun the root's test artifacts.

## Closed implementation evidence

Artifacts are retained under `D:/CodexGamesimEvidence/integration-20261004`:

- Focused `wave-c-pitches-01/pitches.trx`:78/78 passed before two learned-reader
  cases were added. SHA256
  `96b4754107fde8266acccc377dc58c00e9baee375e15218e327f7890042f1a56`.
- Earlier full `wave-c-pitches-full-02/full.trx`:2091 executed passes, retained
  but predates final UI/copy cleanup. SHA256
  `d3fc8e24de4219f4cb9e68bfaf933e0f5950b8d57dd2dc2072507715a0a744dd`.
- **Final full03:2091/2091 executed passed, zero failed.** Discovery2104 includes
  13 explicit unexecuted reports, not2104 passes. Root33919 closed0.
  `wave-c-pitches-full-03/full.trx`, SHA256
  `19dca086d0fe244b330aadd60cdc3b7e2dc10417fb1ce27f2ed60386bcbf96d8`.
- **Final offline02:8/8 fresh assemblies, zero compilation errors**, including
  the final native-persistence and Play test source. Root25899 closed0; explicit
  Windows PowerShell5.1, read-only references from the now-idle UMA cache, output
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/ac6a5cec677841b5b87a0a2b51a5e9bb`.
  `wave-c-pitches-offline-02.log`, SHA256
  `62c5509af7a94e97d921f847a17cc9a62e213f6c4a4d78ee36e68c19d6a88347`.
  The earlier offline01 8/8 artifact is retained, not relabeled as final.
- Final diff check is clean apart from Git's informational LF/CRLF warnings.
  A final strict metadata check caught a 33-character GUID on the new persistence
  test; replaced it with a valid unique 32-character GUID before commit. This
  metadata-only correction followed the pure/compile artifacts; source bytes
  are unchanged and native verification must use the corrected revision.
  No changes to historical Fixtures, Runtime/Persistence, Scenes, Packages,
  ProjectSettings, CI or vendor/art assets. This does not imply a whole-branch
  source-drift attestation or a fresh standalone player compilation.

## Separate QA candidate and open gates

Frozen c272's UMA run g22u2 closed naturally at2026-10-05T11:11:52Z: Edit2540/2540,
general Play935/935 and UMA77/77, zero failed/skipped, no drift, timeout, cleanup
errors or unowned descendants. Terminal status is
PassedNativeTechnicalChecksVisualReviewPending; terminal SHA256
`bc2bc4d64bb7f5d4b9b6a1d13aaba474b7aa4b4a28da3eec6d114ebd8415db43`.
It contains NONE of this Wave C gameplay. Its same-pin NoUMA g22n4 run is still
active at this document's writing; integration/controllers remain frozen.

E4 still needs native save/UI execution and later combined regression acceptance.
Remaining implementation includes E2's broader contextual balance/action coverage,
E5's bounded block-speech influence, and all four owner-required Wave D systems:
unified commitments, NPC strategy throughout the week, negotiated alliance
meetings and deeper secret leaks. Real consistent assets, deliberate integration,
a matching desktop build, actual GTX1060 windowed1920x1080/60FPS profiling and
human playtests are separate open gates. A900p profile does not accept that target.

No live edit, ordinary user save, retained scene/recovery/build, remote, account,
cloud connection, purchase or deployment was changed by this increment.

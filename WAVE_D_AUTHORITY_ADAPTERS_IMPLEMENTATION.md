# Wave D1 authority adapters and combined-native repairs

2026-10-05 UTC. Work above disabled foundation `296266ce`; not production activation,
full D1 completion, native acceptance, a desktop build or performance acceptance.
All four Wave D systems and actual windowed1920x1080/60FPS on the GTX1060 remain
owner-required. Imported/historical games never opt into the new authority.

## Material changes in this increment

- `CommitmentReferences.cs`: detached source-shaped promise/deal views, source-family
  capacity counts including all history, stable-ID lookups and detached canonical
  lookup retaining effect identity. Legacy fields and list order are unchanged.
  Canonical rows follow their own stored order: this is not cross-store chronology.
  No writable mirror, dropped evidence, implicit settlement, RNG or save write.
- `HoHPitches.cs`: safety-promise eligibility/capacity/copy reads the new reference
  facade. `HouseguestNotes.cs`: player-known promise/deal notes read it too; existing
  party filtering and ballot masking remain. Other readers are deliberately NOT
  relabeled complete: strongest protection, incident counts and saved refs need
  explicit integration rather than counting projected duplicate evidence rows.
- New reference tests cover exact legacy field/order parity, detached collections
  and rows, source status/term/actor projection, mixed-family stable links, complete
  grouped evidence, historical capacity, corrupt competing authority refusal,
  HoH-pitch reaffirmation/full caps and notebook knowledge boundaries. New metadata
  is explicit. These tests are being registered in the pure subset.
- `UnifiedCommitmentStore.cs`: assembly-internal promise/deal creation, pending
  NPC response and atomic mixed-counter creation. Cloned new rows/lists install
  only after source-capacity/identity/term/link checks. Both counter rows are
  reserved under40 before writing, including all historic and other-family rows.
  Links are reciprocal, same-week/player-to-same-NPC, with identical numeric
  counter/price ID suffixes and exactly one Safety member. Stored non-safety
  partner remains in the legacy authority. Source availability/consent/action/RNG
  are still the command owner's responsibility, not a storage capability.
  Existing legacy list/record handles stay unchanged for promise creation/response;
  append builds a new list preserving existing objects and cloning only new rows.
  Incoming legacy field replacement is refused before publication.
- `UnifiedCommitments.cs`: narrow source-backed NPC-offer term representation.
  A late acceptance keeps original proposal ID/week and expires in its actual
  answer week. Pending/declined terms stay at the proposal week; no early expiry
  pass, history rewrite or production response handler was added here.
-79 new storage cases plus26 reference cases;105 new executed-case candidates.
  Source-floor declarations raised to3632Edit/988Play/77UMA/2398pure for WaveC.
  Future combined floor is3643Edit/992Play/77UMA/2398pure, preservingQA11/4.
  New GUIDs are explicit. Test-only reflection retains internal writer visibility.
  Safety must never be appended to legacy stores under the prospective rule.
  Existing engine creation/settlement still owns gameplay; activation remains
  rejected by the engine/save validators. Execution details below remain pending.
- Seven Play test partials keep all76 authored cases without added skips, removed
  cases or loosened assertions. They fix the fixture/timing/recovery/replay issues
  below. Retryable UI-write tests use the existing Editor-only failure seam and
  assert one actual button-owned save attempt while primary is readable; separate
  file-lock cases explicitly recover by validatedLoadNow before fresh actions.
- `EpisodeDirector.Strategy.cs` renders assessed Diary feel-out withLockedAction;
  source handler guards/stale callback refusal remain. `EpisodeHud.CampaignBoard.cs`
  and `.FreeTimeBoard.cs` measure every original payoff description at its normal
  scaled12pt font and reserve matching reply space. Free-time reply heroes can
  take more of the house-card area; exceptionally short frames use the existing
  scroll. No truthful tradeoff copy removed, minimum-font workaround, ignored
  overflow, source rule/effect/cost or scene/art changes.

## Closed combined-native failure, preserved

Previous combined candidate `ed492fce` completed run `g23n1`, root session96249
CLOSED exit1 at2026-10-05T14:56:21.1355688Z,6419.034s, no timeout. Native Edit:
3240 total/3230 passed/10 failed/0 skipped. Native Play:992 total/950 passed/
42 failed/0 skipped. Source drift, cleanup errors and unowned descendants empty;
all held processes exited and were confirmed stopped without controller stop.
Terminal SHA256`dd548ac6ee13c39d72250794a11f982ecc0bf0479693cb0905187f2d69affc6d`.
All eight retained proof-file hashes independently matched terminal receipts.
This run is a failure, not current-adapter acceptance or a green integration gate.

Ten Edit failures match the prior isolatedV3-V5/V13-V15 test-wiring repair.
The42 Play failures have source-backed repair groups:

-13 authored-roster fixture IDs incorrectly hard-code ContentCatalog.Maya.
-20 modal-control assertions run before the existing0.45s pointer hold expires.
-5 retry fixtures lock the primary unreadably, correctly trigger recovery, then
  incorrectly expect the old panel to stay open. Use readable-primary pre-write
  diagnostic failure for retry-in-view; preserve actual unreadable recovery tests.
-1 intentionally disabled control is inspected with a helper requiring pressability.
-1 nomination replay puts the second nominee into the command's text field.
-2 actual UI defects: assessed Diary feel-out remains interactable, and truthful
  reply-payoff copy overflows its fixed tile. Repair runtime affordance/layout,
  not the strict stale-callback, command/disk, hit-testing or overflow assertions.

## Live-project preservation check

After full-controller closure, read-only Unity MCP identity matched
`C:/Users/kelli/Gamesim Big Brother`. EpisodeHouse is loaded/saved (`isDirty:false`),
21 roots, editor idle/not updating or compiling. Console Error query returned0;
warnings include historical UMA-body fallback/model-preview warnings and shadow
atlas pressure. These are not accepted final character/art/performance evidence.
Nothing was cleared or changed in the editor. LiveHEAD remains`b25edcb45`, with
the existing room/font/scene/UMA/settings/recovery/InitTestScene edits retained.
No normal saves, old builds, snapshots, accounts, remotes or optional services
were touched. The integration's local-onlyUMA settings remain uncommitted.

## Execution and review

Independent reviews checked the storage/reference and native-repair changes.
Initial pure01CLOSED1:2395/2396 passed; the new canonical notebook actor case
exposed a missing reader path, not a source effect or save failure. The canonical
player-party path now uses the projected stored settlement actor, keeping the
legacy public-ledger interpretation otherwise. Added strict legacy parity and
public-helper unrelated-NPC privacy regressions, without weakening the original
assertion. Pure02CLOSED0:2397 passed before the final public-helper guard; this
intermediate success is NOT final-source acceptance. Both first and second
offline invocations closed0 with8/8 fresh assemblies; final-byte rerun pending.

Final boundary review is clear. Final frozen-source checks are CLOSED:

- Pure03:2398 regular cases passed,0failed,exit0. The13 pre-existing Explicit
  report fixtures are not selected; discovery totals are not executed-report
  acceptance. TRX SHA256`24f788ffe22ccc6b8e10b78450c7fe29f972ed15a2725634626688b57bd5c568`.
- Offline03:8/8 fresh owned assemblies,0errors,exit0; output`b28b7b65979e473fa25985b6132bf8a4`.
  Log SHA256`6d397aacaf6080b74f482d6dd2583e1ab5343bdcd907ec66a610e4a34151b1d8`.
  All944CS/asmdef inputs remain byte-identical to before03 image, SHA256
  `8d31dc76f65951b55867d7e38f23bac9114b5b77afa7d1b0f742c9921300dc43`.
- Managed24-01:385/385 real authored methods passed under Unity'sMono,0failed,
  exit0,15.75s;110Frozen23+59V24+111policy+26reference+79storage. Binding SHA256
  `94e444d09a072c989ce2ce7e83a0ba526818e86fb42107d98d0321a35e8ed5da`.
  Synchronous fixture diagnostic, NOT native Edit/UI or desktop evidence.
- Cross-build replay23-01/replay24-01: both exit0, complete55seasons/5956actual
  attempted transitions,103.72/107.21s. Same retained original22fixture/harness/
  launcher and paired whole persisted snapshots/commands/results/RNG/history.
  Only exact disabled24defaults/schema are projected for comparison. All55
  per-trajectory digests/counts match. Final comparison02 SHA256
  `48579fc33bea2a114e7fe8b6cb682f21fde45bd005821c3e887dfa978b98d67c`.
  First comparison01 retained correct55paired rows but null aggregate from
  Measure-Object on OrderedDictionary; retained/superseded, not acceptance.
- The separately reviewed adapter runner specializes only the foundation's
  output scope/five-class allowlist/diagnostic label. Original helper/shared
  harness are unchanged. New helper SHA256
  `e3582cd453fc105334de06b4ec6f65fbfe31a970d5acffd815cfc184fa133a7a`.
  Its source/DLL hashes are separate diagnostics, NOT arbitrary compile provenance.
- Copy-only archive afterg23n1 preserved801mixed-timestamp images/manifests/
  diagnostics plus9proof files, originals unchanged. Receipt SHA256
  `96ff5d209f4165cc74ec56021448cd385aab23e9bcaf0dd9131482a41ba7028b`.
  This is preservation, not fresh screenshots or visual acceptance.

All four new metadata GUIDs are valid/unique acrossAssets. Frozen1-23,
immutablefixturebytes, scenes, art, settings and packages have no increment
delta. Independent review also preserved all76oldPlaycases/strict assertions.

Pending: deliberate combined merge retainingQA11Edit/4Play cases, fresh complete
exact-pinNoUMA/UMA, separate matching shipping build and graphical verification.
Offline/pure/managed evidence never substitutes for native, visual, performance
or human acceptance. Safety is STILL not activated or complete end-to-end.

## Next dependencies

Finish every Safety writer/response/settler/expiry/expulsion and effect owner,
mixed-link/reference resolution, warnings/page and mechanical consumers BEFORE
fresh-only opt-in. Then canonical final-two, vote/plea, oath and alliance calls;
all-week NPC strategy; negotiated weekly alliance meetings; deeper secret leaks/
double-dealing; widerE2 contextual choices; consistent actual house/cast/competition
assets; native same-pin regression/build; actual1080p60 quiet/stress/startup/memory
and visual/accessibility review; three first-time30-45minute human playtests0/3.

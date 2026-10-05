# Wave D preparation and agreed1080 profiling support

2026-10-05 UTC, separate existing D:/GamesimWaveC branch abovec98c0ee. The live
project and frozen combineded492 native checkout were not changed. This is a
dependency increment, NOT implemented unified commitments or a completed port.

## Every project change

1. New `Runtime/Persistence/FrozenEpisodeV23.cs` and unique `.meta`: literal
   schema23 economy, complete original lobby/card/reply/event/speech shapes,
   caps, IDs, references, pitch boundaries/payoffs and speech delivery receipts.
   Strict-check all original rows before removing only valid novel pitch cards
   and Campaign/vote lobby rows in a private validation clone; strip exactly
   two economy fields, set22 and delegate all remaining shape checks to the
   unchanged Frozen22 chain. Preserve original payload, ledger, events and
   promises. The contract remains UNUSED by live migration dispatch.
2. New `Tests/EditMode/FrozenEpisodeV23ContractTests.cs` and unique `.meta`:
   110 authored cases (5 simple,105 parameterized,14 methods). Actual reducer
   witnesses for pitch/inspection/replies, all5 speech approaches, Campaign
   vote lobby, full season and Results/Social/next-week history; exact projection
   equality and unchanged rejected input; strict missing/extra/null/type/cap/
   duplicate/reference/boundary/payoff/audience/delivery/receipt rejection.
   Independent review found and repaired empty-string optional HoH compatibility
   before checks; a positive live-validator witness retains its exact value.
3. `Runtime/Episode/PortVerification.cs`: named1920x1080, frame-cap-1/VSync0
   target. Keep1280x720/800 accessibility captures before timing. Sample actual
   Screen resolution/fullScreenMode and Application cap/VSync alongside EVERY
   timing sample. Missing coverage, any bad resolution/mode/cap frame invalidates
   operational verification even if restored later. Report requested/first
   sampled/final screen distinctly, counters and observed uncapped settings.
   End measuredSeconds/profileFinishedUtc immediately after the sampling loop,
   excluding post-workload captures/waits and the optional functional season.
   Extract non-writing/non-quitting report construction for tests. Explicit
   performanceAcceptance is `Not assessed; uncapped diagnostic sample only.`
4. `Tests/EditMode/VerificationReportTests.cs`:18 new cases; all14 existing
   cases remain (32 total). Synthetic slow valid evidence does not award60FPS;
   wrong/restored resolution, mode, cap, missing coverage, batch/nongraphical and
   runtime errors fail. Tests call the report builder, NOT Quit/file output or
   window-changing APIs. These new native tests are authored/compiled only.
5. `Tools/baseline.txt`: Edit floor3229->3357 (+110+18); other floors988/77/2182
   unchanged. Future merge must retain integration's11 QA Edit/4 Play extras:
   combined3368/992/77/2182. No excluded/removed cases or lowered checks.
6. `WAVE_D_COMMITMENT_AUTHORITY_DESIGN.md`: source-reviewed canonical authority,
   source-policy/consent/deadline preservation, all creation/settlement/readers,
   deduplicated incidents, mixed-family price links/caps, disabled empty schema24
   migration and acceptance requirements. A design, not live gameplay.
7. This evidence record and fresh top notes in `UNITY_PORT_ROADMAP.md` and
   `UNITY_PORT_IMPLEMENTATION.md`; historical records preserved below them.

No current schema/model/dispatcher, simulation rules, historical freezers1-22,
fixtures, pure registrations, scenes, packages, settings, local UMA define,
visible primitives, source-control configuration or normal saves changed.

## Every external workflow/evidence change

- `work/orchestration/run-owned-player.ps1`: require requested and actually
  sampled1080 Windowed, exact integer coverage/mismatch evidence, observed
  uncapped/VSync0 and explicit unassessed performance. Final screen after Season
  is informational. Propagate all evidence fields into terminal profileMetrics.
  Retained old900p reports are deliberately rejected, never relabeled.
- `check-owned-player-runner.ps1`:15 new synthetic operational cases (+24 prior
  =39), including fullscreen at identical pixels, mid-sample changes, mistyped
  counters, unjustified FPS claim, final-screen distinction and slow diagnostic.
- `owned-player-runner.md`: new requirements/evidence, preserved historical
 24/8 records. No real player launch was performed by these workflow checks.
- New task-local `VerifyFrozenV23Contract.cs` and scoped
  `run-frozen-v23-contract-probe.ps1`: invoke the actual110 NUnit methods under
  managed Mono, never copy validator logic. Refuse source mismatch, active
  NoUMA references, old evidence directory, unsupported lifecycle/source/excluded
  cases, incomplete execution or changed source/DLLs. Retain hashes/results.
- Task-local `CONTINUATION_STATUS.md` retains ownership, failed native evidence,
  repaired historical wiring, new preparation and remaining acceptance gates.

External native controller/helpers were unchanged while root96249 runs. No
second native/player/editor owner, MCP mutation or active NoUMA cache access.

## Evidence, kept separate

Offline compiler root19680 CLOSED0,8/8 fresh assemblies/0errors using ONLY idle
UMA cached references, explicit Windows PowerShell5.1. Output
`C:/Users/kelli/AppData/Local/Gamesim/offline-compile/d1653f9bc7e84e5f99d96c12ed78dc49`.
Log `D:/CodexGamesimEvidence/integration-20261004/wave-d-preparation-offline-01.log`
SHA25680929ed6361eef57c254180d8ac2912932928f9f2475a1e83e9707072f9560de.

Focused managed Frozen23 diagnostic:110/110 pass,0failed,exit0. Source pin is
c98c0ee plus the explicitly hashed preparation files, not a native final commit
acceptance result. Run `wave-d-managed-frozen23-01`; bindingSHA
43806287c6264a7177c0b14e5ba5a36a74fdfc46cd5e06a49bcd2477027d80ef;
validation log0e62902af5376dfc6a4e89892981562a95c33007dd3ccfdd91f6af149883717a.
Harnessc20e9c41bbae1fb2af872ea89af159aab91fdc21f82fc01c03a5287fd2f14507;
launcher60385c152a4a38ad8463ddf84bd6b4d564fd0a5c27bf41a3e7c2434cbcb3cf11.
The probe binds source and precompiled DLLs separately; compiler logs/provenance
support this diagnostic pair, but the runner alone cannot prove arbitrary
source-to-DLL provenance. It is NOT a source-pin/native acceptance gate.

External launcher checks39/39 pass,exit0,zero PS5 parser errors. Retained
`owned-player-runner-checks/20261005T134628708Z/checks.json` SHA
8a0d9238bb1090f3d51b64e94addee0c54cb19abfe9edb16e411c1ec0eccc6fb.
Telemetry unchanged8/8 pass,exit0, `owned-player-telemetry-checks/20261005T134744170Z/checks.json`
SHAcedb74acfd4433ee031ef7c1f2c24177111e9f47f9a02aa6d48f81c5395925eb.
No Unity or game launches for these synthetic checks. Current runnerSHA
0b0802ccacb7a8cb72591a774c47284919bea11c8cbdc956cdce7a2b986e5373;
checker9254779c1283aebf96050956ab6aaee4fee6d5ad369fb9d9c38a9b7641aec1f3.

Independent reviews found no remaining blocker; root inspected diffs, exact
counts, source hashes and unique metadata. Root's measured-interval fix is
separate from the initial review hash and compiled in the fresh8/8 check.
Final source: freezer4c7bb5c7572c3aa605b5822db85cada151722349e6f87f11a39143ffa86bda47;
freezer tests51a8b045239bd636c7d9a0e128b791bc5c32a3c3368a996eb91bb3f1e8e8428b;
profileb41aa6e779dadac56b49fe4122dcbaad6a23cad338b16cbd4f43c6f156bf8418;
profile testsc17267c76a1e4d660736e16c886572399355caf3976cd7347c5a7edd4cb3b3e9.

No fresh pure run claimed: pure simulation/source registrations unchanged.
110 managed cases do NOT imply128 new native cases executed. Native current
g23n1 Edit is still3230/3240 pass,10failed (historical wiring repaired separately);
the whole combined Play/controller remains active. No shipping build, actual
graphical sample,1080p60, visual or human acceptance in this increment.

## Limits and next dependency

Frozen23 freezes the new23/widened lobby contract. As in the existing Frozen16-22
pipeline, unchanged story/ledger semantics still need unchanged D1-off live
validation after migration. Do not broaden old DTO meanings under schema24.
Current reducer/synthetic fixtures are NOT retained schema23 shipping-save
golden envelopes; capture/verify that corpus before claiming compatibility.

Frame durations are Unity frame-loop measurements, not GPU/OS Present timing.
Current display observation is not an independent proof of the previous render
at a resize boundary; the first duration straddles the sampling start. Do not
use measuredSeconds as an exact sum of sampled intervals or FPS denominator.
Actual1080p60 GTX1060 needs shipping samples/full-cast stress, sensible precise
timing/startup/memory evidence and a separate sustained presentation check.

Finish root96249 to whole controller closure first; preserve results/captures.
Then deliberately integrate the historical wiring repair plus preparation and
verify new exact combined pin with full NoUMA/UMA and a separate desktop build.
Continue D1 canonical safety authority/schema24 only after this prerequisite;
other commitment families complete B1 later. All-week NPC strategy, negotiated
alliance meetings and secret leaks/double-dealing remain required. Wider E2,
consistent actual assets, performance/visual and0of3 first-time playtests remain.
No live/remote promotion, PR10 operation, purchase, account/cloud/AI connection,
access-control change, force-close or retained-data cleanup was performed.

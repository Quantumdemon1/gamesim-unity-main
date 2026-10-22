# Gamesim Big Brother — implementation handoff

Updated 21 October 2026, after the 20:40:46 UTC preservation checkpoint.
This is the current entry point. Older LIVE/pending entries in the append-only
ledger are historical; do not restart them or treat private baseline commits as
the integration queue. The port is not complete and the new work is not yet
qualified for promotion to GamesimPlay.

## 1. Start here: current state

GamesimPlay remains the user's main playable copy. GamesimConsolidation is the
working source/integration authority, not a replacement user play project.

| Location | Purpose / current state |
| --- | --- |
| D:/GamesimConsolidation | Authoritative dirty source; HEAD f1b93b1c64972b9e0491f0a27e1ebe5771dbe8de; 156 status rows, none staged. |
| D:/GamesimAcceptance | Isolated native validation; working test observer withdrawn, but Library/results still belong to recorded diagnostic runs. |
| D:/GamesimPlay | User play copy, same HEAD, six preserved font/settings edits and saved EpisodeHouse; not updated by this round. |
| C:/Users/kelli/Gamesim Big Brother | Original project and shared Git metadata; do not assume it supersedes the verified D: source. |

Evidence/work root:
C:/Users/kelli/Documents/Codex/2026-09-09/github-plugin-github-openai-curated-remote-2/work/gamesim-current-r2-integration-20261018.
The original web reference/staged port remain in this chat's existing work/gamesim
and work/unity-port-next. Do not create another project/task or duplicate them.
Continue the existing UNITY_PORT_ROADMAP.md and UNITY_PORT_IMPLEMENTATION.md,
with this handoff and the newest evidence correcting older pending descriptions.

No owned Unity Editor, compiler, native test, controller or watcher remains live.
The Unity-named Hub utility is not an Editor. MCP previously reconnected to
GamesimPlay/EpisodeHouse, and that saved idle editor was normally closed with
permission. This is not a current live-MCP health claim. On the next live Editor
session, verify project root, state, active scene and Console read-only before
mutating; never clear the Console merely to get a clean-looking result.

Current complete source manifest:

- r2-v2-observer-withdrawal-current01/source-withdrawn.json:
  2,153,535B / SHA256 60F35497A1C6E66443BA56A1D92FAFD608067EDDA930030E890BCF397BCACC57.
  Exactly 9,634 files / 410,175,125 bytes.
- Matching tested composite: tested-withdrawn.json,3,437,980B /
  F59C16DEDD384332C4199C921931C3B434B5B0131519B4B03A075E1FC34BFA9E.
  Exactly 15,770 files / 1,873,201,453 bytes: 15,252 physical Acceptance files plus 518
  Tools files in the CLOSED diagnostic snapshot. Acceptance/Tools is ABSENT.
- Actual receipt-withdrawn.json,14,443B /
  49B543975FABFB180218D262E167544487AE55972DE88DB4E665BE4CC92655AF.
- Physical index is C:/Users/kelli/Gamesim Big Brother/.git/worktrees/GamesimConsolidation/index,
  SHA256 9AA8C18E47A0D76C52FFE6B24404E3F2907B2E18C51A34D87F38BB928A75C8C9.
  Nothing was staged, committed, reset or published.

## 2. Completed work and exact limits

### House-context lifecycle

The portrait queue now binds to its actual creation scene before DontSave and
retires only its owned work/preview when that scene unloads. Cached pixels,
borrowed providers/controllers and foreign resources are preserved; late
callbacks are terminally guarded.

Original two integration tests, three new scene-owner tests and five Edit tests
passed in separate controlled runs; the original 52-case cohort then passed six
batches of 10/10/10/10/10/2. Complete logs contained no recurrence of the targeted
PlayableGraph warning. Independent full custody/log reviews and root acceptance
exist. This closes that bounded reproduction, not every possible animation graph
path, every native suite, or zero-allocation shutdown.

Anchor: R2_PORTRAIT_FULL52_ROOT_CLOSED_BOUNDED_ACCEPTANCE_20261021.md
(SHA7ECF1C429FA5F2E4CF3EA06798D5869163EFC57BE6C50C3C868E7C0585EF12CD).
The original main Play test file is still the uninstrumented 68,865-byte / 3CB4 source.

### Combined dialogue V2 source integration

The reviewed combined patch, not private baseline commits, was admitted as six
replacements/fourteen additions: public veto-rescue identity/evidence validation,
bounded cited-record dialogue, closed V1/V2 story recognition, campaign content,
existing UI projection, two test fixtures and eight raw fixture products.
No role/AP rewards, shipping-default change, schema change or scene rebuild was
introduced. R2_V2_SOURCE_ADMISSION_ACTUAL_20261021.md lists every source change.

The first full 96-case run failed: 17 passed / 79 failed. Of the 79, 68 were Parent/OneTimeSetUp
blocked and never executed, 10 ordinary cases failed input materialization before
Apply, and one signed-zero comparison executed and failed. Do not call them79
observed gameplay bugs.

Both test fixture readers now use ObjectCreationHandling.Replace. Default
population had appended four fixture counts to four constructor counts. The
production fixture producer already used Replace. No fixture, original assertion,
case, strict comparison, production serializer or expected output was weakened.

The targeted 37-case diagnostic then executed 10/10 focused, 10/10 ordinary and 16/17 strict
ordinary controls: 36 passed, one signed-zero failed, zero skipped. All three
native processes closed naturally in under 97 seconds each; controller/capture/watchers
closed. Failed runs, snapshots, logs and raw references remain untouched.

Root diagnosed the remaining failure: the installed Json.NET JValue(double)
constructor calls a cached DoubleZero boxing helper, so the negative-zero test
input becomes positive zero before comparison. Direct bit conversion was correct;
the strict comparator correctly compared two positive-zero token values.
Installed DLL metadata was inspected without loading/executing that target DLL.

### Cleanup finished at this handoff

The temporary 1,949-byte signed-zero log block was withdrawn from both working
ordinary test files. Each exact 3D1757 preimage became 3F0D. Both Replace fixes
remain. Source now differs from admitted B0C only by those two 69-byte settings.

Actual project changes in this cleanup were ONLY:

- D:/GamesimConsolidation/Assets/Gamesim/Tests/EditMode/StrategicOldControls05ParityTests.cs.
- D:/GamesimAcceptance/Assets/Gamesim/Tests/EditMode/StrategicOldControls05ParityTests.cs.

New Task-only records, proposed patch, handoff and ledger updates do not alter
the game. R2_V2_OBSERVER_WITHDRAWAL_ACTUAL_20261021.md contains the complete
before/application/after evidence. No compilation or native qualification
followed withdrawal.

## 3. Immediate next increment: fix fixture construction and qualify 96 cases

1. Refresh current source/status/index, saves and editor/compiler ownership.
   Check the exact 60F354 source boundary rather than assuming this note is fresh.
2. Finish independent review when capacity is available: reviewers hit a usage
   limit before final closed37 tuple/ancestor and PE/factory reports were written.
   Completed independent message findings are documented, but no missing final
   report/acceptance may be fabricated. The withdrawal transports had a completed
   independent static review; its fresh actual custody is root-observed only.
3. Review the staged Task-only proposal:
   r2-v2-signed-zero-boxed-factory-next01/PROPOSED.apply_patch.txt,
   718B / F485B515E1642B584ADE32A68DA445A9E282B81B88EF8946C1E7B3F97FF59F23.
   It changes only both Floating factories from new JValue(value) to
   new JValue((object)value). It is NOT APPLIED, compiled or native-qualified.
   Do not weaken the comparator or replace the signed-zero assertion.
4. Exact source preimages before any application:
   DialoguePublicRescueV2Tests.cs55,049B/FAFD32519F74CF81A3699DE76DDAFF39D0CC25080EF0BA28573F2C33C4AEBACC;
   StrategicOldControls05ParityTests.cs16,580B/3F0DD8BD4A4AACEAA36AD7007CC6EE96C257C15231C83D7D5BB9D2329AF0F9BE.
   Derive/measure the whole new boundary and verify no other source changes.
5. Rebind the existing capture/verification adapters truthfully to the actual
   corrected source. Older guards pin47E4/1D91,88CED or earlier receipts; do not
   waive mismatches, edit old records or blindly rerun occupied wrappers.
6. Run the original uninstrumented 96 cases: 24 methods, 15 plain / 81 parameterized, in
   nine method-atomic groups of 10/11/10/10/10/17/1/10/17. The old <=10 planning target
   was not the user's time limit. Existing route:
   r2-v2-method-atomic-native-roster-next01/v2-method-atomic.suite.txt
   SHA F55485B3A74CD4D3D76236DDE1EBED6D8B1C83FA3628C2EE73D99A077AAF7140;
   roster.json SHA58CD4025B296C0E40D127319BC40E244E0E124430ACF244786164A69D17595FA.
   Use actual typed XML tuples and ancestor results, not guessed FullNames.
7. Accept only exactly 96 executed passes with no missing/duplicate/skipped/Parent
   cases, whole process/stream closure, complete shutdown logs, and fresh
   source/vendor/save/Play custody. A green subset does not close this gate.

Closed diagnostic root to preserve:
D:/GamesimAcceptance/Logs/validation/r2-dialogue-pilot-v2-materialization-diagnostic-three-batch-native02-20261021T200817234Z-55eff31d36ee42f7b297c0096029894b.
Actual closed evidence: source-final88CED/tested-afterECCE; three XMLs36/1;
controller-end8FF04F/root-after2E145 in the Task capture folder.
Read R2_V2_MATERIALIZATION_DIAGNOSTIC_CLOSED_ROOT_FAILURE_20261021.md for the
actual numeric observation, DLL pin/IL and limitations.

## 4. Historical-reader and save gate — BEFORE role/AP changes

This is OPEN. Generated ordinary controls or newly fabricated saves cannot prove
the required funded historical path. The user waived unavailable personal mode0/1
legacy saves; do not ask again or misrepresent generated files as those originals.

- Rebase the prepared funded-capture overlay onto the actual corrected focused
  reader, preserving Replace and the boxed factory. Its old47E4 preimage is stale.
- Capture actual F immediately after real public Cite through a legal entry and
  legal non-Alex Talk. Observe AP0; do not substitute the AP1 Cited helper.
- Retain both genuine current SaveWire envelopes and real PRIMARY TryLoad and
  public TryRecoverBackup results in a unique owned temporary save store.
  Never use the player's original save folder for this experiment.
- Follow r2-public-rescue-v2-next01/FUNDED_PARITY_CONTRACT.md. W is F.AfterVeto;
  O is outOfPhase.
  C differs only W-minus-one to fund the old paid control. The old private
  three-argument static Lobby receives explicit null; preserve its raw output.
  The separate expected mapping changes only O-minus-one, not W a second time.
- Evaluate the four preselected main-RNG-only receptive/open/skeptical/hostile
  variants. No rerolls, changed voters/rules/cohort or manufactured terminal oracle.
- Independently compare exact numeric/state/RNG/read/terminal/FIFO/revision/
  accepted-ID behavior and the current AP0 refusal. Resolve any real mismatch
  before changing costs, rewards, roles or save semantics.

Prepared funded authority is NOT activated; actual F, envelopes/recovery and
old private-leaf terminal evidence have NOT been produced. Old-only raw producer
draft02 is statically reviewed but uncompiled/unexecuted/unselected; it still
needs exact actual-F/variant inputs, bounded owner/parser integration and review.
Do not run held E143/old templates blind. Detailed gate:
R2_V2_FUNDED_F_SAVE_AND_HISTORICAL_PARITY_DEPENDENCY_CHECKLIST01.md.

## 5. Remaining gameplay, story and NPC intelligence

Work in this dependency order, preserving already integrated behavior:

1. R2 consequential dialogue: vote-word/memory/pressure/neutral branches,
   truthful evidence/category choices, stale callbacks, zero-time exits,
   actual numerical consequences and exact save/resume. Verification adapters
   must enumerate hidden category choices for read, deal and oath; silently
   skipping a choice is not coverage.
2. R3 unified commitment follow-through: explicit consent, one canonical
   obligation, fulfillment/breach/lapse, Your Word/Your Week counting, jury
   and finale receipts. Closure is not fulfillment; survival is not proof
   a vote promise was kept. Do not leak secret ballots.
3. R4 role/AP rules: bounded capability, caps, expiry, conserved rewards,
   refusal atomicity, duplicate/stale replay refusal and anti-farming.
4. R5 fresh-season defaults: commitment/story rules active from the start
   as the user selected; retain legacy mode0/1/2 meanings and migrate safely.
   Schema29 is coordinated here; no other chat owns it.
5. Qualify all-week NPC strategy, negotiated alliance meetings, deeper secret
   leaks and unified commitments; all four are required for completion.
   Measure decisions/coherence, not just dialogue frequency or a source count.

Re-measure balance after rule changes. Owner-selected target is ATTEMPTED
accepted-deal counter success of 35–50% (include zero-member attempts, exclude lapse).
Proposed tuning bands chosen under delegated judgment: average-skill player
studying weekly, HoH and Veto 60–70% each; season wins with 8 contestants 25–35%,
and 12 contestants 18–28%. Keep prediction, member conversion, deal carried and
season-win metrics distinct; record seed/cohort/before-after evidence.

All-Star content brief and independent design review exist, not twelve shipped
campaigns: Cody, Dan, Will, Derrick, Janelle, Danielle, Vanessa, Tyler, Chelsie,
Rachel, Xavier and Jun. Start Cody's One Vote at a Time only after generic/save/
canonical commitment gates. Each needs four authored beats, refusal and exit,
specific character/history linkage, legal eligibility, truthful consent,
save/resume/settlement/finale receipts and ballot privacy.

Use ALL_STAR_STRATEGIC_CAMPAIGNS_CONTENT_BRIEF_20261021.md and
ALL_STAR_STRATEGIC_CAMPAIGNS_DESIGN_REVIEW01_20261021.md. Proposed pacing:
at most two alumni campaigns per season, one bespoke active at once, one opening
per week; no repeat opener after decline and no displacement of ordinary stories.
Measure frequency, repetition, pool starvation, NPC coherence and B9 balance;
do not force all twelve campaigns into every season.

## 6. Real art, assets, layout and competitions

Ownership-confirmed Review handoff supersedes ONLY the README's older ownership
acknowledgement. GameSim Dev retains storage bins and all shared-scene integration.
Delivered batch:
C:/Users/kelli/Documents/Codex/2026-09-20/re/outputs/gamesim-props-20261020.

Verified READ-ONLY: 14 house models / 16 placements, 39 competition parts / five
families; 108 overlay files, 19 canonical packages; 439 bundle files / 438 manifest
rows, four current guards and patch applicability. Five NoUMA assembly passes
are historical offline evidence, NOT current native import/UMA/runtime approval.
PROPS_EQUIPMENT_HANDOFF02_READINESS.md is the entry point.

Remaining:

1. Recheck source applicability and current ownership; complete exact package
   verify/admission dry-run and license/validation disposition through the
   available asset-vendoring skill. No provider, paid call or account change.
   Retain 17 inspector advisories; LicenseRef-GameSim-Original is not CC0/MIT
   or blanket public redistribution permission.
2. Admit exact reviewed overlay/packages with preserved before/after custody;
   Unity import/material/mesh-kit generation and new GUID/meta review are
   separate measured stages. No whole-house rebuild.
3. Run the ordinary 28 house / six mesh-kit cases plus existing apparatus tests,
   and the EXPLICIT installed-kit/no-fallback gate. Ordinary fallback passes
   do not prove the real package is installed or rendered.
4. Adopt ONLY the 16 named saved-scene placements and verify RoomFinish/saved
   references. Optimize contact, clearance, camera sightlines and navigation
   with real assets/UMA; inspect rendered results, not only transform values.
5. Finish eight outstanding placeholder placements: three storage bins,
   two shoe boxes, basket, suitcase, gift box. Laundry/shoe/suitcase/gift
   research is not an ownership reservation or delivered asset.
6. Qualify competition equipment in actual playable challenges and review
   challenge instructions, scoring/readouts, failure/cancel flow, clarity
   and balance. A mesh kit or concept alone is not a finished minigame.

No new canonical admission, native import, saved-scene adoption, art render
approval or Play update occurred from this received batch in this round.
Preserve the storage-bin package/dry-run/build receipts and exact scoped approvals;
they do not authorize arbitrary later project writes or paid generation.
Use consistent high-quality real assets, not primitive replacements.
The user's farewell choice is a shorter beat, not a forced full 3.8-second hug.

## 7. Combined validation, desktop and Play promotion

Keep these evidence levels separate: source implementation, source-fixture
parity, actual Unity runtime tests, standalone profiling, human acceptance.

- Current declared cumulative floors are 8,558 Edit, 1,342 main Play and 129 UMA,
  not current full-suite passes. Edit = 8,253 historical + 199 dialogue + 2 studio +
  8 emotional-plea + 96 V2. Play = 1,281 old + 44 distinct generic + 14 real dialogue +
  3 scene-owner. UMA = 90 old + 38 triangle + 1 graph.
- Preserve all historical occurrence multiplicities, including 49 duplicate
  Edit FullName pairs; do not aggregate with a set. The one accepted named
  rule-one semantic migration is documented; it is not a general floor waiver.
  Use R2_CURRENT_NATIVE_COVERAGE_DATA_ONLY_BRIEF.md. An older 61m27s Edit run
  cannot satisfy the current 45-minute rule. Prepared LPT partitions are estimates,
  not timing guarantees. New admitted tests are cumulative additions.
- Every original native has a strict 2,700-second deadline, ZERO grace measured
  from original Process.StartTime. Use exact owned handle/birth/image supervision.
  No broad kill, unowned compiler kill, renewal, silent retry or late acceptance.
  Existing strict owner915865 and explicit selectors are reusable building blocks;
  historical FE18 native owner with late grace is not an acceptable substitute.
  Adapt bounded watchers to the correct number of children; three is not nine.
- Inspect full logs after the final XML marker. Recent logs retain certificate,
  licensing404 and UTP allocation diagnostics; don't call them pristine or
  zero-leak. Compare against the retained baseline and disposition real failures.
- Accessibility: qualify Largest text on every surface (including1.2 cast chrome
  coverage gap), caption dwell/sound cues/separate sliders, truthful rebound key
  captions, navigation/focus and actual 4:3 / 16:9 / 21:9 combined layouts. Prior
  implementation or screenshots do not close whole-build UX acceptance.
- Produce a separately verified nondevelopment Windows64 build. Walk new/load,
  backup recovery, a complete season, jury/finale, save/resume and clean exit
  on that exact build, with real UMA and admitted assets.
- Actual user target: GTX 1060, 1920×1080 at 60 FPS. Run three serial 300-second
  standalone 6/12/16-cast scenarios, VSync 0 / uncapped for measurement,
  p95 frame time <=16.7ms and p99 <=33.3ms. The 1600×900 prototype timings are not
  shipping acceptance. Do not profile while another heavy game/test is running.
- Three first-time human playtests are required; currently 0/3. User can arrange
  testers once the finished pinned build exists. Automated walks are not humans.

Only then promote the accepted checkpoint to saved/idle GamesimPlay. Recheck
its six local edits and saves, preserve a recoverable baseline, apply a reviewed
bounded source/settings/scene projection and verify the actual installed build.
HEAD is already the same and new work is dirty/uncommitted: an old ff-only command
alone will NOT transfer this implementation. Do not overwrite the local fonts/
settings, publish the separate Unity project, or imply an unconfigured remote.

## 8. Preservation and stop conditions

The fresh 20:40 checkpoint preserved ALL 378 backup files, 117 current Gamesim saves,
one Houseguests character file, six Play font/settings edits, saved EpisodeHouse,
6,140 vendor files, 399 protected controls, old 428F / new 50944 retained PNGs and
the current 50944 working PNG. Original analytics/TestResults/log copies are retained
among the 378 backups; their CURRENT generated counterparts are not player saves and
can legitimately differ. Normalize mixed slash paths before selecting categories.

The tiny cleanup checker wrote only fresh Task records. It did not acquire an
Editor, compile, refresh assemblies, copy source, alter account settings or
publish anything. Do not rerun its occupied before/withdrawn transaction.
Its projectWritten=false describes the checker, not the explicit two-file
apply_patch withdrawal already disclosed.

Stop/report for real custody drift, essential license/authority conflict,
unowned active project edits, actual failing qualification, unavailable essential
review, or human acceptance still required. Preserve partial/failed evidence
instead of resetting, deleting baselines or declaring completion.

Current blocker: signed-zero fixture correction remains proposed, and independent
final review capacity was exhausted. No user credential, account change, purchase
or new design answer is needed to start the next safe increment. Once reviews
resume, fix/qualify the 96 cases first, then the historical/save gate, then the ordered
gameplay/art/All-Star/full-build queue above.

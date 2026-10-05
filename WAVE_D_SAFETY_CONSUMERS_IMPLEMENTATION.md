# Canonical Safety conversation, negotiation and presentation consumers

2026-10-05 UTC. Implemented in the existing isolated `D:/GamesimWaveC` checkout
above `a14bab3530bba85000a08566d925acd250ad2a7b`. This is another dependency of
the required complete D1 system, not activation, a completed port, or shipping
acceptance. The full D1-D4, real-asset and actual GTX1060 windowed 1080p60 scope
remains unchanged.

## Every material production change

- `Simulation/HouseDialogue.cs`: direct active player-to-speaker Safety promise
  acknowledgments include detached canonical PromisePolicy provenance. Rule1
  selects resolved direct promises by actual settlement week (creation-week
  fallback when a legacy outcome has no timestamp), then ordinal stable ID.
  Future/other-party rows are excluded. Rule0 keeps original reverse saved-list
  behavior. DealPolicy never masquerades as a unilateral promise.
- `Runtime/Presentation/DecisionContext.cs`: promise comparisons include the
  player's own canonical provenance in both directions, retaining source order,
  status copy, limits and private-pair filtering. Deal comparisons were already
  routed; no replacement of that completed work.
- `Runtime/Episode/EpisodeDirector.cs`: actual notebook promise records use
  detached references, still bounded to the player and labeled as authored.
- `Runtime/Episode/EpisodeDirector.Journal.cs`: actual profile promise records
  include the canonical direct pair, preserving creation-week labels and copy.
- `Runtime/Presentation/RelationshipWeb.cs`: actual relationship column lists
  canonical own-pair promises and binding deals without showing NPC-only rows.
- `Runtime/Episode/EpisodeDirector.Conversation.cs`: the displayed player deal
  ceiling uses the same historical family count as command authority, including
  NPC, declined and ended deal rows; promises do not become deal slots.
- `Simulation/Negotiation.cs`: canonical actual player/wronged incidents supply
  refusal penalties, mend allowance and caption history. Deal-family chance
  terms require deal evidence; promise-only incidents remain outside those terms.
  Mend counts include both families once per incident. Captions use actual
  settlement and deterministic incident owner. On an equal canonical/legacy
  week, the old legacy-family choice remains. Existing ballot privacy holds.
- `Simulation/NpcDeals.cs`: broken-deal reputation includes canonical incidents
  containing DealPolicy evidence, once per actual actor. It never counts the
  wronged party or a promise-only incident as a broken deal.
- `Simulation/PlayerDeals.cs`: refusal track-record copy includes canonical
  player-broken promise evidence. Existing audible YourWord acceptance policy
  stays unchanged; the no-knowledge fallback uses incident-aware NpcDeals.
- `Simulation/KnownOdds.cs`: own-pair history counts each agreement's provenance,
  not betrayal incidents. Private NPC-only agreement history remains excluded.

No storage/schema, rule activation, family policy, source fixture, knowledge
writer, command/cost/RNG owner, scene, asset, package, vendor, local settings,
normal save, retained build or snapshot changes are introduced by this work.
Production engine/save/factory still refuse unified rule1.

## New regression coverage and tools

- `UnifiedSafetyNegotiationReaderTests.cs` plus unique meta:80 synchronous regular
  cases/26 methods, grouped actor/pair/source penalties and mending, chronology,
  family controls, privacy, agreement history, actual pure settlement/mend effect
  gateways, unchanged input and corrupt-key refusal.
- `UnifiedSafetyPresentationReaderTests.cs` plus unique meta:29 synchronous
  regular cases/12 methods, actual prospective evaluator and direct dialogue,
  direction/source/term controls, durable outcomes, original legacy ordering,
  actual settlement chronology, fallback dates and ordinal ties.
- `UnifiedSafetySurfaceTests.cs` plus unique meta:20 synchronous regular cases/
  15 methods, actual DecisionContext, static director deal ceiling, private
  profile method on an isolated component, and built relationship-web controls.
  These require native Unity before they are called passing.
- `Tools/SimulationTests/SimulationTests.csproj`: registers the two pure fixtures.
  DecisionContext was not pulled into the pure project: its actual HouseVibe
  dependency uses Unity. All ten DecisionContext cases and original summary
  privacy assertions live in the native surface fixture; no Unity stub or
  unrelated HouseVibe refactor is substituted.
- `Tools/baseline.txt`: isolated floors4113 Edit/991 Play/77 UMA/2859 pure.
  Future combined MUST retain integration QA11 Edit/4 Play:4124/995/77/2859,
  plus future additions. No existing cases, floors or source assertions lowered.
- Task-local `run-wave-d-consumers-probe.ps1`: only evidence prefix, two added
  synchronous fixture names/allowlist message, and diagnostic binding label
  differ from the retained Summary helper. The native surface fixture is not
  allowed in its Mono list. Ownership/cache/deadline/hash/source guards and the
  shared harness remain unchanged. Independent reviewed SHA256:
  `83573b1f9c8e0e0adac30e11835bc15768f59f6948fa8a43a7e28da4ee85ca82`.

## Actual old native failure and scoped test repair

The separate bb0f21e7 NoUMA g24n2/root12232 fully CLOSED FAILED at19:16:51Z,
elapsed5940.08s. Edit3826/3826 passed; Play994/995 passed,1 failed,0 skipped,
Unityexit2. Drift, cleanup errors and unowned descendants are empty; all retained
Unity/worker/relay handles naturally closed. Never poll or restart root12232.
Evidence remains in
`D:/CodexGamesimEvidence/orchestration/full-suite-runs/g24n2-20261005T173751616Z-42446a19`.

`Negotiation_ACounterStandsAtTheHeadOfTheConversationAndIsAnsweredThere` failed
at its one-command assertion: expected revision1, actual0. The test retained its
yes/no buttons before CaptureConversation. Actual CaptureFraming explicitly
re-renders the HUD for each frame and afterward; Pressable correctly refuses
the retired control. Root repaired only that existing Play test to re-find the
current answer after capture and assert active/interactable before invoking it.
All transaction, two linked deals, decline, caption/tag/layout and capture
assertions remain. No runtime stale-control safeguard was relaxed. Fresh native
execution is required; compilation alone does not establish the repair passes.

## Independent review and fixture preparation

Independent static source/fixture/helper review is clear after repairs. It found
the remaining PlayerDeals raw promise reader, now routed, and source-scale test
assumptions: six-person ballot tally, actual bounded witness count, and hearsay
threshold. The fixture now uses three distinct actual incidents for above-20
hearsay and separately checks one incident's below-threshold track-record copy.
These corrections preceded execution, not hidden passing-test substitutions.
The reviewer independently confirmed the screenshot/rebuild/stale-control cause
and that the scoped existing-test repair strengthens control eligibility.

## Verification evidence

Source frozen at a14bab35 plus the reviewed diff.958 C#/asmdef and2 tool inputs
are bound in `wave-d-consumers-input-before01.json`, SHA256
`932a7f3faa87eed040b5ca5ec900543038a995cd70cb714489c4a795f86395d2`.
First compiler/root47084 CLOSED FAILED on the new Surface fixture's direct TMP
compile reference. First pure/root3826 CLOSED FAILED:2858/2859 regular cases
passed, one new gateway fixture expected two history items but omitted the
source promise routine's actual player-owned memory. Both failed artifacts stay
retained. After both closures and unchanged-input verification, only those two
new fixtures changed: actual installed TMP font/text/build reflection, and exact
agreement-plus-owned-memory assertions. No assembly/compiler/stub change or
weakened historical assertion was used. Independent re-review is clear.

Final before02 and after02 bind958 C#/asmdef and2 tool inputs, with identical
SHA256 `62604494386b805dd28fcede2441b21298b999a1d9ec30ae07d6684be6aa8c82`.
Source inventory, input hashes and HEAD remained unchanged through all checks.

- Root65986/offline02 CLOSED0:8/8 fresh assemblies,0 errors; isolated output
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/d6d893a469fb4c7a83a533fdbc0b0f3a`.
  Log SHA256 `465f30ccddf25aae44229e1dfb5fa7c2134d6f1506bb11cb849bab5bf5bc2d78`.
- Root66362/pure02 CLOSED0:2859/2859 executed regular cases,0 failed/0 skipped;
  13 existing Explicit diagnostic reports remain unselected, not additional passes.
  TRX SHA256 `feae94101d99457c2acd9f96085a19b7ed0eaf7d27fbda78d0e30fe3acf29c15`.
- Root99128/managed24-02 CLOSED0:846/846 focused authored cases,0 failed,
  17.23946 seconds. Binding SHA256
  `1f92ef9ae0d568492d540f87f11c9d4e4e3bdcee811e149ccce8433c4abcbd3b`.
- Root47850/replay24-02 CLOSED0:55 scripted seasons/5956 attempted transitions,
  105.73096 seconds. Every paired digest and attempt count equals retained
  Summary replay24-02, with unchanged harness/Mono/compiler/original22 fixture.
  Comparison SHA256 `4527a81f6aaf2327eb7e445e3da3c903cfa6c04d6b0a2f24f30a5f01d449c0d3`.

All six first/final check handles above are closed; never poll or restart them.
The 20 new native surface cases and existing counter repair are compiled, NOT
executed by these checks. Managed diagnostics do not establish Unity lifecycle,
disk persistence, enabled seasons, shipping, performance, visual or human gates.

## Live project verification and preservation

After whole native closure, read-only MCP confirms the actual C: Gamesim project,
EpisodeHouse loaded/21 roots/not dirty, editor idle. Error query returned0;
Console also contains existing Male_Unified gizmo-preview warnings and shadow
atlas downscaling logs. Nothing was cleared or claimed warning-free.
Live dirty source/font/scene/UMA/settings/recovery files remain untouched.
Integration still bb0f21e7 with only local UMA settings, SHA256
`eb0525c7a30cb48787468e352cc3b8b5a66497c776eefd5292b6eb8c48e510e3`.
No live or remote promotion, PR10 operation, credential logging, account/cloud/
AI connection, purchase, access-control or deployment action occurred.

## Remaining completion gates

Safety story progression/casting and active/kept summary consumers; actual
incident/listener hearing policy; full enabled-save identities/link/reference/
audience validation and genuine historical shipping-save corpus; native command
transactions/cancel/stale callback/save-failure/disk reload, then fresh-only
activation. Native notebook lifecycle remains unproven by the component tests.
Other D1 families, all D2/D3/D4, wider contextual balance, coherent real assets,
same-pin full NoUMA/UMA, separate shipping build, actual windowed1080p60,
visual/accessibility and0of3 human acceptance remain required.

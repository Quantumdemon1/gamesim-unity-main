# Exact public schema25 frozen contract

Implemented and managed-diagnostically verified,2026-10-06 UTC. Isolated DWave,
not live or integration. The former contract is pinned to public Safety commit
f4566b69afc10c1008d64e112c12ed39241caa36. No schema26, migration routing, rule
activation, game behavior, scene, asset, account or remote change is included.

## Material changes

- NEW `Runtime/Persistence/FrozenEpisodeV25.cs`: unused internal
  `Validate(JObject)` entry. Exact frozen shape precedes detached hydration and
  complete frozen semantic/storage validation. Invalid input is refused; caller
  JSON is never repaired, normalized, installed or converted to raw mirrors.
- NEW `FrozenV25Shape.cs`: literal recursive72DTO/540field contract, root88 and
  canonical15. Missing/extra fields, wrong scalar/container types and future Vote
  fields are refused even when null or empty. Shape never comes from a growing
  current DTO; depth64 and numeric contracts are bounded.
- NEW `FrozenV25Data.cs`: independent field carriers, five literal enum ordinal
  maps and only the source read/clone leaves needed by the fixed validator. Active,
  Find and IsStory are fixed readers; frozen ledger defaults preserve actual
  fallback semantics. Field-only serialization does not serialize getters.
- NEW eight exact validator/storage partials: `FrozenV25Validator.cs`, `.Finale`,
  `.Ledger`, `.NpcSocial`, `.SafetyReferences`, `.SocialHistory`, `.Storage`,
  `.Story`. They preserve f456 complete public0/1 dispatch, source-owned references
  and storage bounds. Enabled1 requires active C0 and story knowledge; hearing0/1
  remain distinct. Disabled0 stays empty. Lawful historical dates are preserved.
- NEW four helper snapshots: `.Ballots`, `.Leaves`, `.SafetyLeaves`, `.Vocabulary`.
  Canonical provenance/history/mixed links, hearing emission/listener lineage,
  ballot-knowledge boundaries and source-qualified finale wording remain local.
  Reachable external dependencies are BCL/Newtonsoft only. No growing live
  validators, DTO methods, enums/catalogs, writer/settlement, migration owner or
  inferred ballot/witness authority is consulted.
- All15 production C# files have unique matching NEW metadata. Existing production
  files and metadata are unchanged; no dispatcher calls this snapshot yet.
- NEW `Tests/EditMode/FrozenEpisodeV25ContractTests.cs` and unique meta:166 regular
  cases across19 methods. Real public Apply witnesses cover cast3/6/8/12 and
  recorded0/0,1/0,1/1, promises/deals/breaches/spared outcomes, hearings, mixed
  Safety/rawVote counters, pitches, economy debit, speeches and finale references.
  All three controlled complete seasons reach Finished. Negative baselines first
  pass current simulation/storage and frozen validation, then receive their
  explicit defect; input/detachment assertions remain. Seed searches are bounded
  to32, full-season controls512 commands, mixed/later controls160. Archive-only is
  explicitly a detached lawful pruning projection, not a public pruning command.
  Capacity cases are corruption refusal controls, not full-cap positive histories.
- `Tools/baseline.txt`: Edit floor4868→5034 for166 new cases. Play999, UMA77 and
  pure3399 unchanged; no persistence fixture added to the pure project. A later
  reviewed combined candidate must retain QA11Edit/4Play:5045/1003/77/3399.
- Roadmap/implementation ledger updated with this exact scope/evidence and next
  historical-witness dependency. No existing evidence or baseline is erased.

Fixture parameter2 denotes unified1/hearing1, NOT future unified version2.
No ordinary save is written by these in-memory tests. This is not a migration
acceptance result or execution of the new enabled Play8/evolved native Save25.

## Retained failure and exact correction

Compiler08 session20619 naturally closed1: fixed HouseEventState lacked the source
read-only IsStory getter, CS1061 at FrozenV25Validator.Story.cs139. Only Simulation
compiled; no eight-assembly or new-test pass is claimed for08. Original before08,
compiler output4ddb2c3470e146899d4a1e7671edbb9f and failed log remain retained.
Log SHA256 db440e0075609ec3377fb45e0a186e29321f2a174f122b367e9529259fb743fb.

Only the NEW data snapshot changed: exact `kind == "story"` source getter and
comment, not shape/authority/source behavior. Reachable DTO-property audit found
only Active and IsStory; both are frozen. All29 sibling production paths unchanged.
Failed08 data is transparently reconstructed after the repair, byte-matching its
original recorded SHA18842fe91bd2aaf3def2528f682022205c3e5836cd5e5a35f513a59bf0262985,
in `wave-d-frozen25-failed08/PROVENANCE.md`. An intermediate reconstruction retained
the new comment/blank line and correctly failed its hash check; the final exact
removal matches. No failed artifact is overwritten or retagged as passing.

## Closed exact-image09 diagnostics

Evidence root: `D:/CodexGamesimEvidence/integration-20261004`.

- Before09/after09 captures naturally closed0, identical fileSHA
  d47e714e815ec61112739adc0855a9b282a6f9764af6ea378a1aaff3862f3119 and contentSHA
  91b65cd3e2693d8d4715c071c131189eec954d18f1ea78e21727d49166abb229.
  Observed HEADf456 plus this dirty draft;990C#+8asmdef+2Tools unchanged. All32
  NEW source/meta paths and2Tools also have byte-equal retained copies in
  `wave-d-frozen25-source-before09`. Only data carrier differs from input08.
- Compiler09 session56814 naturally closed0, eight fresh assemblies in
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/132ecb41227a42cca02592b2ab964868`.
  Independent audit:990 current C# exactly once across8 responses, fresh Gamesim
  dependency references in correct order, no stale Gamesim DLL or active NoUMA
  cache reference, only idle UMA cache references. Compiler logSHA
  d0fc80d277c9f307f2e11ccb82afda83d6194022ebced784c8a19bd85e6cd29f.
- Pure09 session35068 naturally closed0:3399 regular passes/0fail, including all80
  public Safety cases.13 older Explicit results remain NotExecuted, not passes.
  TRX SHA3759e6bc28e56ce8e57ce19a1d13504b66019898df49e1f204235be8e0b70ffd.
- NEW focused managed Frozen25 session43883 naturally closed0:166/166 pass,
  0fail,21.8970212s,no timeout. Runs actual synchronous authored NUnit methods,
  not copied validators or Unity lifecycle stubs. Binding
  `wave-d-managed-frozen25-01/binding.json` SHA
  44a1597b73c85b046f8a64634dc3cd9f8beb8848f51d19e4e4edd815318f4866;
  validation logSHA ac22587e2e69912c1e0fc9290eea7b3a58bd86aa0780c686f4e6ad17da538c5f.
  Exact reviewed helperSHA de3209afbe4e0b2d6739a8b3398ff4b0e1ce3b09456c6b6b1f835dbe2ac87905,
  harnessSHA653c2ecbc78f28c99d1314450a64da74cef05db627802f0ef934082aa10358bf.
  Owned hidden compiler30s/Mono180s bounds retain failure evidence. No editor or
  player launched. Unsupported exclusions/outcome/async/lifecycle controls refuse.

Root and independent source review cleared72DTO/540field exact source equality,
all8 validator/storage partials, local helper/read-only closure, literal enums,
all16 unique source/test GUIDs and the166-case fixture. Corrected30-production-file
digest19379c705fd1afaecf3ad45ab6addd4a88e7100492c8f55f932bd67b1bf1afc0;
fixtureSHA ccd0e78f34ebcbe936809b02795719baa40961c063410ea4dfa291a6f7803c93.
Compiler provenance is independently audited, not inferred from DLL hashes alone.
Managed diagnostic/source review, native runtime, desktop and human gates stay
separate. Earlier07 replay/legacy migration results are historical source-bound
diagnostics, not new09 runs or acceptance of an active migration dispatcher.

## Final whitespace-only image10 closure

Scoped staging found six whitespace-only blank lines: three each in the NEW
Ballots and SafetyLeaves helper snapshots. Removed only trailing spaces; the
independent comparison reproduces both files exactly by whitespace normalization.
Other28 production paths, fixture and Tools stay byte-identical. Final30-file
production digest9893a545093c76e23e91d8e948e2b6dc8abd4b7157b975bb15d95a67c2e4670d.
The09 evidence above remains evidence for its original image, not retagged.

- Fresh compiler10 session50426 naturally CLOSED0, eight fresh assemblies at
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/8ed44c5938d74465865b74c20e420c40`.
  Independent990C#/8responses/dependency order/cache/source audits clear.
  Compiler logSHA f0bba59bc64c42c37a3fc3eaf593b872ba2a41004772718617b0088b49bf497c.
- Fresh managed `wave-d-managed-frozen25-02` session27821 naturally CLOSED0:
  166/166 pass,0fail,21.8610986s,no timeout,native launchfalse. BindingSHA
  0062cff930c9f8bde4f58bd99cb384a7eb30455c669b731674096d73adc0adea;
  logSHA ac22587e2e69912c1e0fc9290eea7b3a58bd86aa0780c686f4e6ad17da538c5f.
  Actual166 authored case labels match09; this is a fresh execution, not reuse.
- Before10 captureSHA a368f9ff5a7791b7f6c809acca5f16bfda9f130ea9807c0ce4887779858ed2e6;
  after10SHA15675e12da41c6cbd951a7e76cb293e1a43a8903e4fae7b5d3fdcbb9d5eebba5.
  Equal ordered998source+2Tools and HEADf456; both contentSHA
  8cd312790e2fc624ff3ad3b8247a613507f8b096acd5d4209348acdf114bcdbe.
  JSON file bytes differ in WindowsPowerShell5 versus PowerShell7 indentation,
  not source inputs. Do not claim these evidence JSON files are byte-identical.
- No new pure10 run: the two changed runtime helper files are excluded from the
  unchanged pure project. Pure09 remains its original3399 regular-pass evidence.

## Next dependency and remaining acceptance

Before widening schema26, retain the actual25 opening/checkpoint/full-season JSON
as a source/compiler/procedure-bound historical test corpus. The current fixture
still regenerates through current factories/Apply/DTOs/serialization. After a future
header change, current-semantic rejection could falsely pass merely on unsupported
schema; it cannot define former25 validity. Historical negatives must first pass
fixed25 shape, then fail fixed25 semantics, with original bytes preserved. Neutral
future26→25 projections may be separate synthetic compatibility controls, never
a substitute for captured25 values. Corpus capture has NOT run in this increment.

Only after that dependency: additive detached25→26 migration following exact old
checksum/shape/Frozen25 validation, null target/subtype and empty private ballot
archive, preserving stored0/1/hearing/RNG/IDs/receipts/listeners. No reconstructed
secret ballots or future2 activation until all Vote owners/readers/settlements,
public transactions and native gates are complete. Other D1 families remain.

Integration6e66 remains frozen under separate nativeg25n2 session76183. Partial
Edit4794/4794 passed; Play and whole controller closure/audits remain pending.
Liveb25 remains untouched with existing edits/recovery/baselines retained. No
integration merge, live promotion, Settings/scene/asset/save/build/remote mutation.
All requiredD1-D4, widerE2, coherent real assets/reactions/audio/input, same-pin
NoUMA+UMA, separate desktop, actual GTX1060 1920x1080 at60FPS, visual/accessibility
and0of3 first-time human playtests remain uncompleted full-plan gates.

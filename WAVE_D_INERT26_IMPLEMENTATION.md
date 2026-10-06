# Wave D1 - inert schema26 save boundary

2026-10-06 UTC. Isolated D:/GamesimWaveC above local portable-history checkpoint
4ed8e46228fb9369cb768ae84bdb97199c544a90. Not promoted to integration or live.

## Scope and owner requirement

Fresh playable seasons require commitment and story rules active from the start;
older saves retain their recorded rules. This increment does not change existing
fresh StartSeason selection or activate proposed unified version2. It only adds
strict inactive storage needed before canonical Vote ownership can be integrated.
No inferred ballot, retroactive knowledge, raw commitment conversion or RNG change.

## Every material source and test change

- Simulation/EpisodeState.cs: current schema26, empty unifiedVoteReveals list and
  deep archive clone. Simulation/UnifiedVoteRevealState.cs and .meta are new:
  frame week/ballots, leaf voterId/targetId; lists and leaves detach, malformed
  null containers/entries are preserved for refusal rather than repaired.
- Simulation/UnifiedCommitments.cs: nullable targetId/subtype fields; supported
  Safety rows require both literally null, including rejection of empty strings.
  Existing version1 equality and complete identity/source/settlement rules stay.
- Simulation/EpisodeValidation.cs: literal26 header and shared nonnull/empty
  archive guard for every accepted mode and internal prospective diagnostic.
  Nonempty future proof and unsupported authority remain refused, never cleared.
- Runtime/Persistence/EpisodeSaveValidation.cs: current header26; full common
  validation and all original persistence/storage limits retained.
- Runtime/Persistence/EpisodeSaveMigrations.cs: new UpgradeV25ToV26 validates the
  ORIGINAL complete frozen25 shape and semantics before cloning. It adds only
  targetId:null/subtype:null to existing Safety rows, an empty root archive and
  header26. PrepareV25Payload retains the former dispatcher body unchanged;
  versions1-25 reach that same25 result before the additive step. Current26
  dispatch stays detached, without repairing malformed data. Store ordering is
  unchanged: original envelope checksum, migration, strict current shape, full
  validation, installation. Read-only load never saves migrated bytes itself.
- NEW Tests/EditMode/UnifiedVoteInertSchema26Tests.cs and .meta:29 regular pure
  cases/9methods for genuine factories/public Safety Apply, strict null/empty
  guards, future-mode refusal, exact fields and deep malformed-data-preserving clones.
- NEW Tests/EditMode/PersistenceV26MigrationTests.cs and .meta:78 regular cases/
  10methods,52 in-memory plus26 actual isolated SaveStore disk cases. Pins the
  retained portable package independently; exercises366 accepted aliases,
  70 same-case semantic refusals, all1-25 dispatcher chains, exact neutral inverse,
  corrupt current/future fields, checksum precedence, byte-unchanged load,
  explicit save rotation/recovery and actual public Apply continuation.
- FrozenEpisodeV25ContractTests.cs retains all166 cases/19methods and original
  attribute lines. Raw current clone/input/Apply comparisons see every26 field.
  Positive fixed25 compatibility uses full current validation and a literal
  neutral exact-inverse projection; negatives keep fixed25 shape and intended
  semantic refusal plus a lifted current26 companion. These projections are NOT
  newly captured historical25 evidence; never rerun the old capture exporter on26.
- PersistenceMigrationTests.cs adds only a test-only StripSchema26 inverse,
  proving complete current shape/core, neutral extensions and fixed25 acceptance
  BEFORE removal. Existing StripSchema25 calls it first. Current read/save/recovery
  assertions advance to26, without changing the former-step contracts.
- PersistenceV3MigrationTests.cs through PersistenceV25MigrationTests.cs retain
  all cases and former Upgrade step witnesses; current/full-chain assertions use26.
  Exact new-root whitelists gain only the empty archive; V13-15 retain24-to25 proof
  before an explicit25-to26 step. V24/V25 neutral-delta helpers project current26
  explicitly before checking the ORIGINAL older step. Future markers use27.
- FrozenEpisodeV23ContractTests.cs validates current26 then removes only neutral
  additions before its existing23 projection; unsupported marker27. Frozen24
  retains its old PrepareV25Payload detached relabel witness and also proves the
  new current dispatch strictly refuses that incomplete25 shape; future marker27.
- Current-header assertions only: BlockSpeechInfluenceTests.cs,
  BlockSpeechPersistenceTests.cs, CommitmentRulesTests.cs,
  EpisodePreparationTests.cs, EpisodeVotingBlocTests.cs,
  HoHPitchPersistenceTests.cs and UnifiedSafetySaveRefusalTests.cs.
- CommitmentRulesSeasonDigests.cs uses JSON-only introspection to strip ONLY a
  literal26 empty archive in legacy digest reporting, remaining compilable against
  retained old assemblies. Existing rule/mirror guards and golden digests unchanged.
- Tools/SimulationTests/SimulationTests.csproj registers only the genuinely pure
  new29 fixture once. Tools/baseline.txt raises Edit5068-to5175 and pure3399-to3428;
  Play999/UMA77 unchanged. Future combined5186/1003 retains integration QA11/4.
- This record plus UNITY_PORT_ROADMAP.md and UNITY_PORT_IMPLEMENTATION.md report
  these material changes and the precise acceptance boundary. Task-local journal
  and new managed probe helpers are outside the Unity project.

No frozen production snapshot, original corpus/index/binding/meta/attribute pins,
historical34 JSON-only fixture, legacy golden, SaveStore, scene, project setting,
live edit, vendor asset, build, save or remote changed.

## Verification state

Independent static production,107 new cases,166 compatibility bridge,34 existing
root-fixture plumbing and focused107 helper reviews CLEAR. This is source review,
not execution. Helpers reject unsupported NUnit lifecycle/data/exclusions and bind
actual source/capture/Tools/package/DLL/response/reference/procedure inputs.

Fresh compiler15 naturally CLOSED0: eight assemblies from all994 current C# once
across8 response files. Independent dependency/reference/source audit CLEAR.
Output455d50a72f1e4088abd0ffab89381a54; log SHA
dcbc4af335413bf02d39ed0c95f13721f748f0f6f0325622f820d8f5f84516e3.
Before15/after15 exactly match1002 source+2Tools, byte SHA
55740482526f269cf68474014c6d7c466b6636a56625650e1d4eb98e4b7c838f,
ordered-content SHA6d457ad38d3e67dbaab39ab55ba60e751a947f7c7391c08f03431b76e2be890a.
These bind4ed8 plus the dirty tested source image, not a retroactively tagged commit.

Pure15 naturally CLOSED0:3428 regular cases passed,0failed;13 preexisting explicit
reports remain unexecuted. Actual TRX3441 unique result IDs,3428Passed/13NotExecuted;
no reports are claimed as run. Independent pure/source audit CLEAR. TRX SHA
d28f534f71cff86ecb1a761bf869fc65f85d7714b4c9242fe077ebb46d9a22da;
log7ca9c62b5e9eb5d1f4c89fdc46572def3f4647a9b6ef0935fb046b94601251b2.
Separate compatibility05 naturally CLOSED0,166/166/0failed/noTimeout33.5484273s;
independent artifact/source audit CLEAR. Binding SHA
7f390df4187030f3564892e19ff9394bb6d23a86efbe275ec2c5169d00c533cb.
New focused107 run01 naturally CLOSED0,29pure+78persistence allpassed,0failed,
noTimeout145.7795646s, exact fixture/global summaries,0inputdrift/HEADunchanged.
Binding729b2fa2255252e082ce791fa912a4ebd462a3c50bd1dab36eeb11bab83fbe03;
log78557784c358396ffd585ca55942f631b56d445bb6bd273518c8542f4906ca52.
Its complete physical artifact audit is independently CLEAR:1002source/2Tools,
1556package,8DLL/8RSP,305external and9procedure records physically match. The107-case focused suite
includes26 actual SaveStore disk cases on test-owned temporary paths, not ordinary
user saves; managed execution cannot establish Unity native lifecycle.

Historical34 replay04 naturally CLOSED0,34/34/0failed/noTimeout81.0705592s;
binding3004a3861d02d5b75b3ba4de35db24eff99b474bc35659c78b3a7d74fabba422,
logc0257a2520319464e9b66646e6230c91ab13044d8e5a773cc8ef70218997631a.
Its physical artifact audit is independently CLEAR with all bound inputs matching.
Formal legacy01 naturally CLOSED1: owned worker19192 hit its180s bound at
180.3125118s/exit-1. Exactly45 completed seasons/4384attempts match BOTH old24
bindings, but the remaining10seasons/1572attempts and final55/5956 summary are
unproven. Bindinga059f60fee243a82733a092656b358f9b2045ba4db7304a777bbd17e9f9e8747,
log98d451148e8fdbc3b6b8e604742c681cfa6af3e58671e894fca01ef45dd7be66;
accepted=false/complete=false,0inputdrift/HEADunchanged. Independent failed-artifact
audit CLEAR as an incomplete timeout, NOT acceptance. Original helper/run remain
unchanged. A NEW fixed10-group runner subsequently closed successfully: same full
projection, commands and assertions,180s per sequential owned worker and600s
whole-controller bound. Sharded01 root26530 naturally CLOSED0: all55 unique seasons/
5956 attempted transitions,10 workers each exit0/noTimeout; maximum75.0867129s,
controller345.2790965s. Bindinge24afbe5f1520f99a1eb99a0f5ec8e1e71f29d43f131eed50ff5d8219844f8b5,
aggregate logfff422c834e819523d656090c8fc40f203c41f9e5ce8e2011dd7cb1f1ccc910a.
Independent physical audit CLEAR: all1002source+2Tools, fixture, original/prior
procedures,8DLL/8RSP,449references,10worker receipts and fixed plan match. Actual
raw tuples equal BOTH retained24 baselines; before15 equals after16,0inputdrift.
No source/golden change or waived check. The original incomplete timeout remains
retained and is not retagged as this separate successful execution.

Formal comparison01 root20978 CLOSED1 before report creation: PowerShell's
case-insensitive loop variable $name collided with the validated $Name parameter.
The original comparator remains unchanged; NEW reviewed comparator-v2 renames only
six loop-variable references (and trims trailing blank lines). AST/static audit
CLEAR. Fresh comparison02 root35392 naturally CLOSED0, status
PassedManagedLegacyReplayOnly,55/5956; JSON SHA
b879d59f303728a8e58c5481fb783bf07b5142baa4cdab0639df0f1a92fc969b.
New comparison helper SHAefe0c1fa27964dd10cd8b3646d72f653417f0c3b1b225b46d293017a95f373d5.
Every original guard and procedure pin remains; failed01 produced no accepted JSON.
Independent final actual comparison02 JSON/pins/rows/limitations audit CLEAR.
An initial root portable04 invocation used an invalid '-probe-' output
name; the scope guard refused before directory creation/compiler/tests. No artifact
was overwritten, no test executed, and no guard was changed; only the argument was
corrected for the genuine fresh04 execution.
All new native full NoUMA/UMA, enabled Safety Play8/save UI, separate desktop build,
GTX1060 actual1920x1080@60FPS, visual/accessibility and human acceptance remain open.

## Next dependency

After reviewed tests and a scoped local checkpoint, capture actual source-backed
VoteTogether/Deal settlement fixtures, then integrate complete canonical Vote
owners/readers/settlements/durable transactions before enabling fresh mode2.
FinalTwo/oaths/calls and other D1 families, all-week NPC strategy, negotiated
alliance meetings, deeper leaks/double-dealing, broader balance, coherent real
house/cast assets and reactions, audio/input polish and three first-time human
playtests remain required. This storage prerequisite does not complete D1 or the port.

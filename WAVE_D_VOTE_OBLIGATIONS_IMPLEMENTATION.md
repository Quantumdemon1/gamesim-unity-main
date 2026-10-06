# Inactive Vote promise and targeted-deal verdict prerequisite

2026-10-06 UTC. This increment adds pure private-evidence predicates, NOT an
installed Vote authority, producer/term validator, settlement/effect writer or
save migration. Existing public modes0/1, schemas, rules, creators, engine,
expiry, ballots, IDs, RNG and knowledge remain unchanged. Future mode2 remains
refused. Actual managed/pure evidence is distinct from native/build/performance/
visual/human acceptance.

## Every material change

| Path | Change and boundary |
| --- | --- |
| Assets/Gamesim/Simulation/UnifiedVoteObligations.cs | NEW135-line pure Promise/targeted Save/Evict leaf and complete-archive overload, with non-Serializable immutable Status/ActorId result; no installation or effects. SHA38f40cf01faba8c4f5437a6232acaf81b42eb5c4f0e073ec4c18ef1512beb7aa. |
| Same source .meta | NEW GUID982ebaf208e448c6bf0732d161747980; SHA482b3bdee50371e67c9a08f283d0ea2f5d832382fa1d68be6e33fa56cf756a6d. |
| Assets/Gamesim/Tests/EditMode/UnifiedVoteObligationsTests.cs | NEW144 regular cases/18 methods,142TestCase+2Test; local controls, actual public mode0/1 predecision producer/reveal witnesses, archive refusal, immutability and structural-fingerprint control. SHA87f71c8c2239e750bc9ff5605b0ce6e3366097ddc66931ad47fd3a54e3cc961d. |
| Same fixture .meta | NEW GUID141e9f8d635c4405987f9093b1cd0a89; SHA6d9978cfd1b5817899dd4890d09b5fca83180e9829f2e639cb49758b942a4572. |
| Tools/SimulationTests/SimulationTests.csproj | Register that fixture exactlyONCE, unchanged defaultCompile=false. SHA6053631dfc5d8164f63240f3421da1e2215ccfef0494cbd1a43f54ad8a84116e. |
| Tools/baseline.txt | Raise Edit5704->5848 and pure3871->4015 by actual144; retain Play999/UMA77. SHAd9338a31b3c18b27669ad065a541f3a80c57db6521da4aaadb37b15defc49846. Future combined5859Edit/1003Play/77UMA/4015pure retains all integration QA11/4. |
| This record, UNITY_PORT_IMPLEMENTATION.md, UNITY_PORT_ROADMAP.md | Record this bounded result and all still-open dependencies, without retagging old evidence or claiming installation. |

All existing Together122/source consumer86, completed-reveal140/archive181,
Frozen25 corpus, goldens and old fixtures retain their bytes. No existing
production/reveal/archive/serialization/asset/scene/Settings source was edited.
Original143-case new fixture was corrected BEFORE execution: dictionary
enumeration yields KeyValuePair structs; generic value-type ToString did not
recursively fingerprint nested object/list values. The final NEW fixture reads
actual Key/Value recursively, scalar-formats only primitive/enum/decimal and
recurses other public state fields. One added control proves mutations, keys
and list order are detected while an unchanged detached clone/source stays equal.
Independent full source and scoped144/plumbing reviews are CLEAR.

## Source behavior and API limits

TryVerdict validates local family/status/row IDs, exact two-name block and the
WHOLE <=16 unique-voter box before any inactive/not-this-block no-decision.
Tokens are bounded160/ordinal/nonblank/no-control. Ballots must target the block
and cannot belong to its nominees. Local shape does not prove a roster, HoH
role, complete reveal, producer origin, term, links or saved settlement fields.

Native EpisodeEngine446-454 settles only active Vote promises from an actual
maker ballot after the whole public reveal. Exact target match fulfills;
mismatch breaks and maker is the action actor. Missing maker is no-decision.
Only literal StoryPromise origin permits a null-target local shape: native
StoryEffects321-342 really creates it, and native null mismatches a real ballot.
Original TS promise-utils15-43,82-83 leaves an unspecified target/preference
pending, so this is a documented pre-existing native difference, NOT original
web parity. No actual Story producer witness is claimed by a local row.

Native DealResolution208-233 C0-enabled targeted policy requires the named
target on the final block. Save keeps by voting the other nominee; Evict keeps
by voting the target. An absent party contributes no vote; the other actual
eligible party may still decide. Proposer then recipient
order, not ballot insertion order, selects first actual keeper or sole breaker.
Two breakers produce Broken/null sole actor; null actor is NOT no-decision.
A fulfilled actor is an action attribution, never a persisted brokenById.
Original web targeted deals do not settle; these positives are native
STRATEGY-LOOP extensions, not original-web targeted settlement parity.

TryFromArchive FIRST validates the whole separate complete archive, even for
inactive rows, then selects the actual regular frame and durable PowerRow block
and resolves parties/named target to the stored cast. No guessed historical
block, pending/final/jury frame, partial history or fabricated HoH ballot.
Historical checks use retained predecision Active projections, not terminal
rows toggled back Active. Actual week turns retain separate evidence while the
real private box/current nominees clear; no authoritative archive is installed.

## Actual19 checks and immutable inputs

Before22/after22 are byte-identical at observed HEAD
a5c46909cade0abd8c403cf3e72330f6957d4055 plus the four new source/meta paths and
two Tools edits. Capture inventories1011source(1003C#+8asmdef)+2Tools:
raw SHAfe2bc569b558a8a14fe3d6a079eeb6e1ebc5b3634e2acb0d44eb2d8e645826a3,
contentSHAb44602e40ee61323470f3402a784e92c82d2933561352fe163af49986253bb97.
These executions are not retagged as a later clean commit or integration run.

Compiler root72538 naturallyCLOSED0, all8 fresh assemblies, output
C:/Users/kelli/AppData/Local/Gamesim/offline-compile/3dd6b638470d4ea2ae3ddadfdf8cc440.
Logwave-d-vote-obligations-offline-19.log at D:/CodexGamesimEvidence/integration-20261004
SHA45c32081c288d84f8873981d8b943d178f4178f0521783a96b9550cd8f055641.
Independent full physical compiler/dependency audit CLEAR: all1003C# compile
exactlyONCE across8fresh RSP/DLL outputs,16fresh internal dependency edges precede
their consumers,445external/449union references exist; no NoUMA/stale routes.

Pure root4508 naturallyCLOSED0:4015actualPassed/0Failed, including all144 new
regular cases;13 unchanged old Explicit reports remain separately NotExecuted.
TRXwave-d-vote-obligations-pure-19/vote-obligations.trx
SHA589eabaf09c76d9810486b5916bc0db6efbc53ee232611666ce48c8995b6aae8.
Actual4028 distinct executionIDs include those13; reused old adapter testIds/
display labels are not confused with executions. Independent full TRX/class
identity audit CLEAR: all144 uniquelyPassed,48 unchanged double-adapter-ID groups
belong only to retained WebVotingBlocParity constant labels; no new collisions.
.NET10 logs a nonblocking SYSLIB0050 warning for the
NEW test's Type.IsSerializable introspection; no formatter is invoked and no
check/warning was suppressed. Do not call this a warning-free run.

Public positives genuinely use fresh source factories, configuration before
constructor, accepted public Apply/receipt/revision guards, legal player
PromiseVote/accepted ProposeDeal and complete current-frame projection. Search
is fixed <=32 seeds x512 accepted commands; missing witness fails, not skips.
Raw predecision IDs/parties/terms precede actual native post-reveal comparisons.
The recorded producer can first succeed week1 or later; no guaranteed unrelated
historic-frame corruption or source removal witness is inferred from that.
Story-null, nominee maker, open/late terms and role projections remain explicitly
detached local/source-shape controls unless an actual producer ran.

## Remaining work, not completion

Producer/origin/term/late-answer/first-eligible admission and cross-family links/
global ID/capacity validation; directed/collective incident effects and dedupe;
reference/readers; atomic reveal/expiry/answer writers; whole-save/durability/
corruption/privacy tests and fresh-only mode2 activation remain OPEN. Genuine
public ordinary-voter removal evidence remains OPEN. No term/effect/expiry
arithmetic, read-side score, relationship, facts or audiences were changed here.

Original combined969 native g26n1 naturally CLOSED Failed, not timed out:
Edit5185Pass/1Fail/0Skip out of5186; Play1003Pass/0Fail/0Skip out of1003. The old
historical-save fixture failure and whole failed acceptance remain retained.
All four held-owned Unity/worker handles have real terminal exit codes; no
cleanup error or unowned descendant. Integration before/after source is equal;
the acceptance copy separately records its permitted dynamic-font-atlas cache
change, not a byte-identical claim. Whole-failed captures are not accepted.
Serializer repair and new increments still need fresh same-pin native
NoUMA+UMA suites. Integration
and live project were not mutated. Real-asset derivative/import/fit/URP checks,
separate clean desktop build, actual GTX1060 1920x1080@60FPS, shipping visuals,
balance and three first-time human E1-E5 playtests remain separate open gates.
Owner can arrange three testers, not evidence they have tested or passed.

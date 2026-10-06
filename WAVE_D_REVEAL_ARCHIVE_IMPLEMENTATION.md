# Wave D1 - complete regular-reveal archive prerequisite

2026-10-06 UTC. This is a reviewed, executed, isolated prerequisite, not an installed
gameplay feature or a completed Unity port. The archive is passed separately from
EpisodeState; public modes0/1 still require their authoritative archive empty and
future mode2 remains refused. No schema, engine, save, migration, knowledge or
live-project authority is changed.

## Every material project change

- NEW Simulation/UnifiedVoteRevealArchive.cs and meta: bounded pure APIs
  TryValidateComplete and TryProjectCurrent. A complete fresh-source history needs
  one frame per completed regular week, including elections without a canonical
  Vote settlement. This replaces the old selective/orphan-frame proposal, not
  legacy history or saved rules. Missing earlier frames/power cannot be backfilled.
- Complete history classifies actual regular, current pending-veto and current
  three-person final-selection power separately. Every prior week needs a regular
  owner; final/jury ballots require no regular frame. Final selection retains the
  two other finalists, empty tally/veto metadata and actual Jury evictee. The last
  predicate was added after independent pre-execution review found a gap; one new
  detached corruption case proves refusal. No old production check was weakened.
- Archive containers are nonnull, bounded to100, strictly ascending and cover the
  exact required weeks. Every frame passes the existing completed-reveal proof.
  Current frame voter/target order must additionally equal the actual private box;
  historical insertion order cannot be reconstructed from a durable tally and is
  not invented. Departures on the reveal week still belong to that pre-reveal cast.
- Projection accepts only an actual completed current regular reveal and complete
  earlier history, optionally an identical already-present current frame. It
  deep-copies only actual voter/target values, verifies the complete candidate and
  returns no partial output on failure. Repeated calls are detached/idempotent;
  they draw no RNG, mint no IDs and cause no events, settlement or disclosures.
- Minimal Simulation/UnifiedVoteCompletedReveal.cs refactor extracts its unchanged
  bounded cast/power/removal/status proof into an internal context. Empty archives
  are therefore checked without a fabricated sentinel frame. Public signature,
  existing leaf predicates and initial refusal order/reasons are retained.
- NEW Tests/EditMode/UnifiedVoteRevealArchiveTests.cs and meta:181 synchronous
  TestCase cases across20 methods. Public source baselines use real modes0/1,
  factory casts3/4/5/8/12, seed1 and the normal enabled recipe, at most512 accepted
  commands. Coverage includes full seasons, ordinary/tied results, post-reveal
  Social, week turns, prior history during six pending stages, final setup/choice/
  speeches/jury/Finished, missing/malformed owners/frames, order, clones, repeated
  projection and complete input immutability. Original140 fixture is byte-identical.
- Player-HoH pending tie and removal date mutations are explicitly detached local
  controls. The actual fixed tie is NPC-HoH auto-decided. None substitutes for the
  still-open genuine public ordinary-voter removal gate; all three failed bounded
  searches remain retained. No100-frame or factory16-person positive is invented.
- Tools/SimulationTests/SimulationTests.csproj registers the new fixture once.
  Tools/baseline.txt raises Edit5523-to5704 and pure3690-to3871. Play999/UMA77 stay
  unchanged. Future combined floors5715Edit/1003Play retain the independent QA11/4.
- This record and the two main progress headers identify the exact evidence and
  remaining work. The earlier completed-reveal record clarifies execution IDs
  versus adapter test IDs; no fixture, case, check or floor is removed.

Source paths above are relative to Assets/Gamesim. New unique metadata GUIDs:
986128d55e544119b0f24d278ff1f5d5 and aa3279de8c724de7be73d7548ce45dda.
Final archive SHA c91d0a93880b7b8c63f80ac2fb77733b0da37b84e18fde93e7f6d7eb629c43fc;
fixture SHA1c152e1a53f38a6e9ed0f7c84bbfcd31f46a03e83a2aea4e465b6da3e0663fe1;
shared leaf SHA3134e2bcde2c405db88c5554b5f24a76c9262ecaeb0697b1a9785d5e4cbb600f.
Unchanged140 fixture SHAeba0ee7a938a56b78160deccd99720a5b3a08a8ccf42f5bcdbe7c4d8167b48aa.

## Actual evidence, not future acceptance

All runs observe HEADd781c3d19261898b89c2b316d43ed275495e3bd1 plus the authored
seven-path source/Tools image, not a later commit. Before20 is a preserved
pre-review capture with no execution; it is not retagged as the repaired image.
Before21/after21 naturally close0 and are byte-identical:1009 source records
(1001C#+8asmdef) plus2Tools, SHA
6332474311e911e744120f759c9b7183c9c15064324c4dbcd5b92e3e318711d4,
content573c81e8b61b4cc53b205a19281b48352b77e230c5837413b76983dd0c815471.

Fresh compiler18/root37493 naturally closes0, all8 assemblies, under
C:/Users/kelli/AppData/Local/Gamesim/offline-compile/858f439c0fc24e57b810daab475b5c31.
All1001C# inputs occur once;16 internal references use fresh dependencies,
445 external references exist,449 union identities and no active NoUMA cache.
LogSHAfca90f9c1a32b03648721abd19e6d8643c5a69c5d1adee408309ae479dc3d671.

Pure18/root95504 naturally closes0:3871 actual Passed/0Failed, including all181
new cases. Thirteen older Explicit reports are separately NotExecuted, not passes.
TRX SHA0d37d1c3d5872db843a1689f9c6d41281f2cdb27bc1dd2963c0ecbaef38e951c.
There are3884 unique execution IDs,3836 adapter test IDs and3833 friendly names.
All48 repeated adapter-ID groups are unchanged old WebVotingBlocParityTests:
two authored methods use the same48 source rows and identical SetName labels.
Both are real executions; the new181 have unique execution/test/name identities.
Old17 likewise had3703 execution IDs but3655 adapter IDs; its earlier shorthand
must not be read as unique test IDs. Independent physical source/RSP/dependency/
TRX/count review is CLEAR; this does not imply native execution or whole-save proof.

Evidence is retained under D:/CodexGamesimEvidence/integration-20261004:
wave-d-save-input-before20.json, before21/after21, wave-d-reveal-archive-offline-18.log,
wave-d-reveal-archive-pure-18/reveal-archive.trx and its separate log.

## Next dependencies and acceptance gates

Safety-only family dispatch; all canonical Vote producers/targets/linked-price
chronology/verdicts/effect attribution/readers; preserved settlement/publication
order; strict full save/load/rollback and native transaction coverage must precede
fresh mode2 activation. FinalTwo/oaths/alliance calls and required D2/D3/D4 remain.
The test-only legal-bloc serializer repair still awaits native re-execution.

Native g26n1 continues on the older integration969 source and retains its genuine
Edit failure; it does not execute this increment. Deliberate preservation-aware
integration, same-pin NoUMA+UMA, separately verified desktop build, actual GTX1060
1920x1080 at60FPS, real-asset URP/visual/accessibility checks and E1-E5 human
acceptance remain separate/open. The owner can arrange three first-time testers;
no human gate is passed until their finished-build results are actually recorded.
Live scenes/settings/recovery/saves/builds and frozen integration remain untouched.

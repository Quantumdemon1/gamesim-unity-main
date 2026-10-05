# Wave C E1: separate implementation and evidence

Status: implementation staged on `codex/wave-c-economy`, based on `a9f11ad0`, at
`D:/GamesimWaveC`. Not merged, promoted, natively accepted or built as a shipping player.
The live project and frozen integration candidate remain separate and unchanged by this work.

## Material changes

- Schema23 persists `economyRulesVersion` and `moveInExtrasSpent`. Existing v1-v22 saves gain
  exactly zero/zero and keep their old rules, even at a pristine opening. Loading remains read-only;
  the normal explicit-save path retains original bytes as a backup. No ordinary user saves changed.
- Only `EpisodeDirector.StartSeason` selects economy1. With window rules active, move-in has two
  base actions at every supported stored cast size3-16; later after-eviction windows have a floor
  of two. Other bases stay2/1/2. No fifth saved window or new day clock is introduced.
- Closing move-in records extras already spent before clearing window counters. Those credits
  cannot be spent twice in week one. The debit clears on actual week advance, not on reload;
  purchases, purchase ceilings, historical rewards and RNG are not rewritten. Positive-credit
  clamping and negative Have-Not penalties remain separate.
- Free-time captions distinguish lost base actions from unspent extras that carry within the
  week. Existing legacy captions and their tests remain intact. New captions explain week-end loss.
- FrozenV22 validates its historical shape, commitment records, vocabulary, bounds and links'
  field types without depending on the growing live DTO. It strips only v22 additions from a clone
  before delegating to FrozenV21; current reference/settlement validation still runs after migration.
- Historical migration tests now expect current schema23 after the full chain; their frozen
  intermediate-version assertions remain. The shared synthetic projection helper strips only the
  two new fields. No historical JSON fixture or legacy season golden is regenerated.
- Added economy, migration, UI-copy and creator/reload coverage, with a retained historical Mono
  schema22 opening envelope. Its origin is a managed Save/TryLoad harness in Unity's Mono, **not**
  an Editor/player capture. The real historical V21 standalone fixture remains untouched.
- Added a paired Game Sense/greedy-social measurement with fresh-season rules identical in both
  arms except E1. The prior default harness remains unchanged. Its explicit report is compiled
  out of Unity, so it does not inflate native-suite acceptance.

E1 is the approved native action-window design in `Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md`,
not literal web parity. The original web pool's ceil(active/2) arithmetic is unchanged. Wave B
commitment/alliance work is already implemented and is not redone here.

## Evidence, kept separate

- Initial pure economy checks:45 passed in `wave-c-pure-01/wave-c-economy-initial.trx`.
- Early broader pure run:1611 passed in `wave-c-pure-02/wave-c-pure-full.trx`; this precedes later
  test/caption additions and is not the final branch-wide result. Explicit reports are not part of
  its default run. Both directories are under `D:/CodexGamesimEvidence/integration-20261004`.
- Initial offline compilation:8/8 assemblies, zero errors, using existing UMA-enabled cached
  references read-only. Output `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/a76c278aa7d1428383d3eea322698f28`.
  This is not Unity test execution, and final-source recompilation remains required.
- `wave-c-measurement-01`: both legacy-golden/commitment walk tests passed. The paired economy
  report failed in its legacy-window arm because its fallback still sent A/B answers to the current
  finale. The failed artifact is retained; incomplete balance numbers are not accepted. The test
  driver now selects an offered finale response. No gameplay assertion or production rule was relaxed.
- Final default pure run: **1,612 passed**, zero failed, at
  `wave-c-pure-03/wave-c-pure-final.trx`. Explicit reports are separately run, not omitted acceptance.
- Corrected `wave-c-measurement-02` passed all three explicit tests: 54 unchanged legacy-golden
  seasons, 54 commitment-enabled walks, and 480 paired economy-policy seasons with zero errors.
  The original golden values were not changed. Game Sense results below are measurements, not
  a claim of human balance acceptance or a proven statistical difference in win rates.
- Final offline compilation: **8/8 assemblies**, zero errors, in `wave-c-offline-final-02.log`,
  output `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/51d5669a24a4449eadd11b6a1cb8ca4f`.
  Native suite floors rise to Edit2604 / Play904 / UMA77; the measured pure floor is1612.
- Managed Mono persistence probe: **28 checks passed**, exit0, `wave-c-managed-04/binding.json`
  and its isolated save-root receipt. Actual production assemblies validate the historical original
  checksum, exact old-field projection, load without writes, exact original-byte backup, reload,
  refused corruption/new-field smuggling, and current-rule opening-debit persistence/replay.
  This is NOT a Unity Editor test or an independently built player. No serializer/API was stubbed.
- Earlier managed-probe attempts are not passes: a PS5 parse error launched nothing; managed01
  stopped at an EFS compiled-binary copy; managed02/03 exposed a read-only archive attribute copied
  onto the isolated save target. Mono02 stderr capture stopped early, and Mono03's wrapper did not
  retain an exit code. The repaired helper reads compiler outputs in place, retains its process
  handle and redirects logs. The repaired test writes identical fixture bytes into a new writable
  slot without changing the archive's attributes. All partial directories/logs remain retained;
  no production persistence guard or file permission was relaxed.
- Native persistence tests, fresh/legacy scene installation, actual UI traversal, both complete native configurations,
  a matching separately verified desktop build, graphical performance and human playtests are pending.
- Independent reviewers were unavailable due their execution quota. This increment has only the
  root's manual review so far; do not describe it as independently reviewed.

| Economy / scripted policy | Mean Game Sense | Wins / 80 | Final two / 80 |
| --- | ---: | ---: | ---: |
| Legacy windows / reader | 71.9 | 11 | 28 |
| Legacy windows / random | 60.7 | 3 | 11 |
| Legacy windows / greedy-social | 66.1 | 6 | 13 |
| E1 / reader | 70.5 | 11 | 19 |
| E1 / random | 60.5 | 9 | 16 |
| E1 / greedy-social | 66.9 | 10 | 21 |

Reader-minus-random score gap: 11.14 before E1, 9.96 with E1. This does not meet the old report's
aspirational 20-point separation; no threshold was lowered. E2-E5 and the required Wave D depth
are still pending, so this is a baseline to revisit, not final balance acceptance.

## Required continuation

Close the frozen schema22 candidate's gates without folding this branch into an active test run.
Then validate this branch's schema23 migration and fresh/legacy flows natively, review it, build
and exercise a matching player, and integrate through the existing preservation gates.

E2-E5 still require differentiated social verbs/replies, target pickers, player-HoH pitch cards and
bounded speech influence. The owner requires all four Wave D additions: unified commitments,
week-long NPC strategy, negotiated alliance plans, deeper secret leaks. Cohesive real-asset
replacement, presentation/accessibility, GTX1060 **1920x1080 at60 FPS**, separate6/12-person workloads
and16-person stress, and three first-time human E1-E5 playtests remain open. Optional cloud/accounts,
Steam/publishing and purchases do not gain authority from these decisions.

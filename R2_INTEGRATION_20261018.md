# Current release-candidate boundary — 18 October 2026

This checkpoint supersedes the work queue in the older October 12–13 ledgers;
their evidence and failures are preserved. It is not release acceptance.

## Integrated

The previously clean `claude/consolidation` worktree was fast-forwarded, without
cherry-picking private baseline commits, from
`2a11020aa361ad77ba7c9b8881c26efc59bc423a` to
`76371982ab0c01de082ed7b79b59f852b44c1346`:

- `3ef0def8`: the standalone season walk respects the existing final-four veto
  lock, while retaining the legal nominee-save/replacement route.
- `76371982`: the director and season verifier find newly committed events by
  pre-command `nextSequence`, not by the old length of the capped 256-entry log.
  Reveals, ceremony cards, sounds and recaps retain their audience checks.
- Three shared PlayMode regressions were added by those commits: two legal
  final-four histories and a legal full-log eviction saved/reloaded into the scene.
- `Tools/baseline.txt` now requires PlayMode 1233 without UMA / 1273 with UMA,
  accounting for those three unconditional tests. All other floors stay unchanged.
  Floors are minimum executed counts, never evidence that the tests passed.

Independent non-author static review found no correctness blocker to the exact
seven-path integration. It identified the missing floor increase and noted that
the new full-log native regression directly covers eviction/recap, not every
other consumer of the repaired event-selection helper.

No save format, rules version, balance coefficient, scene, material, package,
licensed asset or local UMA define was changed by this integration. The live C:
project, old D:/GamesimIntegration20261010, D:/GamesimR2-walk, D:/GamesimRelease
and D:/GamesimPlay were not promoted or overwritten. No remote was changed.

## Evidence already present, not rerun here

- RC2 on `2a11020a`: full Edit 8073/8073, UMA-build Play 1270/1270, UMA 90/90;
  zero failures, skips or inconclusives. Its schema4 summary is
  `D:/GamesimAcceptance/Logs/rc2-summary.json`. Its structured Review13 build
  succeeded with zero errors, 690 warnings. These precede the R2 product changes.
- R2 targeted direct Unity runs: Play 16/16 (`pmuma.xml`) and Edit 83/83
  (`emscan.xml`) in `D:/GamesimAcceptance/Logs`. These are not full suites and
  have no sealed controller summary for a new build gate.
- Four `D:/GamesimVerify/r2walk2-walk-{1,2,3,4}` season subreports passed. Each
  overall verifier failed: batchmode at 1422x693 is not presented 1920x1080.
  The direct R2 build lacks the structured whole-tree evidence required by the
  current build workflow. Keep these results; do not relabel them accepted.

## Current R2 qualification completed

The audited `r2-integrated-20261018` run passed complete native suites:
Edit 8073/8073, UMA-build Play 1273/1273, UMA 90/90. No failed, skipped or
inconclusive cases; all three processes closed naturally, exit 0, not late.
The schema4 summary records fullSuiteAcceptance=true. These are shipping suites;
separate diagnostic probes retain their previously recorded inconclusive results.

The matching audited Windows build completed on 18 October 2026 at 22:18 UTC:
zero errors, 690 warnings, 229 output files / 1,477,215,222 bytes. Source drift
and unexpected build drift were zero. Allowed copy-only changes were the dynamic
Inter-SemiBold atlas, shipping GPU Resident Drawer and configured window/input/
package-analytics settings. The previous build and its manifest were preserved.

Build evidence: `Logs/validation/build-r2-integrated-20261018-20261018T214434755Z-5e5bca7003f54d6b8e0ccf2f11cb9fac/evidence.json`
under the acceptance copy; SHA256
`7d07ad15e3c889073735b43480b09d39dc779f436cb07990004149f0073f609e`.

The independently bounded standalone walk `r2-integrated-20261018-season01`
finished with functionalSeasonPassed=true and walkQualified=true: seed 2949782945,
120 commands, nine save/reload checks, five weekly recaps, zero season errors.
The complete player tree matched the build manifest before and after the walk.
Only its fresh isolated save folder was used; existing saves were not touched.
The player closed naturally, exit 1, not late. Overall/profile status remains
Failed because batchmode cannot prove presentation or requested 1920x1080.
Those two exact profile errors are retained, not waived as performance acceptance.
No unexpected player errors were reported. Player-finalist answers/speech/final
argument lock, a loyalty opportunity and physical input synthesis were not covered
by this legal season. One passing walk is not a claim of every branch or seed.

## Remaining gates, in order

1. Apply and qualify the separately reviewed verifier, creator-scrollbar and
   display-name follow-ups. They are not part of this qualified R2 build.
2. Integrate and prove the frozen historical reader through the real schema28
   save/load paths before adding gameplay. The owner confirmed no other chat
   reserves schema29; this task coordinates it for the actual role/AP contract,
   not an empty schema bump. Then implement role/AP, measure B9 on its new
   opportunity set, finish authored props and combined UI/accessibility checks.
3. Fresh complete native suites and a separately verified Windows build of the
   eventual combined candidate, preserving current R2 evidence and all saves.
4. Real windowed, GPU-presented 1920x1080 verification and clean 6/12/16-person
   profiles on the GTX 1060: p95 <= 16.7 ms and p99 <= 33.3 ms, with runtime/error,
   resolution, duration, build identity and competing-process gates intact.
   UI/accessibility/render evidence remains separate from timing evidence.
5. Three first-time E1–E5 human playtests (currently 0/3), fix/retest blockers,
   then controlled promotion. Do not update a play copy while its Editor is open.

Unity MCP root, active-scene and Console probes timed out. The isolated batch
route was disclosed; no live scene or clean-Console claim is made and no bridge,
package or account was changed. Only the Hub helper was present before validation.

Fresh run labels, process IDs, paths and outcomes are recorded in the task's
`work/gamesim-current-r2-integration-20261018` evidence and the destination's
`Logs/validation` receipts. This document qualifies the R2 baseline only.

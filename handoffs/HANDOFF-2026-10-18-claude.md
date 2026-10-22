# Gamesim Big Brother: handoff, 18 Oct 2026

For the next agent. It picks up the consolidation of all agents' work where Claude stopped, with the remaining work and the context it needs. Claude's weekly usage limit cut the last round off; it resets 21 Oct, 11 pm PT.

## 1. Where things stand, in one paragraph

All work from every agent (Claude lanes, Codex, the lead branch) is consolidated on one branch, **`claude/consolidation`**, worktree **`D:\GamesimConsolidation`**, tip **`2a11020a`**, pushed to GitHub (`Quantumdemon1/gamesim-unity-main`, public repo). On that tip the audited release pipeline (`rc2`) passed every suite on the UMA copy and built a Windows player. Its standalone season walk then failed. The cause is found and fixed on branch **`claude/r2-walk`** (worktree `D:\GamesimR2-walk`, 2 commits on 2a11020a), and a player built from it passed 4/4 walks. That fix is reviewed (no BLOCKER), but it is **not yet landed**: it still needs the full suites and an audited pipeline run. After that come the owner's play copy, the PR to `main`, and the backlog.

## 2. Next steps, in order

1. **Finish and land `claude/r2-walk`** (section 4).
   - Fix floors: the branch adds 3 PlayMode tests but `Tools/baseline.txt` was not raised. Measure, then set `Gamesim.PlayModeTests` (NoUMA, 1230 now) and `Gamesim.PlayModeTests.UmaBuild` (1270 now) to the counts that ran.
   - Optionally take review MINORs R2W-2 (walk counts evictions committed into a full log), R2W-3 (test oracles still diff by count) and R2W-4 (guard before the competition result is dismissed).
   - Merge into `claude/consolidation` with `--no-ff` and a message file.
2. **Run the audited release pipeline on the new tip, alone on D:** (section 6): verify, build, then walk. The walk's `season-verification.json` must say `status: Passed`. `verification.json` always fails its graphics-performance half in batchmode, which is expected and not a blocker. If the walk is flaky, run it 3 or more times on the built player (each walk uses a random seed).
3. **Ask the owner, then update the play copy** `D:\GamesimPlay` (section 7). Only with Unity closed there.
4. **Open one PR `claude/consolidation` → `main`.** Draft body: `scratchpad\consolidate\pr-consolidation-body.md` (section 9). Replace its `BUILD_AND_WALK` line with the real results, and add the walk fixes and the 4 GiB tooling bound.
   - Then close PR #25 as included. The owner approved this.
   - **Never run `gh pr merge`.** The classifier denies it; the owner merges on the web (PR page → Merge pull request → Create a merge commit → Confirm).
   - The desktop app's Autofix is on for PR #25 (`claude/lead-integration`); its last review thread was already fixed and resolved.
5. **After the owner merges:**
   - Switch the C: checkout (`C:\Users\kelli\Gamesim Big Brother`, now on `port/game-flow-v2-pass` b25edcb4 with 14 dirty files) to `main`, per the ledger's instructions (section 8).
   - Then archive and retire old branches and worktrees per the ledger.
6. **The backlog** (section 10).

## 3. The owner's decisions (binding)

From the handoff Codex left and Claude's sessions of 15–17 Oct:

**Method**
- Consolidate on one landing branch; keep the full test gates, old-save acceptance and independent review.
- Drop Codex's paperwork: sealed programmes, index-hash pinning, permission documents.

**Standing rules**
- UMA bodies only for the cast. Never a non-UMA body as fallback, stand-in or test cast.
- The farewell is a noncontact beat; the original 3.8 s Embrace stays tested.
- Desktop target: GTX 1060 at 1920×1080, 60 FPS.
- Three first-time human testers (0 of 3 done).
- Asset Store and purchased packs are reference only, never committed (the repo is public). Furniture and props are authored in Blender.

**Text, captions and saves**
- At the Largest text size (1.4), a screen that cannot fit may page or scroll, with a visible indicator. Standard and Larger keep one page, and tests assert that literally.
- The radial's "More ways to talk" caption removal was the owner's request.
- Old-save acceptance means committed fixture saves per save version and rules combination, not Codex's 16 lifecycle cases. Done: lane `saves`.
- Story-arcs' own "schema 14" save files stay refused.

**The house**
- The Sept 23 room layout is re-created in the placement code. Done.
- The double beds stay at 1.20 × 1.50 m. Full size would block the walk to the lower bunk.
- The HoH sofa is squared to −90°. Done.
- The flight-case prop is in, because its textures were proven in-house. Done.

**Later work**
- The All-Star "player histories" will be rebuilt properly after the old-save work, with their own schema. The real-person research stays out of the public repo unless the owner says otherwise.

**GitHub**
- PR #3 and PR #4 were already closed.
- Close PR #25 as included when the consolidation PR opens.

**The lead's own calls, recorded**
- Replay windows F2 (28 Sep) and F3 (9–10 Oct) are accepted: saves made then continue slightly differently, and no gate is possible after the fact.
- V7-5: keep counting Safety per incident.
- ARCS2-2: the veto ledger types keep fading.
- Flight cases follow the no-shadow dressing policy.
- The unused hazard-stripe material stays (`git rm` is classifier-denied).
- s3: diagnostic probes live in their own floored assemblies.
- D1(b): the refuted probes assert their measured outcome.
- s2b: no house action may be rebound onto a UI Navigate, Submit or Cancel path. Consequence: on the pad, only the board's grip and take-back can be rebound.

## 4. The open fix: `claude/r2-walk`

Commits on 2a11020a:
- `3ef0def8`: the season walk keeps the block when the veto is locked at the final four.
- `76371982`: a commit into a full season log still gets its reveal, its card and its recap.

**Cause 1: the walk script was wrong.**
- When the player is both HoH and veto holder at the final four, the veto is locked (`EpisodeEngine.VetoIsLockedAtFinalFour`, EpisodeEngine.cs:811), and the screen correctly offers only "Do not use the veto".
- `PortVerification.Season.cs` picked a replacement from `ReplacementCandidates`, which ignores the lock, and so pressed a name that wasn't there.
- Fix: press what the screen offers, and check that no veto use is offered.

**Cause 2: a real product bug.**
- `EpisodeEngine.Log` keeps 256 lines.
- `EpisodeDirector.Submit` found a command's new lines by count (`events.Skip(knownEvents)`). Once the log is full, that finds nothing, so there is no vote reveal, eviction card, weekly recap, competition result or fallout. The walk failed with "week N recap never opened".
- Fix: the new `EpisodeDirector.CommittedEvents(after, fromSequence)` finds lines by sequence number, as the conversation code already does. It is used in all five places in `Submit`.
- This was also behind the old Oct 10 player's flaky walk.

**Tests**
- New PlayMode tests:
  - `PortSeasonWalk_KeepsTheBlockWhenTheVetoIsLockedAtFour` (red before the fix)
  - `PortSeasonWalk_NamesTheOnlyReplacementAfterANomineesSaveAtFour` (coverage)
  - `WeeklyRecap_OpensAfterAnEvictionThatCommitsIntoAFullLog` (red before the fix)
- Focused runs:
  - NoUMA subset 148/149, then 5/5 after correcting the new test
  - UMA focused 16/16
  - EditMode scans 83/83

**Not yet done**
- The full suites and an audited walk (review finding R2W-1, MAJOR).
- The floor raise.
- Records: the debug report and review are in the workflow output at `...\tasks\wct0bna05.output`. The local builds are in `D:\GamesimVerify\r2walk-player`, `r2walk2-player` and `r2walk*-walk-*`. The build script is `scratchpad\r2walk-build.sh`.
- `D:\GamesimAcceptance` currently holds 76371982's sources. The pipeline re-syncs it.

## 5. What is on `claude/consolidation` (all reviewed, verified, landed)

**Foundation**
- GitHub `main` 3f23e76b.
- PR #25's branch `claude/lead-integration` (fc763582): Wave D complete, vote V5 and V6 (mode 2 in fresh seasons), the input slices A0–A13, balance B5–B7, schema 28.
- The four 10 Oct lanes: V7, input-later, arcs2 and art-reactions.
- Codex's `codex/port-completion-20261010` (515246bf).
- Codex's 221 uncommitted entries, ported byte for byte in `4cef3e0b`. This kept 515246bf's bytes for 8 assets an editor without UMA had damaged: the 6 HouseGarments slot and overlay assets, `AssetIndexerProject.asset` and the URP global settings.

**This round's lanes** (merge commits on the consolidation line):
- **s2c** (`c176b4ea`): the readers never reveal a ballot the player could not know.
- **s4a** (`d53d587d`): the shared Largest pager, and the Free Time board pages.
- **room** (`7814d15b`): the owner's Sept 23 layout.
- **flight** (`5df6a17a`): the flight case. The scene was landed by rebuilding it.
- **s2a** (`b0db2186`): HUD status, D9/D10/D18, the hot-tub line, and the save chip.
- **s2b** (`a6c40fa2`): the witnessed line, and the rebinding rule.
- **saves** (`b9ec52eb`): old-save fixtures plus `Assets/Plans/OLD-SAVE-ACCEPTANCE.md`.
- **s3** (`9f935790`): diagnostics assemblies, deadlines, and the readiness tests judged on UMA.
- **s5** (`bb4a636a`): the farewell foot stance, IKPass 0, and the port patch applied.
- **s2d** (`b21fb6ff`): noncontact goodbye, storm-offs clear of furniture, the peace embrace gather.
- **s4b** (`89f147ad`), **s4c** (`fc75a1f4`) and **s4d** (`236bf260`): Largest text.
- **beds** (`5d824c73`).
- **validation fixes** (`751b5e03`): the Tour test, the UMA-build floor, honest diagnostics.
- **tooling** (`2a11020a`): single-file bound raised from 512 MiB to 4 GiB.
- **docs** (`ae294b35`): Codex's `REMAINING_WORK_20261012.md` and `ROLE_DIALOGUE_ECONOMY_20261012.md` (the owner's dialogue-economy contract for the role-aware dialogue work).

**Floors** (`Tools/baseline.txt`)

| Suite | Floor |
|---|---|
| EditMode | 8073 |
| PlayMode (NoUMA) | 1230 |
| PlayMode, UMA build | 1270 |
| UMA | 90 |
| Diagnostics | 7 |
| UMA Diagnostics | 10 |
| Unity-free subset | 5358 |

**Last measured, all on 2a11020a or 751b5e03**
- Subset: 5358/5358.
- Season digests: 12/12.
- NoUMA: EditMode 8073/8073, PlayMode 1230/1230.
- rc2 pipeline: EditMode 8073/8073, PlayMode UMA build 1270/1270, UMA 90/90. Status Passed; build OK.
- Diagnostics: 7/7, and 10 with 8 passed and 2 Inconclusive by design.

## 6. How to run things (hard-won rules)

**Git and commits**
- Git is `"/c/Program Files/Git/cmd/git.exe"` (not on PATH).
- Stage explicit paths. Commit with `git commit -F <message file>`, written with the Write tool. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Never amend, rebase or force-push. Never `git stash`.
- **Never commit** `ProjectSettings/ProjectSettings.asset` (it holds the local GAMESIM_UMA define), the Inter-* SDF fonts, `Assets/_Recovery`, `Assets/UMA` or `Assets/UMA.meta`, the HouseGarments assets, `Assets/UMAProjectData`, or the URP global settings.
- Read other tools' worktrees with `GIT_OPTIONAL_LOCKS=0`.

**Writing files**
- Write files with the Write or Edit tools. Bash heredocs mangle backslashes, and Python `-I` ignores `PYTHONIOENCODING`, so use `sys.stdout.reconfigure(encoding='utf-8')` instead.

**Unity test runs**
- Run one Unity batch job at a time, machine-wide.
- Lanes run only through `scratchpad\runs-locked.sh` (lock fixed on 16 Oct; harness `lanes\s2d-load\lock-harness.sh` passes 7/7):
  ```
  WT="<worktree>" [MODE=uma] [GAMESIM_DIAGNOSTICS=1] sh runs-locked.sh <tag> "<name>|<EditMode|PlayMode>|<assembly>|<filter>"
  ```
  - `MODE=nouma` mirrors into `D:\GamesimNoUma`; `MODE=uma` into `D:\GamesimAcceptance`.
  - Results land in `scratchpad\results\<tag>-<name>.xml/.log`.
  - Always start it with `run_in_background`.
  - Never kill Unity or a runner.

**The audited release pipeline**
- Script: `scratchpad\release-pipeline.sh <fresh-name>`. It runs from the release worktree `D:\GamesimRelease` (detached; it has `Assets/UMA` copied in and the define set locally).
- First move it to the commit under test: `git -C D:/GamesimRelease checkout --detach <sha>`.
- Its stages: `Tools/verify-review-candidate.ps1` (EditMode, PlayMode and UMA on the UMA copy), then `build-review-candidate.ps1`, then the standalone walk.
- The build is bound to the verify run's exact source, so any commit in between means rerunning the whole pipeline.
- The script now reads the verify verdict from `summary.json` and refuses to walk a player older than its build.
- `release-build-walk.sh` reruns only the build and walk, and only for an unchanged source.
- Nothing else may run Unity while it runs, or the save-and-reload fixtures time out.
- It takes about 4 hours: verify about 2.8 h, build about 20 min, walk about 3 min.

**The walk by hand**
```
D:\GamesimAcceptance\Builds\Port-Windows-Review13\Gamesim.exe -batchmode --gamesim-verify --gamesim-verify-season --gamesim-save-root <empty abs dir> -logFile <dir>\player.log
```
- Never `-nographics`.
- Judge it by `<dir>\season-verification.json`.

**Other test runs**
- Unity-free subset: `dotnet test Tools/SimulationTests` (about 5 to 8 min).
- Digests (each by full name): `CommitmentRulesSeasonDigests`, `AllianceLeakSeasonDigests`, `PactPlanSeasonDigests`, `ModeOneSeasonDigests`, `AllWeekSeasonDigests`.

**Generated house scene**
- `EpisodeHouse.unity` is generated. Change it only through placement code plus a rebuild (`HouseLivingGallery.BuildFromCommandLine`), never a text merge. The procedure is in `scratchpad\lanes\w2-flight-land\`.
- Leave out the rebuild noise: room-finish `_EMISSION` drift, the URP asset's GPU Resident Drawer switch, and `.lighting` line endings.
- Never open a copy without `Assets/UMA` in the Unity editor. It rewrites the HouseGarments assets into broken YAML.

**Workflows and permissions**
- Resuming a workflow (`resumeFromRunId`) caches results only in the original call order. Parallel workflows re-run reviews and fixes; never edit prompts to "help" a resume.
- The auto-mode classifier denies `gh pr merge`, `git rm`, and some agent merges and restores. Never work around a refusal; surface it to the owner.

## 7. Working copies

| Path | What it is | Rule |
|---|---|---|
| `D:\GamesimConsolidation` | `claude/consolidation`, where everything lands | Lead lands here |
| `D:\GamesimR2-walk` | `claude/r2-walk`, the open walk fix | Land after verification |
| `D:\GamesimRelease` | detached at 2a11020a, with UMA and the local define; the pipeline's source | Never commit from it |
| `D:\GamesimPlay` | **The owner's play copy.** Branch `play/2026-10-15` at ae294b35; fallback `play/2026-10-09` | The owner has had Unity open there since 15 Oct, 21:54. **Never touch while open; ask before updating.** Keep its 6 local editor files: backup under the archive's `play-copy-before-update` |
| `D:\GamesimAcceptance` | the UMA test mirror (runner, pipeline, walks) | |
| `D:\GamesimNoUma` | the NoUMA test mirror | |
| `D:\GamesimW1-*`, `D:\GamesimW2-*`, `D:\GamesimR1-fixes`, `D:\GamesimLane*`, `D:\GamesimLead` | finished lane worktrees, all landed | Remove after the PR merges; see ledger section 4 |
| `D:\GamesimIntegration20261010` | Codex's candidate, fully carried over; holds the 8 damaged assets | Read only; never copy assets from it |
| `D:\ClaudeScratchArchive\2026-10-15-consolidation` | everything that existed only outside git | Keep |
| `D:\GamesimVerify` | the only real save files, schemas 16–28 | Keep |
| `C:\Users\kelli\Gamesim Big Brother` | the C: checkout, old branch, 14 dirty files | Switch to `main` after the merge, per the ledger |

The archive holds Codex's Frozen28 reader, the B9 diagnostics, Source24, the TV-break slice, the web game copy with the alliance fix, the flight case source, the real saves, the Sept 23 scene, the play-copy backup and the s5 port patch.

## 8. Key documents

All paths are under the scratchpad (`C:\Users\kelli\AppData\Local\Temp\claude\C--Users-kelli-Gamesim-Big-Brother--claude-worktrees-pensive-einstein-1490d6\4edb7311-fbf9-4eba-91d9-8b48e06b022b\scratchpad\`) unless noted.

- `consolidate\LEDGER.md`: the inventory of all 190 refs, 31 worktrees and the copies, the retire list, and the C: checkout switch steps.
- `check\CHECK_REPORT.md`: the original check of Codex's work: defects D1–D20 and the waves.
- `consolidate\r1\TRIAGE.md`: the validation r1 triage.
- `consolidate\round3\*.md` and `round4\*.md`: each lane's builder, review and fixer records.
- `consolidate\pr-consolidation-body.md`: the PR draft.
- `status\gamesim-status.html`: the owner's status board, published at https://claude.ai/artifact/NDwSAsd7ooWruBeNFE8MxG. Republish the same file to keep the URL.
- In the repo: `Assets/Plans/OLD-SAVE-ACCEPTANCE.md`, `UNITY_PORT_IMPLEMENTATION.md`, `UNITY_PORT_ROADMAP.md`, `ACCEPTANCE_MATRIX.md`, `PLAYTEST_PROTOCOL.md`, `BALANCE_BASELINE.md`, `ROLE_DIALOGUE_ECONOMY_20261012.md`, `REMAINING_WORK_20261012.md`, `Tools/README.md`.
- Memory: `C:\Users\kelli\.claude\projects\C--Users-kelli-Gamesim-Big-Brother\memory\`. In particular `consolidation-line-is-the-one-project.md`, `a-uma-less-editor-damages-garments.md` and `workflow-resume-caches-only-the-same-call-order.md`.

## 9. Small known leftovers (not blockers)

- **The given-name fix** (ledger PB-2: "Dr. Will Kirby" shows as "Will"). Not landed yet.
  - Recipe: ledger section 2.
  - Patches: `consolidate\ledger-namefix-display.patch` and `ledger-namefix-tests.patch`, from `codex/allstar-characterization-20261012` (3a00079e, 24baba83).
  - Take only the display parts. Never take any Alumni or GAMESIM_ALLSTAR part.
- **The creator's Personality page** scrolls with no indicator; not measured at Largest (s4d).
- **ART-5:** the embrace's hands float past the partner's back on UMA. Measured; the take still needs retuning.
- **R2W-5:** the locked final-four veto meeting's title still says "you name the replacement too". Cosmetic; give the locked state its own title line.
- **Diagnostics:** G2/G3 probes are pinned to their measured outcome; G4 readiness probes report Inconclusive by design.
- **The web game alliance-ownership fix:** exists only in the archive's `codex-work\gamesim`. The owner hasn't decided whether to offer it to the web repo as a PR.

## 10. Remaining product work, after the PR

1. **Role-aware consequential dialogue.**
   - Spec: `ROLE_DIALOGUE_ECONOMY_20261012.md`.
   - One HoH Consultation, one Veto Consultation and one Nominee Appeal per week.
   - Starting proposals: relationship +3/0/−3, time +1/0/−1.
   - Behind a new rules version set in `ShippedRules.ApplyFresh`.
   - Needs schema 29, claimed jointly. Codex's Frozen28 reader (archived; `FrozenEpisodeV28` must freeze the mode-2 core) is the prerequisite. A tripwire test pins schema 28.
   - The owner approves a design note first.
2. **B9 balance tuning** to the agreed bands. Weekly wins now about 95% against a 60–70% target.
   - Behind a fresh rules version; paired 100-seed lab runs; re-record the goldens with stated reasons.
   - The owner still has to pick populations and bands (PORT_COMPLETION_20261010.md:66).
3. **The All-Star player histories, rebuilt properly.**
   - Own schema; NPC-to-NPC changes only through RelationshipLedger.
   - The research stays out of the public repo.
4. **Art.** 27 room placements and 5 apparatus families (Signals, PairConsole, GripRig, DiceTray, WordConsole). All authored in Blender, each verified by render.
5. **Desktop 1080p60 profile on the GTX 1060.** Windowed, not batchmode: `Tools/profile-evidence.ps1` after a build. Also fix PR-12 (the look sheet's viewport sizes).
6. **Three first-time human playtests** per `PLAYTEST_PROTOCOL.md`; fix and retest; then promote to `D:\GamesimPlay` with the owner's OK.
7. **Story session:** schema numbers are claimed jointly with it. `port/story-arcs` is contained in the consolidation line; don't delete its worktree `D:\GamesimStory` until it confirms it is done.

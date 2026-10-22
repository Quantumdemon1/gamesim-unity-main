# Brief for the agent on the owner's PC: push the local work, then qualify the 96 cases

Read `OWNER-DECISIONS-2026-10-22.md` (next to this file) first; it is binding. This brief has two
parts. Part A puts everything that exists only on the PC onto GitHub. Part B is the next
engineering step. Do Part A completely before Part B.

Ground rules (from the owner's decisions):
- No hash pinning, sealed programmes, custody receipts or permission documents.
- One Unity batch job at a time, machine-wide. Never kill Unity or a runner you did not start.
- Never `git stash`, amend, rebase or force-push. Stage explicit paths. Commit with
  `git commit -F <message file>`.
- Do not touch `D:/GamesimPlay`. The owner promotes it separately, after acceptance.
- Git may not be on PATH; it is `"/c/Program Files/Git/cmd/git.exe"`.

## Part A: get everything onto GitHub

### A1. Check the starting point

In `D:/GamesimConsolidation`, with no Unity Editor open on it:

```
git fetch origin
git rev-parse HEAD                                  # expect f1b93b1c...
git merge-base --is-ancestor 72a22fff HEAD && echo "descends from the merged PR #26"
git status --porcelain=v1 -uall > %TEMP%\pc-status.txt
```

Report the HEAD, whether it descends from `72a22fff`, and the number of status rows (the 21 Oct
handoff says 156). If HEAD does not descend from `72a22fff`, still continue: the PR in A5 will
merge, never rebase.

### A2. Commit the source on a new branch

```
git switch -c pc/v2-lifecycle-20261022
```

Stage every changed and new path from `pc-status.txt` by name, except these, which are never
committed:
- `ProjectSettings/ProjectSettings.asset` (the local GAMESIM_UMA define);
- the Inter-* SDF font assets;
- `Assets/_Recovery`, `Assets/UMA`, `Assets/UMA.meta`, `Assets/UMAProjectData`;
- the HouseGarments slot and overlay assets;
- the URP global settings asset;
- anything from the Asset Store or a purchased pack;
- `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/` and test evidence (XML, logs, PNG captures).

Every new file under `Assets/` must have its `.meta` (CI checks this). Commit with a message
that lists what the work is: dialogue V2 (public veto-rescue validation, cited-record dialogue,
V1/V2 story recognition, campaign content, UI projection, fixtures), the house-context
lifecycle fix and its tests, and the two `ObjectCreationHandling.Replace` fixes. State that the
96 V2 cases are not yet qualified.

### A3. Add the props and the research (separate commits on the same branch)

1. **Props batch.** Copy
   `C:/Users/kelli/Documents/Codex/2026-09-20/re/outputs/gamesim-props-20261020` to
   `ArtSource/props/gamesim-props-20261020/`. It sits outside `Assets/`, so Unity imports
   nothing; admission comes later.
   - Before staging, list any file of 95 MB or more. GitHub refuses files over 100 MB and
     this repo has no Git LFS. If there are any, leave those files out, say which, and go on.
   - Leave out anything that is Asset Store or purchased content, if the batch contains any.
2. **All-Star research.** Copy `ALL_STAR_STRATEGIC_CAMPAIGNS_CONTENT_BRIEF_20261021.md`,
   `ALL_STAR_STRATEGIC_CAMPAIGNS_DESIGN_REVIEW01_20261021.md` and every other real-person
   research note to `Research/AllStars/`. If the local branch `codex/allstar-characterization-20261012`
   holds research, push that branch as it is (A4) rather than copying from it.
3. **Frozen28 reader archive.** Copy its 18 source and 10 test files from
   `D:\ClaudeScratchArchive\2026-10-15-consolidation` to `Research/Frozen28/`. They go
   outside `Assets/` so they are not compiled; they are landed properly in step 2 of the plan.
4. **Evidence summaries.** Copy the `.md` documents the 21 Oct handoff names as anchors to
   `handoffs/evidence-20261021/`. Copy only the markdown files, not the logs, XML, captures or
   snapshot folders. The anchors are `R2_PORTRAIT_FULL52_ROOT_CLOSED_BOUNDED_ACCEPTANCE_20261021.md`,
   `R2_V2_SOURCE_ADMISSION_ACTUAL_20261021.md`, `R2_V2_OBSERVER_WITHDRAWAL_ACTUAL_20261021.md`,
   `R2_V2_MATERIALIZATION_DIAGNOSTIC_CLOSED_ROOT_FAILURE_20261021.md`,
   `R2_V2_FUNDED_F_SAVE_AND_HISTORICAL_PARITY_DEPENDENCY_CHECKLIST01.md`,
   `R2_CURRENT_NATIVE_COVERAGE_DATA_ONLY_BRIEF.md`, `PROPS_EQUIPMENT_HANDOFF02_READINESS.md`
   and the proposed patch `PROPOSED.apply_patch.txt`.

### A4. Push

```
git push -u origin pc/v2-lifecycle-20261022
git push origin codex/allstar-characterization-20261012     # if it exists locally
git push origin codex/port-completion-20261010              # if it exists locally and is not on GitHub
```

Never `--force`. If a push is refused, stop and report the message.

### A5. Open a draft PR

Open a draft PR from `pc/v2-lifecycle-20261022` into `main` titled "PC work 18-21 Oct: dialogue V2,
lifecycle fix, props and All-Star research (V2 not yet qualified)". Then report the branch, the
commits and the PR link to the owner.

## Part B: fix and qualify the 96 dialogue cases

1. **Apply the signed-zero fix.** In both Floating factories, change `new JValue(value)` to
   `new JValue((object)value)`. A cloud session reproduced the cause with Json.NET 13.0.2, the
   version Unity's package ships. `new JValue(-0.0)` stores +0.0, and the boxed form keeps the
   sign.
   - Under 13.0.2, `JToken.Parse("-0.0")`, `JToken.FromObject(-0.0)` and deserializing `-0.0`
     into a `double` lose the sign as well. Check the eight V2 fixture JSON files for `-0`
     literals. Where the sign matters, use explicit negative-zero flags, as
     `WebJurySentimentParityTests` does (see its line 94).
   - Do not weaken the comparator or remove the signed-zero assertion.
2. **Run the 96 V2 cases natively** through the locked runner, in the existing method-atomic
   groups. Accept only 96 of 96 executed and passed: none skipped, missing or blocked at Parent.
3. **Run full EditMode on the same source.** It must include `OldSaveAcceptanceTests` at 172 of
   172. V2 changed story content, which can turn the schema 2-15 fixtures red.
4. **Get one independent review** of the V2 diff and the fixes.
5. **Commit and push.** Raise `Tools/baseline.txt` in the same commit, to the counts that
   actually ran.

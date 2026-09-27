# Tools

Verification without the interactive editor, plus the Blender bridge. None of this is imported by
Unity (it sits outside `Assets/`). Machine-specific paths are derived or overridable:
`GAMESIM_ACCEPTANCE` (the batchmode copy, default `D:\GamesimAcceptance`), `GAMESIM_UNITY_EDITOR` /
`GAMESIM_UNITY` (the editor, default derived from `ProjectSettings/ProjectVersion.txt`).

| Script | What it does | Time |
| --- | --- | --- |
| `offline-compile.ps1` | Smoke-compiles all eight owned assemblies from current source lists using cached editor references. Missing assemblies/configuration mismatches fail; outputs and dependencies are unique to this invocation. `-WithoutUma` requires matching UMA-free cached responses. | seconds |
| `player-compile.ps1` | Same for all three player assemblies (two with `-WithoutUma`); validates player/UMA defines and refuses stale dependency outputs. Cached compiler checks do not replace Unity tests/builds. | seconds |
| `verify-review-candidate.ps1 -Name <fresh-name>` | Runs every owned suite, retains before/after complete input manifests, exact `.meta` archives, XML/log hashes and individual test names. Unexpected product-input drift fails. `-WithoutUma` requires the existing separate UMA-free copy. | depends on suites and input hashing |
| `build-review-candidate.ps1 -TestName <passing-name>` | Requires a passing complete same-configuration test summary. Checks live and synced inputs, builds Review13, checks final inputs, retains a timestamped report and hashes the entire build tree. Supports `-WithoutUma`. | depends on changed assets |
| `test-review-evidence.ps1` | Exercises evidence comparisons against temporary synthetic fixtures. No Unity, mirroring or acceptance-copy access. | seconds |
| `test-acceptance-mirror.ps1` | Exercises the real mirror runner against temporary fixture projects, including retained subtrees, folder GUIDs, ordinary deletion and manifest prediction. No Unity or real acceptance-copy writes. | seconds |
| `sync-acceptance.ps1` | Checks source/destination boundaries and that the acceptance editor is idle, then checks every folder mirror. GPU Resident Drawer is disabled only when explicitly requested for tests. | depends on changes |
| `sync-and-run.sh` | Waits for the acceptance copy to be free, mirrors `Assets/` and `ProjectSettings/` over it, then runs each `name:Platform:Assembly` suite headless and prints the totals and any failures. | ~1 min EditMode, 10–15 min PlayMode |
| `build-and-verify.sh` | Retired and fail-closed. It never builds, launches an old executable, or recursively removes a named output folder. Use the current PowerShell runners. | immediate |

```bash
powershell -NoProfile -File Tools/offline-compile.ps1
```

```bash
Tools/sync-and-run.sh edit:EditMode:Gamesim.EditModeTests play:PlayMode:Gamesim.PlayModeTests
```

Run EditMode, PlayMode and UMA PlayMode separately, preserving each XML and its candidate manifest.
The release requirement is zero failures and skipped tests; a historical test count is not evidence
that the current source passes. Record added/removed tests explicitly rather than lowering a count
to accommodate a regression.

Use the review-candidate scripts for acceptance. A test snapshot disables GPU Resident Drawer to avoid
the editor harness crash. The build explicitly permits only its 0-to-1 shipping difference when matching
the tested BEFORE snapshot, and compares post-build inputs with the tested AFTER snapshot. The build's
window settings and known input-action preload are the only other permitted build changes. All other
product differences fail. Workflow changes under `Tools` are retained separately and never described
as tested product changes. Keep feature/source edits frozen through the final tests and build.

Before hashing, both test and build copies remove only the `SENTIS_ANALYTICS_ENABLED` Standalone
symbol that the installed inference package removes on batch startup. The live editor's analytics
preference is untouched. `-WithoutUma` also removes the exact Standalone `GAMESIM_UMA` token.
The source preview applies those same declared copy settings. Explicitly retained `Assets/Resources`
and `Assets/UMAProjectData` directories retain their `.meta` companions as well as their contents.
Dynamic font assets start with cleared generated caches, matching TextMesh Pro's documented-in-source
editor-exit behavior; font files are still fully hashed and any further byte changes fail the audit.

Both runners hash every file under `Assets` (including local UMA and retained resources), `ProjectSettings`,
`Packages`, `ArtSource`, plus the live workflow `Tools` directory. Full `.meta` bytes are archived before
and after import so a drift report can be reviewed. The only built-in import allowance is an authored FBX
changing solely `materialLocation: 0` to `1`. Other import changes fail unless a reviewed JSON array is
supplied to `-ApprovedMetaDrift`, with exact `path`, `beforeSha256`, `afterSha256`, and a nonempty `reason`.
Only `Assets/*.meta` entries are accepted; a null before hash is allowed for a newly generated `.meta`.
Approvals are retained inside the drift report. Do not approve unexplained GUID/material/rig changes.

The named summary binds XML/log hashes, before/after manifests, the drift report and individual test names.
The build retains a timestamped copy of the fresh Unity report, pre/post-build input manifests and a
SHA256 manifest of **every** player file, including managed DLLs and data. The launcher hash alone is not
the player identity. The fixed Review13 output may be rebuilt later; verify its retained tree manifest
before treating an older graphical report as evidence for the files currently in that directory.

```powershell
powershell -NoProfile -File Tools/verify-review-candidate.ps1 -Name review13-f
powershell -NoProfile -File Tools/build-review-candidate.ps1 -TestName review13-f
```

For UMA-free acceptance, set `GAMESIM_ACCEPTANCE` to the existing UMA-free project and pass `-WithoutUma`
to both commands. This configuration never removes a locally installed UMA package. The test suites
exercise the editor configuration; run the reported player explicitly for shipping-render visual and
performance checks, always providing a fresh isolated `--gamesim-save-root`. Neither a successful build
nor passing automated tests establishes visual quality or a frame-time improvement.

## The acceptance copy

The interactive editor holds the live project, so the suites run against a mirror:
`robocopy` `Assets`, `Packages`, `ProjectSettings`, `UserSettings` **and `Library`** (copying
`Library` avoids a reimport of thousands of files) to `D:\GamesimAcceptance` once, then
`sync-and-run.sh` keeps it current. It shares nothing with the live project, so the open editor and
its save slots are untouched.

## Before a commit

`Tools/check-asset-metadata.sh` asserts the three metadata invariants: every tracked asset under
`Assets/` has a tracked `.meta`, no `.meta` is an orphan, and no GUID is used twice. A `.cs` written
from the CLI has no `.meta` until the editor imports it, and Unity writes one on the acceptance copy
during the sync - so a file committed without its `.meta` passes every suite here and breaks only in
a fresh clone, where Unity invents a new GUID and every reference to the asset resolves to nothing.
It found exactly that on its first run.

## Traps, each learned the hard way

- **Always pass `-assemblyNames`.** Unfiltered, EditMode also runs UMA's own bundled tests —
  1,243 cases with 37 failing inside `UMA.Editors` / `UMA.Tests` / `UMA.TexturePaint`. Third-party
  noise that makes a green suite look red.
- **Forward slashes in every Unity CLI path from Git Bash.** A backslash `-logFile` silently
  dropped the `Logs/` segment, wrote no results XML at all, and stopped `-assemblyNames` taking
  effect, so the suites exited 2 looking like a real failure.
- **PlayMode crashes intermittently inside Unity's GPU Resident Drawer.** Not a test failure and
  not project code. The tell is a log ending without `Test run completed` and no XML; the script
  flips `m_GPUResidentDrawerMode` off on the copy only. Re-run.
- **Read the result out of the XML, not the shell.** `grep -o 'Test run completed.*'` on the log
  and `grep -c 'Crash!!!'`; a run that neither completed nor crashed is a stale log from a previous
  day — check its timestamp before believing it.
- **Never sync while a run is live.** The script serialises on `waitfree` for that reason.
- **While a run is live, the Unity MCP may be attached to it.** The acceptance copy carries the
  same MCP package, and the relay serves the most recent editor instance - a batchmode run is
  one. Check `GetProjectRoot` before any scene or asset call, and do editor-side work on C:
  before launching a run or after it exits.
- **Compile errors come back from the editor's console as type `Log`.** When reading the Unity
  console over the MCP, ask for `Types: ["All"]`.
- **`GAMESIM_UMA` must never be committed** in `ProjectSettings/ProjectSettings.asset`; the editor
  adds it locally on any machine with UMA. Check `git status` before every commit.

## Blender

Authored assets are built and exported by the scripts in `ArtSource/` (see its README): a `bpy`
script per set piece, an export checklist that enforces the conventions, and an
`AssetPostprocessor` on the Unity side. Run a script headless with
`blender --background --python ArtSource/setpieces/<script>.py -- <out.fbx>`.


Blender 5.2.2 LTS is at `C:\Program Files\Blender Foundation\Blender 5.2\`. Claude drives it through
**mcp-for-blender** (the renamed `blender-mcp`): the add-on
`%APPDATA%\Blender Foundation\Blender\5.2\scripts\addons\blender_mcp.py` auto-starts a socket
server on `localhost:9876` whenever Blender is open (a startup script in `scripts\startup\`
re-enables it on every launch), and the MCP server `C:\Users\kelli\.local\bin\mcp-for-blender.exe`
is registered in `~/.claude.json`. Telemetry is off on both sides — the add-on's default sends
prompts, code and screenshots to the author. Every tool takes a `user_prompt` argument. Check with
`netstat -ano | grep :9876`. Asset conventions are Part 4 of `Assets/Plans/MASTER-PLAN.md`.

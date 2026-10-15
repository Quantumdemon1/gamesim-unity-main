# Tools

Verification without the interactive editor, plus the Blender bridge. None of this is imported by
Unity (it sits outside `Assets/`). Machine-specific paths are derived or overridable:
`GAMESIM_ACCEPTANCE` (the batchmode copy, default `D:\GamesimAcceptance`), `GAMESIM_UNITY_EDITOR` /
`GAMESIM_UNITY` (the editor, default derived from `ProjectSettings/ProjectVersion.txt`).

| Script | What it does | Time |
| --- | --- | --- |
| `offline-compile.ps1` | Smoke-compiles all eight owned assemblies from current source lists using cached editor references. Missing assemblies/configuration mismatches fail; outputs and dependencies are unique to this invocation. `-OutputRoot <directory>` retains fresh outputs on another drive. `-WithoutUma` requires matching UMA-free cached responses. Changed asmdef dependencies require a freshly regenerated Unity Bee graph; stale cached references are not acceptance. | seconds |
| `player-compile.ps1` | Same for all three player assemblies (two with `-WithoutUma`); validates player/UMA defines and refuses stale dependency outputs. Cached compiler checks do not replace Unity tests/builds. | seconds |
| `verify-review-candidate.ps1 -Name <fresh-name>` | Runs every owned suite - a failing suite no longer stops the ones after it - records each, then fails at the end if any failed. Enforces each assembly's minimum in `baseline.txt`, and retains before/after complete input manifests, exact `.meta` archives, XML/log hashes and individual test names. Missing/malformed XML, short suites (floor shortfall), a Unity that never exits and unexpected product-input drift still stop it at once. Each suite has its own deadline (see *Deadlines* below); a late run is recorded failed and never killed. `-WithoutUma` requires the existing separate UMA-free copy. | depends on suites and input hashing |
| `build-review-candidate.ps1 -TestName <passing-name>` | Requires a passing complete same-configuration test summary. Checks live and synced inputs, builds Review13, checks final inputs, retains a timestamped report and hashes the entire build tree. Supports `-WithoutUma`. | depends on changed assets |
| `profile-candidate.ps1 [-Executable <Gamesim.exe>] [-BuildEvidence <build evidence.json>]` | The performance evidence for the owner's 1920x1080 / 60 fps target: three clean 300 s `--gamesim-verify` profiles of the built player, one at a time, each into its own fresh save root - six, twelve and the sixteen-person verification-only stress house (`--gamesim-stress-roster`). Windowed, uncapped, never `-batchmode`; blocks competing Unity/Gamesim processes and records any that start mid-run. Only the exactly identified installed Unity Hub `resources/unity.exe` quoted `serve` helper is exempt, with a separate sanitized receipt; incomplete or unexpected identities still block. This inventory does not establish global CPU/GPU quietness. Writes `profile-summary.json`/`.md`; each valid run's `performanceAcceptance` (p95 <= 16.7 ms, p99 <= 33.3 ms) is the verdict, never its `Passed`; invalid runs read "Not assessed". Exit 0 (evidence complete) also needs `-BuildEvidence` to match the player's whole build tree file by file and one `buildGuid` across the three reports. | about 20 min |
| `test-profile-evidence.ps1` | Exercises the pure profile predicates, bounded closure/verdict conditions and exact Hub helper identity against 42 synthetic cases. No player or hardware measurement. | seconds |
| `test-review-evidence.ps1` | Exercises evidence comparisons against temporary synthetic fixtures. No Unity, mirroring or acceptance-copy access. | seconds |
| `test-acceptance-mirror.ps1` | Exercises the real mirror runner against temporary fixture projects, including retained subtrees, folder GUIDs, ordinary deletion and manifest prediction. No Unity or real acceptance-copy writes. | seconds |
| `sync-acceptance.ps1` | Holds the shared destination file lease, verifies an immutable byte snapshot, then checks every mirror against the exact UMA/NoUMA projection. GPU Resident Drawer is disabled only for explicit test mode. | depends on input hashing |
| `sync-and-run.sh` | Routes `name:Platform:Assembly[:filter]` to the same held PowerShell controller. Retains unique evidence; failures/skips/nonzero exits/shortfalls fail. Always diagnostic evidence, never a build gate. | depends on suites |
| `test-review-workflow.ps1` | Isolated lock/snapshot/XML/failure-retention fixtures, including real controller refusal paths. No Unity or real acceptance-copy access. | seconds |
| `test-review-suite-runs.ps1` | 46 isolated fixtures for how the controller runs suites, with a fake Unity (a `.cmd` writing NUnit XML): per-suite deadlines at least twice their measured durations, a late process recorded late and never killed (and abandoned, still running, past its grace), every suite run after a failure, the `GAMESIM_DIAGNOSTICS` define (refused for full acceptance, added to and taken off the copy), and a source status probe that leaves `.git/index` byte for byte. | about a minute |
| `test-asset-metadata.ps1` | Twelve isolated fixtures for metadata, GUID and binary-attribute enforcement, including the same Unity-ignored hidden-path missing-meta exemption as CI. No source or acceptance-copy mutation. | seconds |
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

### Deadlines

Each suite's deadline covers the whole Unity process (start, import, compile, the tests, exit) and is at
least twice the longest time measured for it (`Get-ReviewSuiteDeadline` in `review-workflow.ps1`):

| Assembly | Measured | Deadline |
| --- | --- | --- |
| `Gamesim.EditModeTests` | 2,305 s for 7,846 tests (NoUMA, 2026-10-14) | 7,200 s |
| `Gamesim.PlayModeTests` | 5,401 s for 1,218 tests (NoUMA, 2026-10-15); 4,305 s for 1,081 under `GAMESIM_UMA` | 14,400 s |
| `Gamesim.Uma.PlayModeTests` | 308 s for 89 tests (2026-10-15) | 3,600 s |
| `Gamesim.DiagnosticsPlayModeTests`, `Gamesim.Uma.DiagnosticsPlayModeTests` | the longest probe run, 135 s | 3,600 s |

Any other assembly takes its platform's (EditMode 7,200 s, PlayMode 14,400 s); a player build keeps its
5,400 s (measured builds 150-298 s). `-SuiteTimeoutSeconds` overrides every suite at once (the cap is
28,800 s). Past its deadline a run is recorded late and failed, and left to exit on its own; one still
running after a further grace (the deadline again) is abandoned while running, never killed, and the
failure names its pid. The sync and the suites refuse to start while any Unity holds the copy. Re-measure
and raise a deadline whenever a suite's measured time passes half of it.

### Diagnostics

Feasibility and diagnostic probes - the farewell's IK feasibility, inert-stream, stream-topology,
passive-IK, untouched-Idle and Source21 Idle Foot IK probes - live in their own assemblies under
`Assets/Gamesim/Tests/Diagnostics`, never in the shipping suites: `Gamesim.DiagnosticsPlayModeTests`
(skeleton probes) and `Gamesim.Uma.DiagnosticsPlayModeTests` (the same on UMA bodies). Both compile only
under the `GAMESIM_DIAGNOSTICS` define (the UMA one also needs `GAMESIM_UMA`), which is never committed:
a diagnostic run adds it to the test copy only, and the next ordinary sync takes it off again. Each has
its own floor in `baseline.txt`; the floor counts that the probes ran, not that they passed. `[Explicit]`
would not keep them out, because the batch runner executes Explicit tests.

```bash
# UMA-free copy: the skeleton probes
GAMESIM_DIAGNOSTICS=1 GAMESIM_WITHOUT_UMA=1 Tools/sync-and-run.sh diag:PlayMode:Gamesim.DiagnosticsPlayModeTests
# UMA copy: the UMA probes
GAMESIM_DIAGNOSTICS=1 GAMESIM_RETAIN_INSTALLED_UMA=1 Tools/sync-and-run.sh umadiag:PlayMode:Gamesim.Uma.DiagnosticsPlayModeTests
```

The same `GAMESIM_DIAGNOSTICS=1` in the environment of any direct `sync-acceptance.ps1` call (as the
locked runner makes) adds the define; `verify-review-candidate.ps1` refuses it, so full acceptance never
compiles the probes.

Use the review-candidate scripts for acceptance. A test snapshot disables GPU Resident Drawer to avoid
the editor harness crash. The build explicitly permits only its 0-to-1 shipping difference when matching
the tested BEFORE snapshot, and compares post-build inputs with the tested AFTER snapshot. The build's
window settings and known input-action preload are the only other permitted build changes. All other
product differences fail. Workflow changes under `Tools` are retained separately and never described
as tested product changes by the legacy comparison API. Schema4 workflows require the workflow bytes
to remain unchanged too. Keep feature/source edits frozen through the final tests and build.

Sync, full verification, build, and profiling share the held `.gamesim-validation.lock` file in the
destination. They fail immediately on contention; a lock file left on disk is not itself a live lease.
Do not bypass these entrypoints with an overlapping direct editor or copy operation. Every invocation
gets a fresh `Logs/validation/<label>-<UTC>-<GUID>` directory. Previous failures, source snapshots,
logs/XML and receipts are never overwritten. Full schema4 verification is required for a new build;
old schema3 summaries and filtered/partial diagnostics are deliberately refused.

Vendor-free worktrees may pass `-RetainInstalledUma` to sync, verify, and build. This explicitly
excludes destination `Assets/UMA` and its folder meta from mirroring, fingerprints every retained
vendor byte in the projected and before/after inputs, and requires that the vendor already exists.
It never installs, deletes, or puts licensed vendor content into Git. Use the same option for the
tests and build. Only this explicit option also adds the exact `GAMESIM_UMA` Standalone token to the
isolated copy and identical projection, if missing; source Settings stay byte-identical and other
platforms/tokens remain unchanged. Vendor presence by itself is not activation. `-WithoutUma` still
requires an existing vendor-free destination and omits exactly
the established optional native content; the two options are mutually exclusive. The shell bridge
uses `GAMESIM_RETAIN_INSTALLED_UMA=1` or `GAMESIM_WITHOUT_UMA=1` for these explicit routes.

Before hashing, both test and build copies remove only the `SENTIS_ANALYTICS_ENABLED` Standalone
symbol that the installed inference package removes on batch startup. The live editor's analytics
preference is untouched. `-WithoutUma` also removes the exact Standalone `GAMESIM_UMA` token.
The source preview applies those same declared copy settings. Explicitly retained `Assets/Resources`
and `Assets/UMAProjectData` directories retain their `.meta` companions as well as their contents.
The source-owned `Assets/UMAProjectData/Resources/AssetIndexerProject.asset` is the narrow exception:
UMA copies receive that file, its `.meta`, and the two parent-folder `.meta` files from source.
Other local UMA data remains retained. NoUMA skips the override. An incomplete four-file override
fails before any mirror starts, and manifest prediction uses the same allowlist as the real copy.
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

The UMA-free mirror also omits `Assets/Gamesim/Uma/Content` and `Assets/Gamesim/Uma/Resources`,
including their folder `.meta` files. These native recipes, slots, overlays and catalog assets require
the absent optional provider/vendor types; loading them can rewrite binary assets as missing-type YAML.
After the existing idle/package guards, the mirror preflights all four exact destination paths and their
ancestors/children for reparse points, then removes stale copies before mirroring. Provider code/asmdefs
and the retained top-level `Assets/Resources` / `Assets/UMAProjectData` policy remain unchanged. Preview
input accounting applies the same omission; NoUMA configuration checks reject any remaining optional
content. UMA mirrors continue to copy it normally. No asset conversion or drift approval is involved.

## The acceptance copy

The interactive editor holds the live project, so the suites run against a mirror:
`robocopy` `Assets`, `Packages`, `ProjectSettings`, `UserSettings` **and `Library`** (copying
`Library` avoids a reimport of thousands of files) to `D:\GamesimAcceptance` once, then
`sync-and-run.sh` keeps it current. It shares nothing with the live project, so the open editor and
its save slots are untouched.

## Before a commit

`Tools/check-asset-metadata.sh` asserts metadata and binary hygiene: every tracked Unity asset under
`Assets/` has a tracked `.meta` (Unity-ignored hidden paths follow CI's narrow exemption), no `.meta`
is an orphan, no GUID is used twice, and binary assets are not normalized as text. A `.cs` written
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

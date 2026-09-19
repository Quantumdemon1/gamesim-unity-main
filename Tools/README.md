# Tools

Verification without the interactive editor, plus the Blender bridge. None of this is imported by
Unity (it sits outside `Assets/`). Machine-specific paths are derived or overridable:
`GAMESIM_ACCEPTANCE` (the batchmode copy, default `D:\GamesimAcceptance`), `GAMESIM_UNITY_EDITOR` /
`GAMESIM_UNITY` (the editor, default derived from `ProjectSettings/ProjectVersion.txt`).

| Script | What it does | Time |
| --- | --- | --- |
| `offline-compile.ps1` | Replays Unity's Bee `.rsp` files through `csc` for all eight Gamesim assemblies, adding sources created since the last editor compile. Type-checks a change without the editor. Non-zero exit on any error. | seconds |
| `player-compile.ps1` | Same, against the *player* response files, which catches editor-only API used in runtime code — the editor compile cannot see that. Needs a player build to have generated the `P.dag` on the acceptance copy. | seconds |
| `sync-and-run.sh` | Waits for the acceptance copy to be free, mirrors `Assets/` and `ProjectSettings/` over it, then runs each `name:Platform:Assembly` suite headless and prints the totals and any failures. | ~1 min EditMode, 10–15 min PlayMode |

```bash
powershell -NoProfile -File Tools/offline-compile.ps1
```

```bash
Tools/sync-and-run.sh edit:EditMode:Gamesim.EditModeTests play:PlayMode:Gamesim.PlayModeTests
```

Baseline the suites must not drop below: **EditMode 1208/1208, PlayMode 152/152.**

## The acceptance copy

The interactive editor holds the live project, so the suites run against a mirror:
`robocopy` `Assets`, `Packages`, `ProjectSettings`, `UserSettings` **and `Library`** (copying
`Library` avoids a reimport of thousands of files) to `D:\GamesimAcceptance` once, then
`sync-and-run.sh` keeps it current. It shares nothing with the live project, so the open editor and
its save slots are untouched.

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
- **Compile errors come back from the editor's console as type `Log`.** When reading the Unity
  console over the MCP, ask for `Types: ["All"]`.
- **`GAMESIM_UMA` must never be committed** in `ProjectSettings/ProjectSettings.asset`; the editor
  adds it locally on any machine with UMA. Check `git status` before every commit.

## Blender

Blender 5.2.2 LTS is at `C:\Program Files\Blender Foundation\Blender 5.2\`. Claude drives it through
**mcp-for-blender** (the renamed `blender-mcp`): the add-on
`%APPDATA%\Blender Foundation\Blender\5.2\scripts\addons\blender_mcp.py` auto-starts a socket
server on `localhost:9876` whenever Blender is open (a startup script in `scripts\startup\`
re-enables it on every launch), and the MCP server `C:\Users\kelli\.local\bin\mcp-for-blender.exe`
is registered in `~/.claude.json`. Telemetry is off on both sides — the add-on's default sends
prompts, code and screenshots to the author. Every tool takes a `user_prompt` argument. Check with
`netstat -ano | grep :9876`. Asset conventions are Part 4 of `Assets/Plans/MASTER-PLAN.md`.

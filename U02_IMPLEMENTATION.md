# U02 — playable house prototype

Implementation date: 2026-09-10. This is the next package in `UNITY_PORT_ROADMAP.md`, not a completed game or final-art milestone.

## Delivered

- A furnished, cutaway five-space house: living room, kitchen, bedroom, private conversation room, and competition yard.
- One selected player with click-to-move navigation, a baked NavMesh, complete-path validation, and a visible selection marker.
- Bounded camera panning, right-drag orbit, wheel zoom, F recentering, and conversation framing with return to the prior view.
- Maya, a nearby interaction prompt, three authored dialogue responses, clickable controls and keyboard shortcuts. Dialogue pauses the current route and restores it on exit. Walls block conversation.
- Bootstrap now enters the house. Original scene assets remain available.
- Repeatable scene registration, automated regression tests, and a separate clean-cache Windows x64 build target.

The prototype uses primitive meshes and flat materials. Dialogue has no relationship, promise, competition, save, network or AI side effects. Those systems remain future roadmap work.

## Material changes

| Area | Files/content | Change |
| --- | --- | --- |
| Git hygiene | `.gitattributes` | Explicit binary override for the baked NavMesh; Unity's Force Text setting does not make this asset textual. Existing ignores continue excluding generated builds, logs, caches and user settings. |
| Runtime assembly | `Assets/Gamesim/Runtime/Gamesim.Runtime.asmdef` | Added Input System and uGUI assembly dependencies. |
| Entry flow | `Assets/Gamesim/Runtime/Bootstrap/GamesimBootstrap.cs` | Loads HousePrototype from the Bootstrap scene while retaining singleton initialization. |
| House runtime | `Assets/Gamesim/Runtime/House/` | Added HousePlayerController, HouseCameraRig, HouseInteraction, HouseNpc, HouseWalkable and HouseRoomMarker. UI blocks world mouse input; keyboard camera controls still work while hovering the HUD. |
| Scene and art | `Assets/Gamesim/Scenes/HousePrototype.unity`, `Assets/Gamesim/Art/Prototype/` | New authored primitive house, furnishings, characters, camera, lighting, EventSystem, room labels, and 11 reusable materials. |
| Navigation | `Assets/Gamesim/Data/HousePrototypeNavMesh.asset` | Saved baked walkable navigation; NPC carving obstacle prevents walking through Maya. |
| Scene setup | `Assets/Gamesim/Editor/HousePrototypeSetup.cs` | Added create-only-if-absent scene generation and non-destructive build registration. Refuses Play Mode and unsaved scene changes. |
| Editor assembly | `Assets/Gamesim/Editor/Gamesim.Editor.asmdef` | Added runtime, Input System, uGUI, navigation and test-runner references needed by editor tooling. |
| Foundation setup/builds | `Assets/Gamesim/Editor/U01ProjectSetup.cs` | Existing Bootstrap is no longer recreated. Scene setup and additional build entries are retained. Added U02 Windows target, enabled-scene validation and machine-readable build reports. |
| Test runner | `Assets/Gamesim/Editor/U01TestRunner.cs` | Durable asynchronous Edit/Play Mode execution across domain reloads, run IDs, status, NUnit XML, strict completion checks and busy/dirty-scene guards. |
| Edit Mode tests | `Assets/Gamesim/Tests/EditMode/FoundationTests.cs` | Inspect a preview scene and close it without replacing the user's active scene. |
| Play Mode tests | `Assets/Gamesim/Tests/PlayMode/` | New assembly and seven end-to-end navigation, input, bootstrap and conversation tests. Temporary test objects/devices are cleaned up. |
| Build settings | `ProjectSettings/EditorBuildSettings.asset` | Enabled order: Bootstrap, HousePrototype, original SampleScene. |
| Unity-generated setting | `ProjectSettings/URPProjectSettings.asset` | Unity serialized `m_ProjectSettingFolderPath: URPDefaultResources`; no rendering package or pipeline asset was replaced. |
| Documentation | `UNITY_PORT_ROADMAP.md`, this report | Recorded the agreed U01–U08 sequence, controls, milestones, evidence and limitations. |

Every new asset and asset directory has Unity-generated metadata. No duplicate GUIDs were found.

## Preservation

Before implementation, Assets, Packages, ProjectSettings and root hygiene files were copied to:

`C:/Users/kelli/Documents/Codex/2026-09-09/github-plugin-github-openai-curated-remote-2/work/unity-u01-backup-20260910-142144`

All original files remain. The original SampleScene and Bootstrap scene files and metadata, original art/settings assets, package manifest and package lock remain byte-identical to the backup. Repeating both U01 and U02 setup was checked with SHA-256 hashes: all three scene files were preserved and the build list remained three scenes.

Unity's test workflow temporarily removed the original preloaded Input Actions entry. That entry was restored after testing; the original PlayerSettings file matches the backup. No package upgrade, project-version change, repository remote, commit or push was made. The existing U01 output under `Builds/Windows` was retained; U02 writes elsewhere.

## Verification

| Check | Evidence |
| --- | --- |
| Unity connection | Read-only editor/scene/Console inspection through the existing local Unity MCP relay. |
| Edit Mode | 3 passed; 0 failed, skipped or inconclusive. Run `c96a5a8058fd4cabb6051792e026ec3f`; `Logs/U01-editmode-results.txt` and `.xml`. |
| Play Mode | 7 passed; 0 failed, skipped or inconclusive. Run `7e7bc0a3584742259f0fb05503e78ef0`; `Logs/U02-playmode-results.txt` and `.xml`. |
| Scene setup preservation | Repeated U01/U02 setup preserved SHA-256 hashes of Bootstrap, HousePrototype and SampleScene. |
| Windows clean-cache build | Succeeded, 0 errors, 510 warnings, 142,963,808 bytes, 78.57 seconds; `Logs/U02-build-report.json`. Built with CleanBuildCache and StrictMode. |
| Standalone startup smoke | Passed at 2026-09-10 21:47 UTC. Fresh executable reached the positive house-ready signal, stayed alive and logged no detected runtime errors. `Logs/U02-player-smoke.json` and `.log`. Test-owned process was stopped after verification. |
| Final editor state | HousePrototype open, clean and not playing; error-only Console query returned no errors. |

Play Mode coverage includes actual synthetic mouse floor clicks, agent arrival, right-drag orbit, scroll zoom, keyboard pan/recenter, all five connected rooms, invalid and locked movement commands, bootstrap entry, all three dialogue buttons, route pause/resume, camera restoration, Escape input and wall occlusion. Synthetic tests are not a substitute for a human playtest or broad hardware/accessibility testing.

The first standalone smoke found a transient native NavMesh activation error before recovery. Fixed the startup order by saving the player agent disabled and enabling it in Start, after NavMeshSurface registers its data. The direct-load and Bootstrap tests now assert disabled-at-scene-load and enabled/on-NavMesh-after-startup. Final verification uses a rebuilt player, not the earlier executable.

The standalone smoke uses `-batchmode -nographics`: it proves the build's entry flow and scene wiring, not GPU rendering or manual desktop interaction. The rendered house was visually inspected in the Unity Editor. A human desktop playtest remains pending.

### Build warnings

The build is error-free, **not warning-free**. Its 510 warning occurrences come from retained packages: Pipeline has no RuntimePipelineConfig and is disabled in the player; two internal rendering-debug shaders are stripped; Sentis/inference shaders report arithmetic and unsupported-variant warnings. The full distinct messages are in the JSON report. No packages were removed, upgraded or modified to hide these warnings. U02 does not use the optional runtime Pipeline or inference systems. The MCP command wrapper reports warning-bearing builds as unsuccessful, so build acceptance here uses Unity's actual BuildReport (`Succeeded`, zero errors) and the independently launched executable.

The Performance Test package generated temporary Resources assets during builds and removed them afterward; they are not part of the delivered source tree.

## Run it

Open `Assets/Gamesim/Scenes/HousePrototype.unity` and press Play, or start from Bootstrap to exercise the production entry flow.

- Click bare floor to walk; right-drag to orbit; wheel to zoom.
- WASD/arrows pan the camera; F recenters on the player.
- Walk near Maya and press E. Select a response with 1/2/3 or a button; Escape or the exit button returns to exploring.

Editor menus: `Gamesim > U01 > Run Edit Mode Tests`, `Gamesim > U02 > Run Play Mode Tests`, and `Gamesim > U02 > Build Windows Desktop`. The new executable is `Builds/U02-Windows/Gamesim.exe`; retain its adjacent player data and DLLs when distributing it.

## Next package

U03 introduces the pure C# simulation state, validated commands, relationships, promises and seeded randomness, verified against approved web-game fixtures before wiring those outcomes into the house. U02's Maya dialogue is intentionally not a substitute for that simulation bridge.

# Gamesim Unity port

Source: the agreed Unity plan in the GameSim Dev conversation, following the choice to build the first slice around a 3D house. The existing Unity 6000.6.0f1 project and its pinned packages are retained.

| Package | Deliverable | Acceptance gate | Status |
| --- | --- | --- | --- |
| U01 Foundation | Project structure, assemblies, Edit Mode tests, bootstrap and desktop build | Standalone desktop build launches | Implemented; standalone startup verified in U02 |
| U02 Playable house prototype | Basic house geometry, player movement, camera, one NPC, interaction prompt | Explore, approach, converse, and exit dialogue without navigation or camera failures | Implemented; automated tests/build/startup smoke passed; human playtest pending |
| U03 Simulation bridge | C# state, command validation, relationships, promises, seeded randomness | Equivalent inputs produce expected results from approved web fixtures | Implemented for documented native scope; 640 Edit Mode tests pass, including source diary/persona/sentiment/oath/study/bloc/NPC-conversation fixtures, ballot privacy and frozen v1/v2/v3/v4/v5→v6 migration |
| U04 Social gameplay | Six-character cast, motives, alliances, knowledge boundaries, authored fallback | A promise or betrayal changes a later decision for an explainable reason | Local social loop, post-eviction Diary Room/persona, oath consequences, private source voting-bloc coordination and physical durable NPC-to-NPC conversations implemented and tested. Source needs/furniture decision leaves are staged separately; broader strategic/activity scheduling remains open |
| U05 Complete episode | Competitions, nominations, veto, campaign, vote, eviction | Finish the episode through normal 3D interaction | Implemented, plus a four-week six-person season/finale; actual station-navigation and ceremony-button traversal passes |
| U06 Saves and services | Local recovery, supported web-save import, optional authentication/cloud saves and AI | Resume safely; offline play and expired authentication cannot strand the episode | Local saves/recovery and restricted import implemented and tested; online adapters unconfigured, not claimed tested |
| U07 Presentation | Final assets, animation, lighting, sound, captions, ceremony staging | Episode meets the agreed visual and interaction bar | Native procedural cast/audio, lighting, captions and accessible HUD implemented; final-art/choreography/visual acceptance still open |
| U08 Acceptance | Profiling, accessibility, regression tests, first-time playtests | One pinned build passes the agreed acceptance matrix | Pinned V6: 640 Edit + 84 Play tests, strict desktop build, clean five-minute graphical workload with fresh NPC activity, actual-UI study/bloc/autonomy seasons and copied-v4/v5-save migrations pass; earlier builds retained. Human/pacing/production-visual acceptance still open |

## Slice contract

An orbitable, Sims-like house view with click-to-move, player selection and conversation framing. The house contains a living room, kitchen, bedroom, private conversation room and competition yard. The later episode targets six contestants, including the player, and 30–45 minutes of play.

Ordinary C# and GameObjects implement the first slice. Gameplay will follow interaction → validated command → simulation → committed result → UI, animation, audio and save. Simulation state will use stable character IDs, independent of frame rate and scene objects. The web game is the reference for approved rules and fixtures.

U02's authored conversation is a navigation and interaction prototype. Relationships, promises, persistence, competitions and a complete episode belong to the subsequent packages. The first integrated promise/save/resume demonstration spans U03 and U06 plumbing.

## Open and play

Open `Assets/Gamesim/Scenes/EpisodeHouse.unity` and press Play, or start from Bootstrap, which now prefers the episode. `HousePrototype.unity` and `Assets/Scenes/SampleScene.unity` remain available; later local prototype/material edits were preserved on the September13 resume. Use the floor to move, right-drag to orbit, scroll to zoom, WASD/arrows to pan, and F to recenter. Approach any active housemate and press E. The episode button routes you to the living-room screen or competition yard; press E there to continue. R travels to the Diary Room; nearby E opens private reflections and eligible decisions with confirmation. J opens the notebook; F5 saves; Escape closes panels.

Use `Gamesim > Port > Start Isolated Preview` for testing without touching the normal save slot; Stop Isolated Preview restores the previous clean scene setup. The accepted schema6 NPC-conversation executable is `Builds/Port-Windows-V6/Gamesim.exe`. `Gamesim > Port > Build Windows Desktop` now targets separate `Builds/Port-Windows-V7/Gamesim.exe` for the next activity increment; no V7 build is accepted yet. Earlier verified builds remain untouched. See the implementation record for exact evidence and scope.

During Social free time, visit the Diary Room to review and confirm a study action. Memorize grants one preparation point; sneak has a45% success chance, adding two or losing one, bounded0–5. Each confirmation costs one social action. Preparation survives saves and weeks and boosts only the separate weighted weekly HoH/Veto simulation option, not precision play or final HoH.

`Gamesim > U02 > Create or Register House Prototype` creates the scene only when absent and registers it after Bootstrap. Existing scene files and other build entries are preserved on subsequent runs. The original SampleScene remains available.

Run `Gamesim > U01 > Run Edit Mode Tests` and `Gamesim > U02 > Run Play Mode Tests` for the regression suite. `Gamesim > U02 > Build Windows Desktop` writes a separate player under `Builds/U02-Windows` and a machine-readable report under `Logs`.

Generated output under Builds, Library and Logs is excluded from Git. Commit the Assets tree with every .meta, Packages and ProjectSettings together.

See `UNITY_PORT_IMPLEMENTATION.md` for the integrated material-change inventory, preservation evidence, tests/build/profile results and remaining limits. `U02_IMPLEMENTATION.md` remains the historical baseline report.

## Completion boundary

September13 accepted V6: source-derived NPC-to-NPC conversations, real paired house routes, independent saved social time/RNG, schema6 migration and save-before-publication pass640 Edit Mode and84 Play Mode tests, a strict clean build, headless and graphical seasons, and a clean five-minute graphical profile. Fresh seasons activate NPC conversations in week1; historical/imported saves activate next week without rewriting existing history. This does not port the full strategic/activity planner.

Post-V6 source now includes pure needs/activity arithmetic, detached source point/phase/chain catalogs, and4,714 original-source fixture rows. Actual Unity Edit Mode passes673 tests, zero failures/skips. These helpers have no live gameplay consumer and make no save-schema change; the separately staged one-TV-routine implementation will supply its own explicit shared ownership, persistence and native timing policy.

This is a playable native six-person slice with an additional short-season finale, not yet every original web subsystem or a signed-off AAA release. Final assets and ceremony choreography, first-time human playtesting and measured 30–45-minute pacing remain unaccepted. Optional online accounts/cloud/AI have not been connected. Full-cast/multi-phase web-save migration and broader web systems (deals, dynamic alliance/grudge lifecycle, autonomous strategic schedules, full diary/activity scheduling, crises/storylines, weekly focus/full mental-state modifiers and the full minigame catalogue) require further port work; they are not silently approximated or marked complete. Earlier accepted increments remain preserved. Next are exact source needs/furniture rule leaves, followed by a separately reviewed native furniture routine sharing conversation motion ownership and durable receipts. Staging those rules does not make furniture routines playable or imply complete web parity.

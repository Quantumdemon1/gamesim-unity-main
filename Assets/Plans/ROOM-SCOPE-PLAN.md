# Room scope: the house one room at a time

Design proposal v2, 2026-10-27. Docs only: it changes no code, scene, setting, save, test or rule. This version answers three independent reviews (gameplay, rendering, tests and process); §9 records what changed and what was rejected.

**The owner's request, 2026-10-26, verbatim.** "Fix the scope of the view of the house to be one room at a time to limit what has to be rendered of the house at once, create a plan on how to improve the current setup."

**Evidence base**
- Unity: branch `claude/room-scope` at main `772828ba` (PR #32, ceremony timing, merged). Code paths are relative to `Assets/Gamesim/`; the scene is `Scenes/EpisodeHouse.unity`; the pipeline assets are `Assets/Settings/PC_RPAsset.asset` and `PC_Renderer.asset`. URP and core are 17.6, read from the package cache.
- Five read-only surveys made this plan's numbers: the camera, the scene (a YAML reader over the scene and a binary reader over the 98 authored FBX files), the runtime, what depends on seeing more than one room, and the rendering techniques available in Unity 6000.6 with URP 17.6. Three reviews then checked the plan against the code. No Unity or Blender run was made for this plan.
- Evidence words:
  - **read**: taken from the code, the scene or the assets;
  - **logged**: counted in the lane's own Editor logs (editor runs only: a release player logs the atlas warning once in its life, §2.4);
  - **modelled**: a geometric model of the camera frustum, the floor boxes and the lights' range spheres (it reproduced the logged 42-map warning exactly, §2.4);
  - **measured**: a Unity run. The only measurement on record is the 2026-09-19 profile, which predates the lamps (§2.9).

---

## 0. The short version

**What the player gets.** In free play the camera looks at the room the player is in, framed along its long side so the whole room fills a third to a half of the frame. That room is drawn in full, with its furniture, dressing, lamps and people. The neighbouring rooms show only as the house's empty shell: floors, cut-away walls, fences and neon outlines, lit by their fills. Walking through a door cuts to the next room, the way television follows someone through a door; the black travel blink stays the sign of a warp. Ceremonies, competitions, the diary room, house events and conversations each draw their own rooms. The next stop stays pinned on screen, and every other room keeps an icon with its head count at the doorway that leads there. The overview stays the whole-house map. A display setting, "Show the whole house", brings back today's view.

**What the house costs today** (read, logged, modelled).
- The default view, 24 m out at a 55° lens and yaw 45, touches 7 of the 8 rooms. Even the closest zoom the scene allows (10 m) touches 2 to 8.
- Nothing hides anything: the house has no ceilings, its walls are 1.1 to 1.5 m cutaways, no occlusion data was ever baked, and the GPU Resident Drawer's occlusion culling is off. Only frustum culling limits what is drawn.
- 449 house renderers draw (the v2 survey's count; RS1's reader counts 450, §3.5), about 246k triangles, 727 submesh draws, 358 shadow casters.
- 14 soft-shadowed lamps need 6 shadow maps each. With the follow spotlight that is 85 maps in a 2048 atlas, so URP cuts every map from 1024 px to 128 px.
- Up to 16 UMA bodies (about 53k triangles each, measured for the player's) are all drawn, all cast shadows and all animate, wherever they are.

**The design (§3.2).**
1. **The camera carries its rooms.** Every camera move names the rooms it is about, in the same call: a shot's own rooms, or the floor rooms under its focus and subjects. Free play is the player's room; a follow is the followed houseguest's; the overview, the opening and the walk-out are the whole house. Each source goes live in the slice that frames it; until then it draws the whole house.
2. **Room layers.** At load, every collider-free house renderer is sorted into a room by the 8 floor boxes and moved to that room's layer (layers 20 to 27). The main camera's culling mask draws the shell plus the scoped rooms. The live feed's camera gets its own room.
3. **Lamps.** For the main camera only, lamps homed outside the scope are switched off while it renders and restored afterwards (or, if RS0's measurement favours it, confined to their room by rendering layers and left unshadowed). The worst room then needs 19 shadow maps, not 85, at 256 px instead of 128.
4. **Bodies.** A body is drawn while it is in a scoped room or its doorway band, and always while it is a camera subject or a prompt names it. Hidden bodies keep animating, walking, sitting and talking; their name plates and bubbles hide, and they cannot be clicked.
5. **The room view.** The free camera stays over its room. The farthest zoom is fixed per room and does not change as the camera orbits.
6. **Who is where follows the floors** (D14). Today it uses the nearest room marker, which names the wrong room for up to 39.5% of a floor. The room view would show that disagreement, so who is where, walk-ins, the "HERE" cards, the icon counts and Talk move to the floors in one declared gameplay slice.

**What stays exactly as it is.** Every GameObject stays active and every `Renderer.enabled` keeps its value. Every collider, the NavMesh, every anchor, seat, marker and floor, the NPC world, the simulation and the save format are unchanged (§3.3). The two gameplay changes are declared and owner-decided: who is where by floor (D14) and "not looking" while the view leaves the player's room (D18). No schema number, saved field, command kind or rules version is claimed. The one new stored value is a display preference.

**Slices.** RS0 (the probe), RS1 (the registry and the camera's rooms) and RSW (who is where) first; then RS0's measurement; RS2a (drawing), RS2b (clicks), RS3 (lamps); RS4 (bodies), RS6a (ceremonies and competitions), RS6b (the other shots); RS5 (the room view) alone; RS7 measures after (§5). Four full qualification sets plus measurement runs: about 30 hours of lane time.

**The owner's decisions come first**: §4, each with a default.

---

## 1. What "one room at a time" means here

### 1.1 The request in this game's terms

The house is eight rooms (§2.1). "One room at a time" means two things, and the plan does both:
- **The view.** The free camera frames one room, not the dollhouse. Today's default frames almost the whole house (§2.2).
- **The work.** Only that room's furniture, dressing, lamps and people are drawn and lit. Framing alone cannot do this: at any distance that fits one room, 2 to 7 more rooms are in frame, and the lamps' light and shadows reach across rooms (§2.2, §2.4). The other rooms must be left out of the drawing explicitly.

It does **not** mean that the other rooms stop. People in them keep walking, meeting, sitting and talking. The HUD's lists, the overview, the travel icons and the live feed still say where everyone is.

What the room view does expose is that the game's idea of "who is where" is not the floors. Today who is where, the walk-in offer, the free-time board's "HERE" cards and the travel icons' counts all use the nearest room marker plus distance, with no floor or sight check (EpisodeDirector.Conversation.cs:389-458; EpisodeDirector.FreeTimeBoard.cs:377-383; EpisodeDirector.Travel.cs:172-180). Talk is 2.8 m plus a sight ray at 1.15 m, which passes over the 1.1 m south-wing walls (EpisodeDirector.Conversation.cs:33-43; Editor/HouseExpansion.cs:94-139). In the dollhouse nobody notices. In a room view, the game would offer "Step in" on a pair it does not draw. D14 makes them follow the floors.

### 1.2 What the player sees

| Moment | Today | With the room scope (defaults from §4) |
|---|---|---|
| **Standing in a room** | The dollhouse at 24 m, yaw 45: every room, everyone, from far away. Name tags fade out past 14 m. | The player's room, framed along its long side: the whole room at the farthest zoom (11.25 m for the four north-block rooms, 10.5 m in the south wing, 18.25 m for the Yard), a third to a half of the frame (D4). Zoom in to 4 m (D11); orbit freely, and the distance never changes as you orbit; corners crop at diagonal yaws; panning stops at the room's edge. Beyond the walls is the empty shell (D2). Everyone in the room carries a name plate. |
| **Walking through a door** | The camera follows the walking player across the house: a floor click makes the player its subject (HousePlayerController.cs:436-450). | When the player's feet are 0.3 m inside the next room's floor, a cut to that room's framing (D5). The player is drawn throughout. Someone walking towards the player's room is drawn from 0.6 m before its doorway. |
| **Travelling far** (a route over 20 m) | A warp behind the travel blink (black, clearing over 0.3 s). | The same warp and blink, landing in the new room's framing with that room drawn from the blink's first frame. |
| **Following a houseguest** (cast rail, Tab, brackets, clicking them) | The camera eases across the house to them and follows at 10 m. | Their room. Starting a follow on someone in another room is a cut, and so is each door they pass through. |
| **Stopping a follow** | The camera stays exactly where it was (HouseCameraRig.cs:436-449). | The followed room stays the view, as a "watching" view, until F, a walk or travel returns to the player's room with a cut. |
| **A conversation** | The two-shot, 4.6 m away at eye height, looks across 2 to 5 rooms. | The conversation's room; the pair is always drawn. Over the walls behind them is the empty shell (D2), softened by the close-up depth of field. |
| **A ceremony** | The camera moves 1.2 s to the ceremony room and back, across the house. | The same moves. During a move, the room left and the ceremony room are both drawn; on arrival, only the ceremony room. The cast is gathered there, so everyone is drawn. |
| **A house event** (the episode screen or a story card shows an event whose people stand together) | The camera moves 0.8 s onto those people, wherever they are (EpisodeDirector.Camera.cs:81-114). | The same move; the rooms of the people it frames are drawn. |
| **An activity far away** | A warp with a cut and the blink (EpisodeDirector.HouseActivities.cs:201). | The same, with the activity's room drawn from the blink's first frame. |
| **A competition** | The yard shot's eye stands over the bedroom and private-room wall. | The Yard (D7). The empty floor of the two rooms under the camera shows in the frame's near edge, dark. |
| **The diary room** | The interview, behind the studio backdrop. | The Private room, which holds the diary set. The Yard behind the backdrop is no longer drawn. |
| **The overview** | Orthographic, every room, room chips, who is where, the live-feed card. | Unchanged, the whole house (D1). Leaving it returns to the room view. |
| **The opening** | The walk-in cranes up over the whole house to 32 m. | Unchanged, the whole house. At the hand-over there is a cut to the player's room (D6). |
| **The live feed card** | A second camera films the room where the action is. | Unchanged: that room is drawn for the feed alone (D10). |
| **Jury or evicted, "Watching the X"** | The camera moves to that room's marker. | The view is that room. Before any watch, the room nearest the camera's focus. |
| **Finding the way** | Room icons with "Houseguests here" counts and the ringed next stop show from 14 m out (fully at 18 m, clickable past 16 m), never over the player's own room unless it is the station or the diary (EpisodeTravelBeacons.cs:47, 642-700). | The next stop stays pinned and clickable at every room-view distance. Every other room has an icon with its count: at the doorway that leads there, or at the frame's edge for a room with no doorway from here (D17). |
| **Who is where** (HUD lists, "HERE" cards, icon counts, walk-ins) | The nearest room marker: wrong for up to 39.5% of a floor. | The floor the person stands on (D14). |
| **Something happens in the player's room while the view is elsewhere** | Sightings, walk-ins and invitations go on; the dollhouse often still shows the player's room. | As under the overview, the player is not looking: no sighting or walk-in, and an invitation waits until the view returns (D18). |
| **Clicking** | Anything under the cursor: a floor, furniture, a person. | A neighbour's visible floor still walks the player there. A hidden person or piece of furniture cannot be clicked or hovered: the click passes through to the floor (D13). |
| **Panels and menus** | The house keeps rendering underneath. | The room view keeps rendering underneath (a separate saving, §8). |
| **"Show the whole house" on** (D12) | - | Today's view and drawing, exactly. |

---

## 2. The current setup, with numbers

### 2.1 The house (read)

- **Plan.** 28 x 40 m:
  - a 2 x 2 north block of 14 x 10 m rooms: Living (x -14..0, z -10..0), Kitchen (x 0..14, z -10..0), Bedroom (x -14..0, z 0..10) and Private (x 0..14, z 0..10);
  - a 28 x 10 m Yard to the north (z 10..20);
  - a south wing of three 9.4 x 10 m rooms: HoH, Nomination and Games (z -20..-10). Nomination is the wing's hub.
- **Rooms are floors.** A room is its floor BoxCollider with `HouseWalkable`, matched by exact name (Runtime/House/HouseRoomQuery.cs:31-40). The ids are the same eight as `Simulation/RoomWords.cs`. Room markers are destinations, never volumes; several stand off-centre (the Living marker at (-5, -7.6), the Bedroom's at (-5, 2)).
- **Walls.** No ceilings. Walls are 1.5 m in the north block, 1.1 m in the south wing, and 1.2 m for the garden fences. One mesh, `bb_shell_house.fbx` (1,044 triangles, 4 materials), draws every wall. The 53 primitive walls keep their colliders (the NavMesh is baked from them) with their renderers off (Editor/HouseSetPieces.cs:513-545).
- **Doorways (10).** Living-Bedroom; two between Living and Kitchen; two between Bedroom and Private; Kitchen-Private; house to Yard; north block to Nomination; Nomination to HoH and to Games.
  - **Two are corner doorways.** House to Yard (x -1.37..1.38 at z 10) and north block to Nomination (x -1.29..1.43 at z -10) open onto x = 0, where two floors meet, and the walls on x = 0 are open beside them (z 8.62..9.98 and -9.97..-8.62). A walker through their middle stands a few centimetres inside either floor, so the walker rule (§3.2 a) also lets the last room go 0.3 m outside it.
  - **9 of the 10 are walkable on the baked NavMesh.** The kitchen run (x 1.0..7.4, z -0.8..-0.1) stands in front of the west 1.8 m of the 2.8 m Kitchen-Private gap, and the agents' 0.5 m erosion closes the last metre, so no polygon crosses z = 0 there and agents go round through the Living room and Bedroom (RS1's review read the committed `Data/EpisodeHouseNavMesh.asset`). Doorway tests take walkable doorways from the NavMesh, or carry a body through by its transform; a scene slice can shorten or move the run and rebake if the owner wants the door open.
- **The hierarchy is by kind, not by room.** Room Finish has one group per room. Set Pieces (about 165 FBX instances) is flat. The 37 lights sit flat under one root. No room root exists to switch, and room membership has to come from position.

### 2.2 The camera (read, modelled)

- **The free camera's envelope** (Runtime/House/HouseCameraRig.cs; scene values):
  - focus clamped to one rectangle, (0.075, 1.2, 0) ± (16.075, 22): the whole house plus 2 m (HouseCameraRig.cs:1104-1109);
  - distance 24 by default, 10 to 34; yaw 45 in the scene; pitch from 45° to 62° with distance, plus a ±20° orbit, clamped to 45-70°;
  - lens 55°, near 0.15, far 150; every layer in the culling mask.
- **The rig has no idea of a room.** `ConfigureBounds` exists but snaps the camera and moves `HouseCenter`, which the overview and the opening read (HouseCameraRig.cs:225-234).
- **The default view touches 7 rooms**, 6 of them more than 60% in frame (modelled). At the closest zoom, centred on each room, it touches 2 to 8 (the Living room 7).
- **Fitting one room depends on the yaw** (modelled). A 14 x 10 room fits at 11.25 m at yaw 0 (46% of the frame), 15.5 m at yaw 90 (25%) and 18.75 m at yaw 45 (16%). A south-wing room fits at 10.5 m at yaw 90 (35%), 11.25 m at yaw 0 (31%) and 15.25 m at yaw 45 (16%). The Yard fits at 18.25 m at yaw 0 (33%) and 29.25 m at yaw 45 (15%). At the scene's yaw 45, every fit distance is past the name plates' 14 m, and 2 to 7 more rooms are in frame.
- **The 10 m minimum is an unannounced revert.** Commit `099de66e` (2026-09-17, "Four metres, not ten") set 4 m in code and scene. The lighting pass, `998b2523` (2026-09-18), put 10 back in the scene without saying so. Clicking a houseguest therefore frames them at 10 m, not the intended 7 (HouseCameraRig.cs:400).
- **Scripted shots** (modelled):

| Shot | Lens and distance | Rooms in frame |
|---|---|---|
| Overview | Orthographic, half-height 22 at 34 m, pitch 62 | All 8, 98-100% each, by design |
| Walk-in crane, last key | 32 m, pitch 62 | The whole house |
| Ceremony framing | 11 m, pitch about 45.7, FOV 55 | All 8 touched at the nomination marker; 3 at the Yard |
| Competition | 12 m, pitch 18, FOV 50, eye at (0, 4.7, 3.6) | Yard 71%, Bedroom and Private 9% each |
| Two-shot | 4.6 m, pitch 25, FOV 40; eye about 2.6 m up | 2 to 5 rooms by yaw |
| House-event shot | 6.5 m, pitch 28, two-shot lens, 0.8 s | The event's people's room and its neighbours |
| Staged ceremony close shots | 1.9-3.1 m, pitch 10-12 | 1 room looking at the screen; 6 looking north |
| Diary interview | Eye at (7, 1.45, 1.4) | Private 32%, Yard 56% (behind the 2.9 m backdrop) |

- **Close shots look over the walls.** Their eyes stand 1.4 to 2.6 m up, above the 1.1-1.5 m walls, so the upper frame reaches across the house. This is the art question in D2.
- **Who moves the camera.** 48 call sites of `MoveTo`, `CutTo`, `FocusSubject` and `ClearSubject` in 16 runtime files: the director's Camera, CeremonyFraming, CeremonyStage, CeremonyStageVeto, Challenge, CompetitionArena, Conversation, DiaryRoom, HouseActivities, Opening, Overview, TalkSpots, Travel and WalkOut partials, `HousePlayerController` and `OpeningSequence.Cards` (10). Tests drive the rig directly too (PublicDisplays, VoteBoard, NpcClick's `FrameOn`, MapClick, Audio, CameraPhase4).
- **Four ideas of "the current room" disagree** (read):
  - `RoomUnderCamera`, from the camera's focus 4 times a second (Runtime/Episode/EpisodeDirector.RoomTone.cs:13-36), drives the room tone. At the default framing it reads no room, because the focus sits on the corner where four rooms meet.
  - The player's feet, 4 times a second, drive the travel beacons; that check builds a new `HouseRoomQuery` every time (EpisodeDirector.Travel.cs:259-264).
  - The nearest room marker drives who is where (EpisodeDirector.Journal.cs:97-133), with 11 readers in 6 files: the walk-in watch, the free-time board and screen, the journal, the room acts and the travel icons' counts. Because the markers are off-centre, it names another room for 39.5% of the Living floor (the part north of z = -2.8 reads "Bedroom") and 37.3% of the Kitchen's.
  - `TryLocate` returns no room within the body radius plus 0.10 m (0.45 m for a 0.35 m body) of every floor edge (HouseRoomQuery.cs:252-254), so doorways and wall lines read as no room.

### 2.3 What draws (read)

The static house: 449 drawing renderers (696 in all; the rest are off or inactive; the v2 survey's counts, which RS1's reader puts at 450 of 694, §3.5), about 246k triangles, 727 submesh draws, 181 distinct materials, 358 shadow casters. Per room, by each renderer's pivot (the survey's rule; RS1 recomputes it with the classifier's own rule, §3.5):

| Room | Renderers | Triangles | Submesh draws | Materials | Shadow casters | Shadowed lamps (home) |
|---|---|---|---|---|---|---|
| Living | 67 | 30,938 | 108 | 37 | 64 | 3 |
| Kitchen | 84 | 43,161 | 132 | 35 | 78 | 0 |
| Bedroom | 45 | 16,991 | 81 | 31 | 39 | 3 |
| Private (diary) | 19 | 12,300 | 26 | 17 | 16 | 2 |
| Yard | 71 | 79,062 (51,630 with the flight cases at LOD1) | 115 | 39 | 61 | 0 |
| HoH | 48 | 20,292 | 89 | 48 | 33 | 3 |
| Nomination | 23 | 17,428 | 34 | 18 | 18 | 2 |
| Games | 39 | 24,079 | 84 | 41 | 33 | 1 |
| Shared walls, bands and the shell | 53 | 1,686 | 58 | - | - | - |

**Rendering settings and flags.**
- **Static flags.** 311 renderers carry ContributeGI, OccludeeStatic and ReflectionProbeStatic; 138 carry none. None is BatchingStatic or OccluderStatic. Static batching is off on purpose, because renderers are toggled and re-materialed at runtime (Editor/HouseCinematicLighting.cs:123-132).
- **The PC pipeline (the only level a Windows player has).**
  - Forward+, with the SRP batcher and the GPU Resident Drawer on (`m_GPUResidentDrawerMode: 1`, `m_BrgStripping: 2`, Keep All); the drawer's GPU occlusion culling and small-mesh culling are off (`m_SmallMeshScreenPercentage: 0`).
  - Light layers are enabled (`m_SupportsLightLayers: 1`) and unused: every one of the scene's 487 `m_RenderingLayerMask` entries is 1, and no runtime code writes a rendering layer.
  - SSAO at full resolution, depth and opaque textures, HDR, SMAA High on the camera.
  - Post-processing is screen-space, so it costs by resolution, not by room.
- **The test copies run without the drawer.** The review runner syncs its copies with `-DisableGpuResidentDrawer` (Tools/run-review-suites.ps1:36, Tools/sync-acceptance.ps1:148), so lane PlayMode runs use plain SRP culling. Only the player build turns the drawer back on (Tools/build-review-candidate.ps1, `-AllowShippingGpu`).
- **No quality level to fall back to.** The Mobile level ("Lean" in the settings) is excluded on Standalone (ProjectSettings/QualitySettings.asset:61-62), so on Windows the Lean/Full switch has nothing to switch to (§8).

### 2.4 Lights and shadow maps (read, logged, modelled)

- **37 lights, all Mixed, 27 active:**
  - the Moon: directional, soft shadows, 4 cascades at 2048 px over 50 m;
  - 14 lamps: point, range 6 m, soft shadows at the High tier (1024 px per map), all standing 0.84 to 1.45 m high, below their walls' tops;
  - 8 room fills (9-16 m) and 4 screen lights (3.6 m), with no shadows.
  - The 10 Living-room TV-console lights are inactive.
- **The bake is indirect only** (Scenes/EpisodeHouse.lighting: `m_MixedBakeMode: 0`). Every lamp's direct light and shadows are fully realtime. Switching a lamp off removes its direct light and leaves its baked bounce; the sleep dim already relies on this (EpisodeDirector.HouseLife.cs:175-198).
- **Lamps reach across rooms.** They stand against shared walls with a 6 m range, so each one's range sphere reaches 2 to 5 rooms. Shadows are what stop their light at the walls (Runtime/Presentation/FollowRing.cs:20-23). Because every lamp is below its wall's top, the walls already shadow the neighbours' floors: a lamp's light reaches the next room only through doorways and on things taller than the wall. Shadowed lamps whose range reaches each room: Living 12 (72 maps), Bedroom 7 (42), Kitchen 6 (36), Private, HoH and Nomination 5 each (30), Yard 4 (24), Games 2 (12).
- **The atlas arithmetic.**
  - A shadowed point light takes 6 cube faces. The 2048 atlas holds 4 faces at 1024 px, 16 at 512 and 64 at 256.
  - The whole house in frame is 84 faces, 85 with the follow spotlight (hard shadows, Low tier, made at runtime: FollowRing.cs:131-150). URP then reduces every face "by 8", to 128 px.
- **Logged, in editor runs.** The lane's Editor logs carry "Reduced additional punctual light shadows resolution by 8 to make 84 shadow maps fit in the 2048x2048 shadow atlas" 7,084 times and the 85-map line 466 times. The brief's 42-map line (1,078 times) is the W3 full-field capture: the model gives exactly 7 lamps (42 faces) for that eye. URP re-arms that warning only when the shadow requests change, and only under `UNITY_ENABLE_CHECKS` (AdditionalLightsShadowCasterPass.cs:399-416, 458-462). The editor's compile defines it; the release player's does not. A release player therefore logs one line in its whole life, for the first scaled configuration, and the warning cannot measure a view there.

### 2.5 Bodies (read, measured once)

- **Count.** Up to 16 UMA bodies (`EpisodeValidation.MaximumCast` = 16): the player and 15 houseguests.
- **Size.** A test snapshot measured the player's body as 1 renderer, 44,438 vertices and 53,093 triangles. If the others are similar, 16 bodies are about 850k triangles, 3.5 times the whole static house, drawn again in the depth-normals prepass for SSAO, the Moon's cascades and every lamp cube face in range. Whole-scene opaque snapshots during competitions ranged from 301,844 to 808,657 triangles.
- **Rendering.** GPU skinning is on, `updateWhenOffscreen` is off, every body casts shadows, and hair and accessories add small skinned renderers. Skinned meshes are outside the Resident Drawer.
- **Animation.** Gamesim adds the Animator without setting a culling mode (Uma/UmaBodyProvider.cs:116-121). UMA's own `CullCompletely` default applies only with `initializeAnimatorWhenAdded`, which is false, and UMA recreates the Animator on a race change. So every body runs Unity's default, AlwaysAnimate (read; RS0 confirms it on the UMA lane). Primitive-rig bodies have no Animator at all.
- **Per-frame presentation.** UMA's face player writes 51 channels per body per frame with no visibility check. `CharacterPresentation.LateUpdate` writes about 15 Animator parameters and turns the head. Each houseguest's world-space name plate faces the camera and fades every frame (fully shown inside 9 m, gone past 14 m: HouseNpc.cs:249); past 14 m it stays enabled at alpha 0.

### 2.6 What runs when nothing sees it (read)

Everything. Specifically:
- **Simulation; must keep running:** NPC motion and the NavMesh agents, arrival checks (they gate meetings), the 10 Hz NPC social world, the walk-in watch, wandering, the week's acts, seat fitting from live hip and foot bones, and witness sightlines from head bones.
- **Presentation; can stop for a hidden body:** face poses, head look, name plates, bubbles, and the hot tub's 50 bubble particles.
- **Already in one room by nature:** the competition stage (Yard), the diary studio (Private), ceremony key lights and the follow spotlight.
- **Nothing to scope:** audio (four 2D sources), the 8 baked reflection probes, post-processing.
- **The main camera is never switched off.** The weekly recap, settings and menus only set `ControlsEnabled = false`.

### 2.7 What limits drawing today (read)

Only frustum culling.
- **Occlusion data.** The scene has none (`m_OcclusionCullingData: {fileID: 0}`), although the camera's occlusion flag is on.
- **Occluders.** There are none, and none would help: from a 45-62° pitch a 1.1-1.5 m wall hides a 0.6-1.5 m strip of floor.
- **Distance culling.** It cannot separate rooms either. In every case modelled, 4 to 7 other rooms have a point nearer the eye than the current room's far corner.

### 2.8 What can only be hidden one way (read)

These constraints rule most techniques out:
- **`SetActive(false)` on a floor** fails `HouseRoomQuery`'s validation for the whole house, and every NPC's motion with it (HouseRoomQuery.cs:101-140, 18 `TryCreate` sites).
- **`SetActive(false)` on other room content** has three failures:
  - on an NPC root, it fails the house's meeting coordinator (HouseMeetingCoordinator.cs:218);
  - on seats, anchors or markers, it ends poses and retires venues;
  - on "House Architecture", it unloads the NavMesh.
- **`Renderer.enabled` is already shared state.** It has 10 or more runtime writers and 14 readers that read it as "this piece is drawn": ScreenSurface, HousePublicDisplays, CharacterPresentation, CeremonySets, rest and talk-spot authoring at every NPC-world rebuild, farewell readiness, and others. `Renderer.forceRenderingOff` appears nowhere in the runtime.
- **Layers carry physics meaning only through colliders.**
  - Furniture collision proxies are separate collider-only children on layer 8.
  - 59 scene objects carry both a renderer and a collider: floors, walls, fences and a few primitive props.
  - A parked NPC's root (only the root) moves to layer 8 and back (HouseNpcMotion.cs:228-241).
  - `GrownPiece` copies its parent's layer once, at creation (GrownPiece.cs:97). `OpeningDoorSet` writes layer 0 on its pieces.
  - No runtime code writes a camera's culling mask. A capture test uses bit 31 of the main camera's mask as a guard; a UMA garment test isolates on layer 30.
- **Clicks hit colliders.** The floor click raycasts the Pick layers 500 m (HousePlayerController.cs:338, 397-450), so hiding a renderer does not stop its click.
- **The live feed** is a second camera rendered by hand in `LateUpdate` every third frame: 320 x 180, far clip 40, no shadows, no post-processing (Runtime/Presentation/LiveFeed.cs:71-101). It is aimed at whichever room has the action, so any hide that applies to every camera blanks it. No test checks its pixels.
- **The Resident Drawer reads renderer changes once a frame**, just before `PostLateUpdate.FinishFrameRendering` (core 17.6 GPUResidentDrawer.cs:245-255). A layer change in `Update` or `LateUpdate` reaches that frame's main-camera render, but not a manual `Camera.Render` earlier in the same frame (the live feed, the capture paths).

### 2.9 What is not known

- **No current profile exists.** The last one ran twelve UMA bodies on the full house, windowed at 1600x900 on the reference machine, the GTX 1060 6 GB / i5-8400 (ACCEPTANCE_MATRIX.md:173). It measured a median of 3.20 ms, p95 4.32 ms and p99 6.28 ms (the matrix's 300 s run: 3.24, 4.45 and 6.32 over 82,928 frames; Assets/Plans/MASTER-PLAN.md:135, 181).
- **It is a pre-lighting baseline.** It was committed at 07:54 on 2026-09-19 (`0fd79b8d`). The lamps arrived at 15:25 that day (`0fddd5d6`, "The house lit for the night"): before that commit the scene had 1 soft-shadowed light, after it 15. The number therefore contains none of the 84-map cost RS3 removes, none of the follow spotlight (2026-09-25) and none of the later furniture, and it does not split shadows, draws and skinning. RS0 is the first measurement with the lamps.
- **The target.** 1920x1080 at 60 fps on the GTX 1060 machine: p95 ≤ 16.7 ms, p99 ≤ 33.3 ms, judged only on a graphical, windowed run of at least 300 s (Runtime/Episode/VerificationPerformance.cs:15-26).
- **The gap.** How far the GTX 1060 is from that target at 16 bodies with the lamps is unknown, and so is which of house renderers, lamp shadows, bodies and SSAO carries the cost. That is why RS0 measures before anything visible changes.

---

## 3. Options and the recommended design

### 3.1 Options considered

| # | Option | What it saves | Risks | Effort | Verdict |
|---|---|---|---|---|---|
| 1 | **Frame one room** (clamp the camera only) | Nothing on its own: 2-7 more rooms stay in frame | The view changes with no saving | S | **Part of the design** (RS5): it is the player's half |
| 2 | **Baked occlusion culling** | Almost nothing: the walls hide a 0.6-1.5 m strip; no occluders exist | A large data asset to re-bake on every set change; static flags that tests pin to 0 (RoomFinishTests, EpisodeHouseYardTests, FlightCaseAssetTests) | M | Reject |
| 3 | **GPU occlusion culling** (the Resident Drawer's) | As little, for the same reason | A pipeline setting for no gain | S | Reject |
| 4 | **Distance culling** (`layerCullDistances`) | A clutter trim at best: distance cannot separate rooms (§2.7) | The Resident Drawer's culler has no per-layer distance code | S | Reject |
| 5 | **Split the house into additive scenes per room** | Memory and load as well as drawing | The NavMesh, the room query (every floor must be active), NPCs that live everywhere; a full scene restructure that breaks about 6 EditMode test files and 11 editor house passes | XL | Reject |
| 6 | **`SetActive(false)` on room content** | Everything in the room, CPU included | Breaks the room query, the NavMesh, venues, seats and the NPC coordinator (§2.8) | L | Reject |
| 7 | **`Renderer.enabled`** | Drawing and shadows | Fights 10 or more writers; changes 14 readers' answers (seat authoring, screens, farewell); re-shows struck props on restore; blanks the live feed | M | Reject |
| 8 | **`Renderer.forceRenderingOff`** (global) | Drawing and shadows; every reader keeps its answer | Hides from every camera, so the live feed and captures go blank. Whether the Resident Drawer honours a runtime toggle is unverified: core 17.6 names it only as an exclusion (GPUResidentDrawerDebug.cs:43) | M | Fallback only |
| 9 | **A per-camera hook** (`beginCameraRendering` hides, `endCameraRendering` restores) | As 8, for the main camera only | Toggling about 450 renderers twice per render; the Resident Drawer does not see a change made between two cameras in one frame (§2.8) | M | **For lights only** (they are few and outside the drawer) |
| 10 | **Per-room layers and the camera's culling mask** | Drawing and shadow casting, per camera, at the cost of one integer per room change. The drawer's culler rejects instances by layer (core 17.6 InstanceCuller.cs:427-437) | Only collider-free renderers may move (layers mean physics on colliders); moving bodies need re-layering; runtime content needs classifying; 8 layers claimed project-wide. The drawer keys its draw ranges by layer (InstanceCullingBatcherBurst.cs:178), so the 22 dining chairs, 19 plants and 13 small plants split by room: whole-house views issue slightly more draw commands | M | **Recommended for renderers** |
| 11 | **Lamp scoping** (other rooms' lamps off for the main camera) | Shadow maps from 85 to at most 19, resolution from 128 to 256-512 px; the direct-light cost of 11 or more lamps | The viewed room loses only what reaches it through doorways and above the wall line, because every lamp is below its wall's top (§2.4). Shadows off with the light on and nothing else would leak through the walls | S | **Recommended** (RS3, D8 i) |
| 12 | **Rendering-layer confinement** (light layers, already enabled and unused) | A light lights only renderers sharing its rendering layer, and `shadowRenderingLayers` filters its casters (UniversalAdditionalLightData.cs:188-207). Lamps outside the scope can stay on, unshadowed, confined to their room: no shadow maps, no leak, and the shell keeps its lamp pools. The overview could drop all 84 lamp maps the same way. A lamp's shadow layers can also leave bodies out of its cube faces, the one lever that helps a gathered ceremony | Every house renderer and body needs its room's rendering layer (no physics meaning, so floors and walls can carry it; shared walls carry both rooms); a hook writes the out-of-scope lamps' shadows; per-pixel light-layer tests in Forward+ | M | **The measured alternative** (RS0 ablation; RS3 chooses, D8 ii) |
| 13 | **Cheaper lamp shadows** (Medium tier, hard shadows, a 4096 atlas) | Atlas pressure regardless of room | A look change everywhere, including the overview | S | Option (D8 iii, §8) |
| 14 | **Animator culling** on hidden bodies | CPU for up to 15 bodies | Stale head, hip and foot bones feed witness sightlines (gameplay), seat fitting (a pop on arrival) and framing; `CullCompletely` stalls the reaction checks | M | **Not now**: pin AlwaysAnimate, measure in RS0 and RS7 |
| 15 | **Static batching** | Draw calls | Combines meshes, so it breaks runtime re-materialing (why it is off) | S | Reject: the Resident Drawer already instances the repeated props |
| 16 | **The drawer's small-mesh culling** (`m_SmallMeshScreenPercentage`, now 0, and its shadow equivalents) | A global trim of tiny dressing in every view, including the exempt ones (the overview, the crane, the walk-out at 24-34 m) | Small props vanish at distance; one `PC_RPAsset` field | S | Optional (§8); measured in RS0's ablations at V1 and V2 |

### 3.2 The recommended design

**(a) The view scope.**
- **The camera carries its rooms.** `HouseCameraRig` reports `Rooms`, set in the same call by every move:
  - `MoveTo(Shot)`: the shot's `Rooms`: named rooms, `FromFocus` (the floor rooms under its focus and subjects), or `WholeHouse`. `WholeHouse` is the default for a shot that names nothing, so a missed caller draws too much, never too little;
  - `CutTo(target)` and every travel blink: the target's room, at once, with no hold;
  - `FocusSubject(subject)`: the subject's room, tracked as they walk;
  - a free `MoveTo(point)`: the floor room under the point (a "watching" view), else the nearest floor box;
  - a timed move: the union of its start and end rooms until it lands, then the end's.
- **The director's scope**, `EpisodeDirector.RoomScope.cs`, reads the rig's rooms and applies holds on top: the whole-house setting (D12), `HoldWholeHouse(owner)` for tests and captures, reference-counted, and the declared exemptions (D6).
- **Sources go live one slice at a time.** Each source (ceremonies, competitions, conversations, the diary, house events, watching, free play, following) names its rooms in the slice that frames it (RS6a, RS6b, RS5). Until then it reports the whole house, so no slice ships an interim rule and no default view changes before RS5.
- **A walker's room** comes from the registry's 8 floor boxes directly, not from `TryLocate`'s safe interior. It changes once the feet are 0.3 m inside a new floor box (0.14 s past the wall line at 2.2 m/s, 0.075 s at 4 m/s); until then the last room is kept. It is checked every frame for every body (16 points against 8 boxes). A walker with no room yet (a load or the opening's hand-over in a doorway) takes the nearest floor box. The view changes rooms at most once per 0.6 s; a change inside that waits, with the subject still drawn.
  - **As built (RS1).** The last room is kept only while the feet are within 0.3 m of its own box; farther out, the walker takes the floor under its feet. Through the middle of a corner doorway (§2.1) the feet are never 0.3 m inside either floor of the far side, so the room changes 0.3 m past the wall line instead; a body warped to a floor's edge takes that floor, not the room it left.
- **Free play never derives its room from the focus point**, which reads no room at the default corner and in doorways. `RoomUnderCamera` keeps driving the room tone as today; in a room view the focus is clamped inside the room, so the two agree except in doorways.

**(b) The room registry and classifier.** In `Runtime/House/HouseRoomScope.cs`, run at load and again at every point that builds or moves dressing (RS2a).
- **The rule.** A renderer is classified when four things hold:
  - it is a `MeshRenderer`, `SkinnedMeshRenderer` or `ParticleSystemRenderer`;
  - its GameObject is on layer 0 and has no Collider;
  - it is not in the shell list;
  - its world bounds lie, in plan, inside one floor box grown by 0.25 m.
- **Where it goes.** It moves to that room's layer. Anything else stays where it is and draws always: the shell (floors, walls and the `bb_shell_house` mesh, fences, neon outlines, anything straddling two rooms), anything on another layer, and anything not yet classified.
- **Runtime pieces** take their room layer in the frame they are created and never change it afterwards. A piece that moves after creation goes to the shell. This keeps every layer change ahead of the drawer's once-a-frame read and away from same-frame manual renders (§2.8).
- **Room layers.** 20 Living, 21 Kitchen, 22 Bedroom, 23 Private, 24 Yard, 25 HoH, 26 Nomination, 27 Games, named in `ProjectSettings/TagManager.asset`. Layers 30 and 31 stay the tests'.
- **Lamps get a home room** by position: Living 3, Bedroom 3, HoH 3, Nomination 2, Private 2, Games 1, Kitchen 0, Yard 0.
- **As built (RS1).** `Runtime/House/HouseRoomScope.cs` keeps the rule with three precisions the saved scene asked for:
  - bodies (anything under a `HouseNpc` or `HousePlayerController`) are never classified, because they move: RS4 draws them by their walker room;
  - the shell list is the `Neon Room Outlines` root, the south wing's own outline (`HouseExpansion`'s `Trim` strips under `South Wing`) and the authored shell mesh (`bb_shell_house`);
  - where two grown boxes both hold a piece (it stands within 0.25 m of a shared wall), the room whose own floor holds its centre takes it; a piece centred on a wall line stays in the shell.
  - A lamp is a point light that casts shadows; its home is the floor box it stands in, else the nearest.

**(c) Drawing.** Each camera's mask is set from the scope:
- **The main camera:** `mask = (mask & ~RoomBits) | ScopeBits`. Only bits 20-27 are written, so the capture guard on bit 31 and every other bit are left alone.
- **The live feed's camera:** the shell plus its own subject's room.
- **The whole house:** all eight bits.

The Moon's and lamps' shadow casters come from the same culling, so hidden rooms cast no shadows into the shown one. That is the intent, since their lamps are off too.

**(d) Lamps** (D8).
- **(i), the default.** A `RenderPipelineManager.beginCameraRendering` handler, for the main camera only, switches off every enabled lamp homed outside the scope, recording what it changed; `endCameraRendering` puts back exactly what it recorded. URP calls `beginCameraRendering` before it culls (UniversalRenderPipeline.cs:533, then `context.Cull` at :1026), so the main camera's visible-light list never holds them. Between frames every Light reads as authored, so the sleep dim, the lighting tests and the season restart see what they see now.
- **(ii), the measured alternative.** Every house renderer and body carries its room's rendering layer, every lamp its home room's; out-of-scope lamps stay on for the main camera with their shadows off between begin and end. RS0 measures it and RS3 picks (i) or (ii).
- The fills, the screen lights and the Moon are untouched. The live feed keeps its room's lamps; it draws no shadows anyway.
- If RS0 shows that URP drops a light whose layer the camera's mask excludes, lamps move to their room's layer instead, and the hook goes.

**(e) Bodies.**
- **Which are drawn.** A body is drawn when any of these holds:
  - its room (the walker rule in (a)) is scoped;
  - its feet are within 0.6 m of a scoped room's floor box: the doorway band;
  - it is a camera subject: the player in free play, the followed houseguest, either person in the conversation;
  - a live prompt names it: the walk-in pair, the Talk target, the invitation's speaker, anyone tagged "HERE". With D14 these are on the player's floor anyway; the rule is the guard.
- **How.** Its renderer GameObjects (never the root, which parking moves to layer 8) go to the room layer when its drawn state changes, and again after any visual swap (outfit change, pool, rebuild).
- **Hidden bodies stay live.** They keep AlwaysAnimate, pinned where the body is completed, because UMA recreates the Animator. They keep their NavMesh agent, their seat fit and their Animator parameters.
- **What hides with them.** Their name plate (`HouseNpc.PlateSuppressed`), invitation bubble and caption hide, and they cannot be picked (`HouseSeatPresentation.TryPickNpc`, the hover, the click).

**(f) Clicks and hover.** A hit on a collider whose owner the main camera does not draw is skipped, and the ray goes on to the next hit. That covers a layer-8 furniture proxy classified to a hidden room and, from RS4, a hidden body's capsule. Floors and walls are always hittable, so a neighbour's visible floor still walks the player there.

**(g) The room view (camera)** (D4, D5).
- **A room clamp** sits beside the house clamp, never through `ConfigureBounds`. It narrows with the zoom: at the farthest zoom the focus is the room's centre and the whole room is in frame; closer in, the focus follows the player out towards the walls.
- **The farthest zoom is fixed per room**: its fit along its best heading. That is 11.25 m for the four 14 x 10 rooms (yaw 0), 10.5 m for the south-wing rooms (yaw 90) and 18.25 m for the Yard (yaw 0). It does not depend on the yaw, so orbiting never pulls the camera in; at diagonal yaws the room's corners crop.
- **The pitch curve keeps today's metres.** `PitchFor` lerps between the minimum and maximum distance (HouseCameraRig.cs:181-182), so the room clamp must not feed it.
- **Entering a room is a cut**, on the frame the subject's room changes:
  - the heading turns to the room's best heading nearest the current yaw;
  - the focus is the subject, clamped by the rule above;
  - the distance is the smaller of the current one and the room's farthest;
  - the scope switches in the same call.
- **The travel blink stays the sign of a warp** (`BeginTravelDip`, EpisodeDirector.Travel.cs:66-100): travel over 20 m and the activity warp. Under reduced motion, every change is a cut with no blink, as travel does today.
- **Name plates** are fully shown for every drawn body inside the room's farthest zoom; today's distance fade applies only to whole-house views.

**(h) Exemptions and the setting.**
- **The setting.** "Show the whole house", in the display settings, is stored as the preference `Gamesim.WholeHouse` in the session's preferences store (PlayerPrefs, or the save root's `preferences.json`), not in the save. It holds the whole house for as long as it is on. It lands with RS5, when the room view becomes the default.
- **The test hold.** `HoldWholeHouse(owner)` / `Release(owner)`, plus `HoldRoomScope(owner)` so tests and the probe can switch a source on before its slice makes it live.

**(i) Way-finding in the room view** (D17).
- The next stop (the episode screen, the diary, "Travel to competition") stays pinned and clickable at every room-view distance, through the existing `TryPin` (EpisodeTravelBeacons.cs:752).
- Every other room has an icon with its "Houseguests here" count, at the viewed room's doorway that leads there, or at the frame's edge in its direction for a room with no doorway from here. In the room view the icons are not gated on the rig's distance.
- The Final 3's names over the last few (MOCKUP-PASS M14) show on those icons and on the overview.

**(j) Looking away** (D18). The act watch (`ActWatchAllowed`, EpisodeDirector.AllWeek.cs:66-70), the walk-in watch (EpisodeDirector.Conversation.cs:357-366) and NPC invitations treat a view that leaves out the player's room as "not looking", as they already do for the overview. A held invitation waits until the view returns.

**(k) Who is where by floor** (D14, RSW). `HouseOccupancy` reads each body's walker room (a), not the nearest marker; its 11 readers follow. Walk-ins are offered only on a pair on the player's floor. Talk also needs the target on the player's floor, or both in one doorway's band.

### 3.3 What stays active and unchanged

- **Every GameObject stays active**, and every `Renderer.enabled`, `Light.enabled` between frames, `Collider.enabled` and `gameObject.activeSelf` keeps its value.
- **Physics:** every collider and the layer-8 furniture proxies; the NavMeshSurface and its baked data; every NavMeshAgent.
- **The rooms' furniture:** the floors (`HouseWalkable`), room markers, interaction anchors, seats, rest anchors, talk spots, conversation slots and venues.
- **The NPC world:** its 10 Hz tick, `HouseNpcMotion`, arrival checks, the meeting coordinator, `CanWitness` and the diary sightline (physics, never renderers or the frustum: EpisodeDirector.AllWeek.cs:66-70, 153-163; HouseMeetingCoordinator.cs:425-447). Storm-offs and travel are unchanged too.
- **Two declared gameplay changes, each the owner's call:** who is where, walk-ins, "HERE", the icon counts, the room acts and Talk read the floors (D14, RSW); and the act, walk-in and invitation watches count a view away from the player's room as not looking (D18, RS5). Neither touches `Assets/Gamesim/Simulation`, a saved field or a command kind. A walk-in's command still names a room id from the same vocabulary.
- **Bodies:** Animators run and write parameters; seat fitting reads live bones; outfit builds keep their timing (no deferral, so the `BodiesCompleted` HUD render, a known flake trigger, does not move).
- **The rest of the scene:** reflection probes, light probes, lightmaps, post-processing, audio and the room tone.
- **`Assets/Gamesim/Simulation` and the engine:** untouched. The dotnet subset, the goldens, the digests and the old-save fixtures do not move; the simulation sweeps never drive the NPC world, so walk-ins do not occur in them.

### 3.4 What changes (every write the scope makes)

1. `gameObject.layer` of classified, collider-free renderer objects, from 0 to 20-27, and of bodies' renderer objects (never their roots).
2. Bits 20-27 of the main camera's and the live feed camera's culling masks.
3. Lamps, by D8: (i) `Light.enabled` of the 14 lamps, only between the main camera's begin and end of rendering; or (ii) `renderingLayerMask` on house renderers and bodies, the lamps' rendering layers, and the out-of-scope lamps' shadows between begin and end.
4. `HouseNpc.PlateSuppressed`, the bubble and caption visibility for hidden bodies, and the plate distances in the room view.
5. The click, hover and seated-pick filters.
6. The camera rig: `Shot.Rooms` and the reported `Rooms`; the room clamp, the fixed farthest zoom and the cut into a room (RS5); the scene's minimum distance (D11).
7. The travel icons' placement and distance gating in the room view (D17).
8. The two gameplay changes (D14, D18).
9. One preference key.

### 3.5 Predicted savings (house drawing recomputed in RS1; lamps modelled; RS0 and RS7 measure them)

**RS2a's expectation.** RS1 recomputed the drawing columns with the classifier's own rule (§3.2 b) on main's saved scene (`772828ba`), by its own reader of the scene and its FBX models; the reader's mesh bounds agree with all 131 fitted furniture proxies, which Unity measured from the same meshes. The props branch (§7) rewrites the scene and moves these numbers, so the table is re-taken when props lands. RS1's tests compute membership by the rule and pin none of the drawing numbers; the lamp homes are pinned (3/3/3/2/2/1/0/0, 14 lamps), and the props head (`e1f1acf9`) keeps the same 37 lights and 14 lamps where they are.

| Room view | House renderers drawn (own + shell) | Triangles | Shadow maps (lamps + follow spot), D8 i | Per-map size in the 2048 atlas |
|---|---|---|---|---|
| Today, whole house | 441 | 245.8k (218.4k at LOD1) | 84 + 1 | 128 px |
| Living | 117 (72 + 45) | 32.5k | 18 + 1 | 256 px |
| Kitchen | 128 (83 + 45) | 44.7k | 0 + 1 | the spot's own 256 |
| Bedroom | 96 (51 + 45) | 18.5k | 18 + 1 | 256 px |
| Private | 68 (23 + 45) | 13.8k | 12 + 1 | 512 px |
| Yard | 107 (62 + 45) | 80.5k (53.0k at LOD1) | 0 + 1 | the spot's own 256 |
| HoH | 92 (47 + 45) | 22.2k | 18 + 1 | 256 px |
| Nomination | 65 (20 + 45) | 18.9k | 12 + 1 | 512 px |
| Games | 83 (38 + 45) | 25.6k | 6 + 1 | 512 px |

- **How it was counted.** Drawing renderers (enabled and active), one level of a LOD group (the Yard's three flight cases have two), mesh triangles once each, before depth, shadow and SSAO passes. The saved scene's 450 drawing renderers are these 441, the flight cases' 3 LOD1 renderers, and 6 pieces of the prototype cast standing in the saved scene, which are bodies: RS4 draws bodies, so they are left out. Of all 694 renderers, drawing or not, the rule puts 583 in rooms by their meshes' bounds; 37 share their object with a collider, 22 are on layer 2, 33 are on the shell list, 18 belong to bodies and 1 straddles two floors. §0, §2.3 and §5.3 give the v2 survey's 449 drawing renderers of 696; RS1's reader counts 450 of 694 in the same scene, with the flight cases' LOD1 renderers and the prototype cast counted apart as above. The two readers differ by one drawing renderer and two in all; RS0's counters settle it. (Unity may report empty bounds for a renderer on an inactive object, so RS2a should classify a piece when it is first drawn, not only at load.)
- **The shell: 45 renderers, 1,562 triangles,** drawn in every view: the 8 floors and 3 prototype planters (they carry colliders), the 20 neon outline strips of the north block and the Yard, the south wing's 12 neon trim strips, the authored shell mesh (every wall's caps, skirting and jambs, and the fences), and the kitchen's dining rug, which reaches 0.5 m through the south wall under the Games room. v2's pivot table counted 53 shared renderers; the difference is mostly the wall bands, which the rule gives to the room on whose side of the wall they hang.
- **Lamps** by position, as modelled: Living 3, Bedroom 3, HoH 3, Nomination 2, Private 2, Games 1, Kitchen 0, Yard 0.
- **Bodies.** Drawn and shadow-cast only in the room: about 106k triangles for two people instead of about 850k for sixteen. At a ceremony everyone is in the room, so the body saving is nil at the busiest moments; only D8 (ii)'s shadow layers could take bodies out of the lamps' cube faces there. The furniture and shadow savings remain.
- **The overview** keeps today's cost unless D1's option is taken.
- **The live feed** adds one room at 320 x 180 every third frame, as today.
- **Whole-house views** issue slightly more draw commands than today, because the drawer splits instanced props by layer (§3.1, 10). RS7 expects a small rise there.

---

## 4. Decisions for the owner

Each row has a default this plan builds on. The last column says what changing it costs.

**The owner's answers, 2026-10-27** (asked as four questions; each answer was the default):
- **D1 and D12:** "Overview + setting". The overview stays the whole-house map, and a "Show the whole house" display setting, off by default, brings back today's view.
- **D2:** "Empty shell". Beyond the viewed room: bare floors, cut-away walls and neon outlines, lit, with no furniture or people.
- **D5:** "Cut at the door". The view cuts once the feet are just inside the next room; long trips keep the travel blink.
- **D14:** "Follow the floor", option (a). Who is where, walk-ins, the "HERE" cards and the room head counts follow the floor each person stands on, in RSW.

The other rows stand at their defaults as the lead's calls, open to the owner. D8 and D16 are settled by RS0's measurement, at the owner's first stop.

| # | Decision | Default | If the owner changes it |
|---|---|---|---|
| D1 | Does the overview survive as the whole-house map? | **Yes, unchanged**: every room, room chips, who is where, the live-feed card, today's lighting. | Lamp shadows off in the overview (cheaper; at 34 m the 128 px shadows are soft anyway) is a small RS3 option after captures; with D8 (ii) the lamps stay on, confined, so nothing leaks through the walls. Dropping the overview loses an approved V5 feature and its tests (Shots_TheOverviewFramesEveryRoomAndKeepsTheCastMoving, the Overview_* and Beacons_TheOverviewsRoomChipsAreAMap). |
| D2 | What shows beyond the viewed room, through doorways and over the low walls? | **The shell**: floors, cut-away walls, fences and neon outlines, lit by the fills and the baked bounce. No furniture, dressing or people. The bake (indirect only, made with about 250 GI-contributing set pieces) may leave furniture-shaped dark patches on the neighbours' floors, and the reflection probes hold that furniture; RS2a's captures check the floors near sofas, beds and the kitchen island. | A dark surround: the out-of-scope fills off for the main camera, through the lamps' hook, so the room reads as a lit set rather than an unfurnished house. With D8 (ii) the shell keeps its own lamp pools. A void (doorways onto the navy clear colour) saves another 1,686 triangles but makes the house unreadable. If the imprints show: accept them, or rebake without set pieces contributing GI, which changes the viewed room's bounce too. Taller walls that rise for close shots are an art job, after RS6's captures. |
| D3 | Which room drives the view? | **The rooms the camera's move names** (§3.2 a): the player's room in free play, the followed houseguest's, a shot's own, a watched room. | The player's room only would break following, jury watching, ceremony framing and captures of other rooms. The raw camera focus reads no room in doorways and at the default corner. |
| D4 | The room view's framing | **The farthest zoom is fixed per room**, its fit along its best heading: 11.25 m in the north block (46% of the frame), 10.5 m in the south wing (35%), 18.25 m for the Yard (33%), with every name plate shown. Entering a room turns to its best heading nearest the current yaw. Orbit never changes the distance; corners crop at diagonal yaws. The focus stays over the room, so to see another room you walk there, follow someone or open the overview. RS0 captures every room at yaws 0 and 45 to back this. | Keeping the player's yaw on entry and fitting the room at the current yaw: at the scene's yaw 45 the room covers 16% of the frame at 18.75 m, past every name plate, among 3 to 7 empty shells, and orbiting at the limit pulls the camera from 18.75 m to 11.25 m. Panning into a neighbour could switch the view to it, at the price of a cut whenever the pan crosses a wall. |
| D5 | How does the view change rooms? | **A cut onto the new room's framing**: at a door crossing (once the feet are 0.3 m inside the new floor), when a follow starts on someone in another room, on Tab or the brackets across rooms, and when the view returns after a follow. The travel blink stays the sign of a warp (travel over 20 m, the activity warp). Under reduced motion every change is a cut. | The blink at every door: one errand through the Nomination hub (Living to Nomination to HoH) blinks twice in a few seconds, and the blink stops meaning "you were moved". For a clicked errand, drawing every room on its route for the walk and cutting once on arrival. A glide with both rooms drawn shows furniture popping in at the far side. |
| D6 | Which views are exempt? | **Whole house:** the overview, the opening walk-in until it hands over (then a cut to the player's room), the staged walk-out, and captures that ask for it. **Their own rooms:** ceremonies (the ceremony room; both rooms during the 1.2 s move), the veto meeting's close shot, competitions (Yard), the diary (Private), conversations (the pair's room), the house-event shot (the rooms of the people it frames), the activity warp (its room, from the blink's first frame), jury and evicted watching. | Each whole-house exemption keeps today's cost for that view. The walk-out could later draw only the rooms it crosses. |
| D7 | The competition shot's foreground | **The Yard only**: the empty, dark floor of Bedroom and Private shows in the near edge. | Adding Bedroom and Private draws their furniture and lamps in that edge (about 29k triangles and 5 shadowed lamps) during competitions. |
| D8 | Lamps and shadows | **(i) Lamps homed outside the scope are off for the main camera**; fills stay on; the Moon is unchanged; the atlas stays 2048. The worst room gets 256 px a map, twice today's 128. RS0 measures (ii) beside it and RS3 takes (ii) instead if its captures are closer to today's look at no more than 10% more GPU time. | **(ii) Confined, unshadowed neighbours** (§3.1, 12): no shadow maps outside the scope, no leak, the shell keeps its lamp pools, and bodies can leave the cube faces at a ceremony; every renderer carries a rendering layer. **(iii) A 4096 atlas** (about 32 MiB at 16 bits, against 8) gives 512 px in the three-lamp rooms and 1024 in the rest; it changes `PC_RPAsset`, which `Lighting_SeasonRestartPreservesFittedLightsAndThePipelineShadowSettings` pins. |
| D9 | Hidden bodies | **Not drawn, still animating** (AlwaysAnimate pinned); plates and bubbles hidden. Skipping face poses and head look for hidden bodies only if RS0 shows the CPU is worth it. | Animator culling would save more CPU but leaves bones stale for sightlines, seat fitting and framing (§3.1, 14). |
| D10 | The live feed | **Keeps showing other rooms**, its own room drawn for it alone. | Making it follow the viewed room would make the card show what the main view already shows. |
| D11 | The nearest zoom | **Restore 4 m** (the 2026-09-17 change, reverted without notice on 2026-09-18) with the room view in RS5. | Keeping 10 m keeps today's close-up framing; a click on a houseguest frames them at 10 m, not 7. |
| D12 | A "Show the whole house" setting | **Yes, off by default**, in the display settings, kept as a preference, landing with RS5. Its caption follows the settings' pattern ("House view: One room  (change)", "House view: Whole house  (change)"), checked unused. It is the rollback for players and the evidence switch for the before/after. | Without it, the only way back is a build. |
| D13 | Clicks on hidden things | **They pass through to the floor**; hidden people and furniture cannot be clicked or hovered. | Allowing them lets the player select what they cannot see. |
| D14 | Who is where disagrees with the floors (nearest marker, wrong for up to 39.5% of a room) | **(a) Fix it, in its own gameplay slice RSW, before RS4**: who is where by floor, keeping the last room in doorways; walk-ins only on a pair on the player's floor; "HERE", the icon counts and the room acts by floor; Talk only on the player's floor or a shared doorway band. RS4 also draws anyone a live prompt names, as a guard. Example of today's fault: the player at (-5, 1) in the Bedroom and a pair at (-5 ± 0.5, -1.5) on the Living floor are offered as a Bedroom walk-in, 2.5 m away, in a room the view will not draw. | **(b) The presentation bridge only**: draw anyone a live prompt names, wherever they stand, and leave who is where on the markers. No gameplay change, but the HUD keeps naming the wrong room, and the bridged person stands furnished in an empty shell. |
| D15 | Re-baselining | **At RS7**: the performance profile and the look sheet's "exploration HUD" shot move to the room view; the "house overview" shot is unchanged. RS0 also records a 300 s whole-house profile at 16 bodies, the first verdict with the lamps. | Keeping the profile on the whole-house view (through the setting) measures the worst case, not what players see. |
| D16 | Order | **No visible change before the numbers.** Set A (RS0's probe, RS1, RSW) is qualified, then RS0's measurement runs and the owner reads it. If its ablations show the bodies and lamps carry the cost, RS3 and RS4 move ahead of RS2a (RS4 then takes the camera-mask write). RS5 lands last, after the owner has seen it. | Starting at RS5 changes the view before anyone knows what it saves. |
| D17 | Way-finding in the room view | **(1)** The next stop stays pinned and clickable at every room-view distance. **(2)** Every other room keeps an icon with its count, at the doorway that leads there or the frame's edge, not gated on the rig's distance. **(3)** M14's names over the last few move to those icons and the overview. | Keeping today's distance gate: the icons show only past 14 m and are clickable past 16 m, so at the room view's 10.5-18.25 m most rooms lose the travel icons, the counts (the only in-view sign of who is where once other rooms' bodies are hidden), the next-stop pin and M14's names. |
| D18 | Events in the player's room while the view is elsewhere | **The player is not looking**: the act, walk-in and invitation watches treat a view that leaves out the player's room as they treat the overview; a held invitation waits. A gameplay change, built in RS5 with tests. | An event in the player's room ends the follow and cuts back to it. Leaving both alone lets the act watch commit a sighting, and an NPC walk up to the player's hidden body with a hidden bubble. |
| D19 | How the owner sees the room view before it lands | **Captures** from the probe runs on a frozen probe copy: after Set B (rooms and lamps), after Set C (shots), and on RS5's head before Set D (the room view). | Moving the owner's play copy to the branch, only on the owner's go-ahead. |

---

## 5. Slices

### 5.1 Rules

- **Landing.** Each slice is built, offline-compiled, tested, independently reviewed and landed on its own branch step.
- **Stacking.** Under the owner's 2026-10-23 rule, a slice may build on a committed slice not yet qualified, and one full set on the newest head qualifies every slice beneath it, with at most three unqualified at once. A red set is fixed on top and re-run over the whole stack.
- **Every set is the full set.** The gate, the UMA suite, the UMA-build PlayMode in its six groups, EditMode and the no-UMA PlayMode: about 300 min. The owner's rule never cuts EditMode or the UMA-build PlayMode from a step, and the no-UMA smoke is still with the owner, so the full no-UMA run stays.
- **Floors.** Tools/baseline.txt's floors are raised in each set's floor commit, with the tests that set adds; never lowered.
- **Off the lane.** The offline compile, the dotnet subset and the Mono harness for plain EditMode fixtures cost no lane time; scene-reading and PlayMode tests need Unity.
- **The lane runs without the Resident Drawer.** Its PlayMode copies are synced with the drawer off (§2.3), so lane tests prove the plain SRP path only. Whether the shipping drawer honours a layer, a mask or a runtime change is proven only in the player probe (§5.3), which fails its run on a mismatch.
- **Copies and builds.** Probe runs use a frozen probe copy: a detached checkout of the set's head, with the triangle patch, the drawer on as shipped, never a lane copy and never the owner's play copy. The release build goes through `Tools/build-review-candidate.ps1`, which needs the set's passed full-suite summary for the same inputs. The development build goes through a new `Editor/RoomScopeProbeBuild.cs` (BuildOptions.Development), whose URP compile is checked for `UNITY_ENABLE_CHECKS` on its first build. Timing passes follow `Tools/profile-candidate.ps1`'s rules: graphical, windowed, 1920x1080, nothing else on the GPU, and no Unity Editor running, so the owner's editor is closed for them.
- **The UMA rules.** Every set and probe needs UMA bodies: those runs go alone, never beside the owner's editor, from a detached checkout, with the triangle patch on the copy.
- **Bodies in tests.** Primitive-rig bodies have no Animator, so an animator pin on the no-UMA lane passes on zero animators. Body-animation pins live in `Uma/Tests`, assert (never assume) that animators equal bodies and are more than zero, and the 12 test files that set AlwaysAnimate on their own fixtures are not evidence for the provider's pin.
- **Clocks.** The blink, the beacon tick and any scope timer run on `Time.unscaledTime`, which `Time.captureDeltaTime` does not drive: tests wait for them in real seconds.
- **Reduced motion.** A reduced-motion test switches it on through Settings and asserts `cameraRig.ReducedMotion` before acting; the fixture's reduced-motion state is not a single switch.
- **Inconclusive is not green.** RS5 narrows the zoom range under existing `Assume` guards (EpisodePlayModeTests.Camera.cs:85, :112), so its qualification lists every non-Passed case.
- **Captures.** PlayMode captures run on a screen of their own; synthetic clicks need a frozen camera.
- **No schema change anywhere.** None adds a saved field, a command kind or a rules version, and none touches `Assets/Gamesim/Simulation`.

### 5.2 The slices

#### RS0 Measure first (Set A)
- **What.** (1) A views mode for the standalone verify, `--gamesim-verify-views`, beside the 300 s profile: new `Runtime/Episode/PortVerification.Views.cs`, and `Runtime/Episode/VerificationViews.cs` (Unity-free report arithmetic and the shadow-map model, like VerificationPerformance.cs). (2) Ablation switches for the probe only (lamp shadows, bodies, SSAO, Moon shadows, rendering-layer confinement, small-mesh culling). (3) The drawer-path checks P1-P7 (§5.3) inside the probe. (4) `Editor/RoomScopeProbeBuild.cs`, the development build, with a hand-written meta. (5) Lane pins. No shipped behaviour changes.
- **Proof.** RS0's measurement (§5.3), after Set A qualifies.
- **Tests.**
  - Add, EditMode: `VerificationViewsTests`: every view's focus is on its room; the report's fields; argument parsing; the shadow-map model gives 84 faces at V1 and 42 at V9's eye; the repeatability rule.
  - Add, PlayMode (lane, plain SRP path): `Culling_ARendererOnALayerOutsideTheMaskIsNotDrawn`; `Lights_ALampSwitchedOffForTheMainCameraStillLightsTheFeed`; `Lights_ALightOnALayerOutsideTheMask` (records which way URP 17.6 goes; RS3 turns it into an assertion of the branch it takes).
  - Add, `Uma/Tests`: `Bodies_EveryAnimatorAlwaysAnimates` (asserts animators equal bodies and are more than zero).
  - Change: none.

#### RS1 The room registry and the camera's rooms (Set A)
- **What.** (1) `Runtime/House/HouseRoomScope.cs`: room boxes from the 8 floors through one cached `HouseRoomQuery`; the walker rule (§3.2 a); the classifier (§3.2 b) as a pure table, not yet applied; lamp home rooms. (2) `Runtime/House/RoomLayers.cs`: layers 20-27. (3) `House/HouseCameraRig.cs`: `Shot.Rooms` and the reported `Rooms` for every `MoveTo`, `CutTo` and `FocusSubject`, computed, not applied. (4) `Runtime/Episode/EpisodeDirector.RoomScope.cs`: the scope with its holds and its per-source switches, all off; statics reset at `SubsystemRegistration`. (5) `EpisodeDirector.Travel.cs`: the beacon tick shares the cached query instead of building one 4 times a second. (6) Layer names in `ProjectSettings/TagManager.asset`. (7) §3.5 recomputed with the classifier's rule, recorded in this plan as RS2a's expectation. Nothing visible changes.
- **Proof.** The saved scene's classification is computed room by room (no numbers pinned, so the props branch can move them). A walker crossing each of the 10 doorways changes room once, and a doorway shuffle not at all. The HousePrototype scene, with no director, is untouched.
- **Tests.**
  - Add, EditMode (on the saved scene): `HouseRoomScopeTests`: `EveryDressingRendererIsInOneRoomOrTheShell`, `NoRendererThatSharesItsObjectWithAColliderIsClassified`, `TheShellHoldsTheWallsFloorsFencesAndOutlines`, `TheLampsLiveInTheirRooms` (3/3/3/2/2/1/0/0), `TheRoomLayersAreNamedAndUnused` (30 and 31 left alone), `HousePrototypeHasNoRoomScope`. Plain fixtures (Mono harness): `TheWalkerRoomChangesThreeTenthsOfAMetreInsideTheNewFloor`, `AnUnlocatedWalkerTakesTheNearestFloor`.
  - Add, PlayMode: `RoomScope_TheViewedRoomFollowsThePlayerThroughEachDoorwayOnce`, `RoomScope_ADoorwayShuffleKeepsTheRoom`, `RoomScope_TheViewedRoomFollowsTheFollowedHouseguest`, `RoomScope_EveryRigMoveReportsItsRooms` (a shot with and without rooms, `CutTo`, `FocusSubject`, a free `MoveTo`, a timed move's union), `RoomScope_AnActivityWarpReportsItsRoomOnTheBlinksFrame`.
  - Change: none.
- **As built.** The seams the later slices use:
  - `RoomSet` (`Runtime/House/RoomLayers.cs`): a set of rooms, one bit each in the layers' order, so `CullingBits` is the mask RS2a writes.
  - `HouseCameraRig`: `Shot.Rooms` and `Shot.RoomsFromFocus` (a shot naming neither is the whole house; a shot from its focus adds any rooms it names, so a caller names its framed people's rooms there), `Rooms` (resolved on read, and every frame), `UseRoomScope`. Free play, before any move and after F, is the player's room, tracked: a pan after a floor click, a released shot or an ended conversation begun in free play gives back the room the player is in then. A stopped houseguest follow keeps the room it showed; a released shot returns to the rooms it left, or to the subject it still follows.
  - `EpisodeDirector`: `RoomScope`, `ViewedRooms`, `HoldWholeHouse(owner)`, `HoldRoomScope(owner)`, `Release(owner)` (one hold per owner and kind; any owner's whole-house hold wins), `CurrentRoomScopeSource`, and `LiveRoomScopeSources`, a constant that is `None` in RS1: a slice turns its own source on there, beside the camera calls that name its rooms. The overview, the opening and the walk-out have no switch (D1, D6): they are the whole house under `HoldRoomScope` too, whatever the rig follows over them (`IsWholeHouseSource`).
  - Every body's walker room is tracked every frame (`WalkerRoomOf(body)`), for RSW and RS4; the rows of destroyed bodies go once a second. The holds and the scope belong to the director, so there are no statics to reset.
  - The doorway walk carries the player by its transform (the NavMesh closes Kitchen-Private, §2.1) and walks the two corner doorways through their middle as well. Added after review: `TheWalkerRoomChangesThreeTenthsOfAMetrePastACornerDoorway`, `HouseRoomScopePlanTests.APieceIsItsRoomsWithinAQuarterMetreOfItsFloor` (plain), `TheClassifierSortsOnlyColliderFreeMeshesOnLayerZero` (EditMode) and `HousePrototypeTests.RoomScope_TheHousePrototypeHasNoRoomScopeAtRunTime` (PlayMode).
  - Not in RS1: the 0.6 s limit on how often the view changes rooms (§3.2 a) belongs to the cut that shows it (RS5).

#### RSW Who is where by floor (Set A; waits on D14)
- **What.** `EpisodeDirector.Journal.cs` `HouseOccupancy` by the walker room, not the nearest marker. Its 11 readers follow: the walk-in watch (`Conversation.cs`), the free-time board and screen, the journal, the room acts and the travel icons' counts. `CanTalk` also needs the target on the player's floor or in a shared doorway band. Gameplay: it changes which walk-ins, "HERE" tags, room acts and talks are offered in live play; no save, simulation, golden or digest moves.
- **Proof.** Every sampled floor point names its own room; the reviewers' Living-Bedroom pair is no longer offered.
- **Tests.**
  - Add: `WhoIsWhere_EveryFloorPointNamesItsOwnRoom` (sampled, the Living's 39.5% included); `WalkIn_APairAcrossTheLivingBedroomWallIsNotOffered`; `Talk_IsNotOfferedOverASouthWingWall`; `FreeTime_HereMeansThisFloor`; `Beacons_CountsAreByFloor`.
  - Must pass unchanged: `WalkIn_IsOnTwoHouseguestsWhoAreActuallyTogether`, `Nearby_AConversationWithinEarshotOffersToListenIn`, `Beacons_EachRoomSaysWhoIsInItAndTheNextStopIsRinged`. The four test files that read occupancy (BeaconCounts, FreeTimeMetrics11, NotebookRooms, Travel) are checked before building; any that places someone off its marker moves to Change.
- **As built.**
  - `Runtime/Episode/EpisodeDirector.WhoIsWhere.cs` holds RSW's director side. `HouseOccupancy` puts each body in the entry of its walker room (`OccupancyRoom`, through RS1's `WalkerRoomOf`); the nearest marker only where the floors cannot say (no scope, or a floor no marker names). The list is still one entry per marker room, in the markers' order, and the room ids are the markers' own, which are the floors' (`RoomWords.Rooms`).
  - `Runtime/House/HouseWhoIsWhere.cs` holds the plain rules: the doorway band (0.6 m past a floor, by the true distance off a corner), `FloorsLetThemTalk` (one walker room, or each in the other's band) and `SurelyIn` (0.3 m inside a floor, that room to a walker from anywhere).
  - Talk (`CanTalk`): one walker room, or both in one doorway's band with no wall between their feet at 0.5 m. The band alone also holds two people either side of a wall, and the south wing's 1.1 m walls are under Talk's 1.15 m sight line, so a wall is looked for under every wall's top.
  - A click's walk over (`TryApproach`) ends 0.3 m inside the target's floor where a spot there is clear; otherwise the first clear spot, as before. Without it, a target just inside a doorway was approached from the far side, where Talk no longer opens.
  - The walk-in watch finds its pair through `TryWalkInHere`: the player's room by floor and the pair in it.
  - The 11 readers follow with no code of their own: the walk-in watch, the free-time board's HERE tags and footer line, the free-time screen's card order, HERE tags and location card, the social screen's location, the arrival line, the notebook's Who is where, the room acts' audiences (`PeopleIn`) and the travel icons' counts. `WhoIsWhere()` has two more: the overview's column and the live feed's caption. The live feed's own rooms (the player's, a conversation's) moved to the floors too (`FloorMarker`), so the room it names is the room its caption counts.
  - Not moved: `PlayerRoom` (the room acts' own room) and the beacons' player room stay `TryLocate`'s safe interior, which is already the floors but reads no room in doorways and within 0.45 m of a floor's edge; the act watch's `Witnessing` stays on `TryLocate`.
  - Tests as planned, and `Talk_AWalkOverEndsOnTheirFloor`, `LiveFeed_ItsRoomIsTheFloorItCounts` (PlayMode) and `HouseWhoIsWhereTests` (four plain fixtures, Mono harness). No existing test moved to Change: none places someone off its marker and reads a room by the marker.

#### RS0's measurement (after Set A)
See §5.3. The owner's first stop: the numbers, D8 (i) against (ii), and the slice order (D16).

#### RS2a Room-scoped drawing of the house (Set B)
- **What.** (1) Apply the classification at load and at every point that builds or moves dressing: CeremonySets (the Living screen and hot seats are copies of Nomination props: classified by position), CompetitionArena's stage, DiarySeatPose's backdrop, HousePublicDisplays, the memory wall's rebuild and HouseActivityEffects, each piece on its layer in the frame it is created. (2) The main camera's mask from the scope; the live feed's mask from its own room (`Presentation/LiveFeed.cs`, `EpisodeDirector.LiveFeed.cs`). No source is live yet, so players see today's drawing; tests and the probe hold the scope on.
- **Proof** (Probe B). Captures of every room at its farthest zoom with the scope held on, before (RS0) and after, side by side, with UMA bodies; the neighbours' floors checked for baked imprints (D2). Counters against RS1's recomputed table, as expectations. With the whole house held, RS0's whole-house numbers within the repeatability tolerance.
- **Tests.**
  - Add: `RoomScope_OnlyTheViewedRoomAndTheShellAreDrawn` (plain path; the drawer path is the probe's P2); `RoomScope_ACeremonyCopyIsDrawnInTheRoomItStandsIn`; `RoomScope_TheLiveFeedDrawsItsOwnRoom` (the feed's first pixel test); `RoomScope_CaptureLensKeepsItsGuardBit`; `RoomScope_NoColliderEverSitsOnARoomLayer`; `RoomScope_EveryStagingPointLeavesItsPiecesInTheirRoom` (one case each for CeremonySets, CompetitionArena, DiarySeatPose, HousePublicDisplays, the memory wall and HouseActivityEffects); `RoomScope_NoSourceIsLiveYet`.
  - Change, if they pin what moves: HouseLayersTests (the Sight and Pick masks; room layers carry no colliders, so the answers should not change); `Camera_ConversationKeepsLowFurnitureAndChoosesTheClearSideOfTallFurniture` and `Camera_ConversationChecksTheShiftedEyeWhenThePanelPublishesItsWidth` (they set layers by hand); `ChromeGate_ThroughACaptureLensTheChromeCoversWhatItStandsOver`; `LiveFeed_PublishesWordsOnlyWithTheirRenderedFrameAndFreezesBoth` (the feed's mask).
  - Must pass unchanged: `UmaHouseGarmentFitCapturePlayModeTests.AuthoredGarmentsRenderAcrossPermittedShapesAndHumanoidFitPoses` (layer 30 is not the scope's); `PublicDisplays_RealCompetitionProjectsTheCurrentHoHAndClearsOldPortraits` (pixels through the main camera; its own shot names no rooms, so it draws the whole house).

#### RS2b Clicks and hover (Set B)
- **What.** `HousePlayerController.cs`: the click, hover and furniture-pick filters of §3.2 (f), keyed on whether the main camera draws the hit's owner, so bodies join them in RS4 with no second change.
- **Tests.** Add: `RoomScope_AClickPassesThroughHiddenFurnitureToTheFloor`, `RoomScope_HoverSkipsHiddenFurniture`. Must pass unchanged: `HouseLife_HoveringFurnitureSaysWhatAClickDoes` (whole house until RS5; see RS5).

#### RS3 Lamps and shadows (Set B)
- **What.** (1) In `EpisodeDirector.RoomScope.cs`: the main camera's begin and end hooks (§3.2 d), subscribed on enable and removed on disable (statics outlive play sessions). (2) D8 (i), or (ii) if RS0 chose it: rendering layers on the house's renderers and the bodies, and the out-of-scope lamps unshadowed. (3) Or lamps on room layers, if RS0 found URP culls lights by the camera's mask. (4) The overview option of D1 and the atlas of D8 (iii) only if the owner takes them (`Assets/Settings/PC_RPAsset.asset`).
- **Proof** (Probe B). Shadow maps per room view, counted inside the main camera's begin callback after the hook, from the shadow-map model: 19, 13, 7 or 1 as §3.5 models. Captures of every room before and after: the room edges, where only doorway and above-wall spill is lost. `Light.enabled` read between frames identical to the authored state.
- **Tests.**
  - Add: `RoomScope_OnlyTheViewedRoomsLampsCastShadowsForTheMainCamera`; `RoomScope_TheLiveFeedKeepsItsRoomsLamps`; `RoomScope_LightsReadAsAuthoredBetweenFrames`; `RoomScope_TheSleepDimStillDimsItsFill`; `RoomScope_TwoPlaySessionsHoldOneSubscription` (R8).
  - Change: `Lights_ALightOnALayerOutsideTheMask` asserts the branch RS3 takes.
  - Must pass unchanged: `Lighting_SeasonRestartPreservesFittedLightsAndThePipelineShadowSettings` (changes only with D8 iii), `Lighting_TheEpisodeSceneShipsItsBakedNight`.

#### Probe B (after Set B)
The owner's second stop: the first room captures (D2, D8's look).

#### RS4 Bodies, plates and bubbles (Set C)
- **What.** (1) In `EpisodeDirector.RoomScope.cs`: each body's drawn state (§3.2 e), its renderer objects re-layered on a change and after every visual swap. (2) `Uma/UmaBodyProvider.cs`: AlwaysAnimate pinned where the body completes. (3) `House/HouseNpc.cs`: the plate suppressed while hidden, with no billboard or fade work. (4) `Episode/HouseNpcInvitationBubble.cs` and `Episode/HouseConversationCaption.cs`: only for drawn bodies. (5) `House/HouseSeatPresentation.cs` `TryPickNpc`, the hover and the click skip hidden bodies, through RS2b's filter. (6) Optional, by D9 and RS0's numbers: skip UmaExpressions and the head look for hidden bodies.
- **Proof** (Probe C). At 16 bodies, visible skinned meshes equal the drawn bodies, and shadow casters drop. A hidden walking body's bones move. A body seated while hidden is on its seat (hip-to-seat distance) on the first frame its room is drawn. An outfit change keeps every body on its room's layer.
- **Tests.**
  - Add: `RoomScope_TheSubjectIsDrawnOnEveryFrameThroughEachDoorway` (walking and running, the player and a followed houseguest, all 10 doorways, waited in real seconds); `RoomScope_ABodyIsDrawnInItsRoomAndInItsDoorway`; `RoomScope_APromptNamesOnlyDrawnPeople` (the walk-in pair, the Talk target, the invitation's speaker); `RoomScope_AHiddenHouseguestShowsNoPlateOrBubbleAndCannotBePicked`; `RoomScope_TheSeatedArriveSeated` (asserts hip-to-seat distance on the first drawn frame).
  - Add, `Uma/Tests`: `RoomScope_AHiddenBodyKeepsItsPoseLive`; `RoomScope_AnOutfitChangeKeepsTheBodyInItsRoom`; `Bodies_ARebuiltBodyKeepsAlwaysAnimate` (a race change or rebuild, read after UMA recreates the Animator).
  - Change: `NameTags_ShowAtAConversationsDistanceAndFadeAcrossTheHouse` (no plate for a hidden room); `NamePlates_AreThePlateAndTheFollowSpotlightHangsOverIt` (plates gated).
  - Must pass unchanged: `AllWeek_NoSightingFromAnotherRoomUnderAPanelOrDuringACeremony`, `WalkIn_IsOnTwoHouseguestsWhoAreActuallyTogether`, `Nearby_AConversationWithinEarshotOffersToListenIn`, `EpisodeScene_StartsSixDistinctNativeCharactersAndUsableHud`; the invitation-bubble tests (`NpcInvitationFlow_*`, `PortConversationAdapters_ActualBubble...`, `DialogueCampaign_RealIncomingOffer...`, `HudStatus_TheInvitationBubbleAndTheStoryPullNeverShareTheScreen`, `SceneStage_AnInvitationUpKeeps...`; the incoming-offer Hear press is a known flake).

#### RS6a Ceremonies and competitions (Set C)
- **What.** These sources go live, each shot naming its rooms in the call that moves the camera: `EpisodeDirector.CeremonyFraming.cs` (both rooms during the move), `EpisodeDirector.CeremonyStage.cs` (close shots), `EpisodeDirector.CeremonyStageVeto.cs` (the veto meeting's close shot, :306), `EpisodeDirector.Challenge.cs` and `EpisodeDirector.CompetitionArena.cs` (Yard, D7).
- **Proof** (Probe C). Captures of every ceremony and competition shot before and after, with real bodies; V6g measured.
- **Tests.**
  - Add: `RoomScope_ACeremonyMoveDrawsBothRoomsUntilItLands`; `RoomScope_TheVetoMeetingDrawsItsRoom`; `RoomScope_TheCompetitionDrawsTheYard`.
  - Change: `Camera_ACeremonyFramesItsRoomAndComesBack`; `CeremonyStage_CapturesTheVoteOnTheLivingScreenAndItsResult`; `CeremonyStage_AFullHouseNominationReportsEveryPlaceAndCapturesTheTable`; `CeremonyStage_AFullHouseEvictionReportsTheHotSeatsAndCapturesTheRoom`; `CeremonyStage_CapturesTheKeysOnTheScreenAndTheBlock`; `Presentation_CeremonyCardsFireDuringAPlayedEpisode`; `Apparatus_InHouseFullFieldResumeWaitsForNativeArrivalAndLateFitBeforeCapture` and `Apparatus_InHouseVetoAudienceRetainsOnlyProvenStationaryFieldOnActualPause` (their file is also the props branch's, §7); `Competition_TheYardSignReadsFromTheDeckOverTheCentreGate`.

#### RS6b Conversations, the diary, house events and the exemptions (Set C)
- **What.** These sources go live: the conversation two-shot (the pair's rooms); `EpisodeDirector.DiaryRoom.cs` (Private); `FrameHouseEvent` in `EpisodeDirector.Camera.cs` (the rooms of the people it frames); the activity warp in `EpisodeDirector.HouseActivities.cs` (`CutTo` and the blink set the scope at once); jury and evicted watching. The whole house, named explicitly: `EpisodeDirector.Overview.cs`, `EpisodeDirector.Opening.cs`, `Presentation/OpeningSequence.Cards.cs` and `EpisodeDirector.WalkOut.cs` (the opening's hand-over cut by D6). Capture paths: CaptureSet's 0.01 s move carries its rooms in the call, so the first captured frame is scoped; the look sheet's "house overview" stays whole house.
- **Proof** (Probe C). Captures of every shot kind before and after, with real bodies. The low shots' background over the walls is D2's question.
- **Tests.**
  - Add: `RoomScope_EveryShotNamesItsRooms` (finds the callers of `MoveTo`, `CutTo` and `FocusSubject` under `Runtime/` by a source scan, not a hand-written table; each names its rooms or is on the declared whole-house list; the scan's list of callers still on the default shrinks to nothing at RS5); `RoomScope_TheOverviewTheOpeningAndTheWalkOutDrawTheWholeHouse`; `RoomScope_ACaptureOfAnotherRoomDrawsThatRoomOnItsFirstFrame`; `RoomScope_AHouseEventShotDrawsItsPeoplesRooms`; `RoomScope_AnActivityWarpLandsScoped`.
  - Change: `Shots_TheDiaryRoomSeatsAndFramesThePlayerThenRestoresNavigation`; `DiaryRoom_TheConfessionalWearsItsIdentityInTheShot`; `OpeningCaptures_EveryStagedBeatIsPhotographedWithContent`; `OpeningStage_TheIntroductionHoldsTheHouseguestInFrame`; `StagedExit_CapturesTheGoodbyeTheDoorAndTheShutDoor` (asserts the whole house); `LiveFeed_WatchesTheHouseFromASecondCameraAndNamesTheRoom` (with pixels). Only if D6's hand-over changes the crane: `Parity_TheWalkInPushesInThenRisesThenCranes` and EditMode `OpeningPresentationTests.TheWalkInPushesInThenRisesThenCranes`.
  - Must pass unchanged, now asserting the whole house: `Shots_TheOverviewFramesEveryRoomAndKeepsTheCastMoving`, `Overview_AFloorClickGoesWhereItWasClicked`, `Overview_AWarpFromTheMapLeavesNoLensBehind`, `Overview_IsTheStrategicDashboardOverTheLabelledHouse`, `Beacons_TheOverviewsRoomChipsAreAMap`, `AmbientCaption_OverviewRoomListRemainsClearAtBothTextSizesAndHudDensities`, `ChromeGate_TheBriefingHidesTheRoomChipsAndThePromptAndKeepsItsRecommendedRow`, `Rail_APageOpenedFromTheOverviewIsTheOneLit`.

#### Probe C (after Set C)
The owner's third stop: the shots (D6, D7).

#### RS5 The room view (Set D, alone)
- **What.** (1) `House/HouseCameraRig.cs`: the room clamp beside the house clamp, narrowing with the zoom; the farthest zoom fixed per room; `PitchFor` kept on today's metres; the cut into a room (D4, D5). (2) Free play and following go live: door crossings, a follow started across rooms, Tab and the brackets, the return after a follow (the watched room kept until F, a walk or travel), a free `MoveTo` as a watching view, `EpisodeDirector.TalkSpots.cs`'s walk to a talk spot, an unknown room taking the nearest floor box, and jury and evicted players before any watch. (3) The minimum distance by D11: the scene value; `Editor/HouseCameraFraming.cs` fixes the minimum too; `Editor/EpisodeProjectSetup.cs:211-213` stops writing 48 and 56. (4) `EpisodeTravelBeacons.cs`: way-finding by D17. (5) `House/HouseNpc.cs`: plates shown inside the room view's distances. (6) D18's guards in `EpisodeDirector.AllWeek.cs`, the walk-in watch and `EpisodeDirector.NpcInvitations.cs`. (7) The setting (D12) in the display settings: a new caption checked unused, its label box at 1.3x its font. (8) The look sheet's "exploration HUD" shot moves to the room view (D15).
- **Proof.** Probe D, on this slice's head before Set D: captures of all 8 rooms at the farthest and nearest zoom at yaws 0, 45 and 90, a walk through every doorway, and a follow across three rooms; the owner sees them before Set D runs (D16, D19).
- **Tests.**
  - Add: `RoomView_TheFreeCameraStaysOverItsRoom`; `RoomView_TheFarthestZoomIsFixedPerRoomAndOrbitNeverPullsIn` (8 rooms x 4 yaws); `RoomView_EnteringARoomCutsToItsFraming`; `RoomView_ReducedMotionCutsBetweenRooms`; `RoomView_FollowingAHouseguestFollowsTheirRoom`; `RoomView_StoppingAFollowKeepsTheWatchedRoomUntilThePlayerActs`; `RoomView_TabAcrossRoomsCuts`; `RoomView_ALoadInADoorwayTakesTheNearestFloor`; `RoomView_TheNearestZoomIsFourMetres`; `WayFinding_TheNextStopIsPinnedAndClickableAtEveryRoomViewDistance` (every room at its nearest and farthest zoom, at yaws 0, 45 and 90); `WayFinding_EachAdjacentRoomsIconReachesItsRoom`; `LookingAway_AFollowElsewhereGivesNoSightingOrWalkInFromThePlayersRoom`; `RoomScope_TheWholeHouseSettingDrawsEverythingAndSurvivesARestart`; the settings caption sweep and the label fit at text sizes 1.0, 1.2 and 1.4.
  - Change (the dependency survey's list and the reviews'): `Camera_PitchFollowsTheDistanceAndKeepsTheOrbitOffset`, `Camera_ZoomsTowardWhatIsUnderTheCursor`, `Camera_ReportsStartupFramingOverTime`, `Camera_CloseAuthoredShotsCheckObstaclesInsideTheExplorationMinimum`, `Camera_PullsInWhileAWallOccludesTheFocusAndLetsGoAfter` (its skip is the minimum distance), `Camera_ATimedMoveLandsOnItsClock` (a free timed move into the Private room), `CameraInput_AMiddleDragKeepsTheGroundUnderTheCursor`, `CameraInput_TheScreensEdgePansWhenEdgePanningIsOn`, `CameraInput_AGamepadOrbitsPansZoomsAndRecentersThroughTheSameRig`, `Follow_ACastRailPortraitFollowsThatHouseguestWithARingAndAChip`, `Follow_BracketsCycleTheHouseOutsideAPanelAndLeaveTheCameraAloneInsideOne`, `Travel_EveryRoomCanBeGoneToAndLandsOnItsOwnFloor`, `Beacons_StayOffTheHouseCloseUpAndUnderAPanel`, `Beacons_TheNextStopIsNeverLostOffTheScreen`, `Beacons_EachRoomSaysWhoIsInItAndTheNextStopIsRinged`, `Beacons_AClickOnARoomsIconTakesThePlayerThereAndTheHoverSaysSo`, `Beacons_AnIconHalfUnderTheHudIsNotShown`, `Beacons_AnIconUnderTheHudIsNotShown`, `Beacons_TheScreenAndDiaryIconsOpenThemWithinReach`, `ChromeGate_NothingTheHouseDrawsProjectsIntoAHudCard` and `ChromeGate_NothingTheHouseDrawsProjectsIntoTheFinalThreeObjectives` (they pull back to 24 m and assert the icons; ChromeGate.cs:213, 215, 664), `ChromeGate_ThroughACaptureLensTheChromeCoversWhatItStandsOver` (the same helper), `HouseLife_HoveringFurnitureSaysWhatAClickDoes` (a free move to the Yard's pool, now a watching view), `Preferences_AnIsolatedRootKeepsItsSettingsAcrossAReload_AndLeavesPlayerPrefsAlone` (`Gamesim.WholeHouse` joins its `SettingKeys`, Preferences.cs:27-35), `Endgame_AtMapDistanceTheRoomsAndTheFewAreNamed`, `NameTags_ShowAtAConversationsDistanceAndFadeAcrossTheHouse`, `Presentation_CapturesAWalkthroughOfTheEpisode` (its 24 m frames), `Accessibility_ReportsHowLargeTheCastReadsOnScreen`, `Accessibility_ComparesCameraFramings` (24, 17 and 10 m), `Accessibility_CapturesTheHudOverTheSetForReview`, `Audio_TheRoomToneFollowsTheCamerasFocus` (its free moves to the kitchen and yard markers become watching views). The plan's largest test churn: about 33 tests. Its qualification lists every non-Passed case.

#### RS7 Measure after
Re-run RS0's measurement at the same views, the whole-house view through the setting; the 300 s profile at 12 and 16 on the new default (D15); the look sheet. Record the before-and-after table in this plan and the master plan's performance row. Expect a small rise in the whole-house rows' draw commands (§3.5). The owner signs off.

### 5.3 RS0's measurement: the probe

**What the views are for.** The views compare one pose with another, before and after. They give no verdict: the verdict rule needs a graphical run of at least 300 s at 1920x1080 (VerificationPerformance.cs:15-26), which RS0 records once on today's default at 16 bodies and RS7 on the new default.

**Views.** Each view is held 10 s to settle and 20 s to measure; timing passes repeat it three times, counter passes once.

| View | Pose |
|---|---|
| V1 | Today's default: 24 m over the house centre, yaw 45 |
| V2 | The overview |
| V3 | The closest free zoom (10 m) on the Living room's centre |
| V4a-h | Each room at D4's farthest zoom and best heading, measured before any scope, so the framing gain and the scoping gain separate; captured also at yaw 45 for D4 |
| V5 | A Living-room two-shot |
| V6 | Ceremony framing at the nomination marker, the cast two per room |
| V6g | Ceremony framing with the cast at the ceremony's places (the nomination table, the hot seats), at 12 and 16: the busiest real frame |
| V7 | The competition shot |
| V8 | The diary interview |
| V9 | The W3 full-field capture's eye, re-derived from the props branch's `FullFieldView` once that lands |

**Bodies.** The cast stands at fixed marks, held for the probe, so before and after see the same placement: at 16, two per room; at 12, two in each north-block room, the Yard and Nomination, with HoH and Games empty. A second pass at 16 with the NPC world running reports the live spread. The sizes come from `--gamesim-house-size` and `--gamesim-stress-roster` where a roster seats fewer.

**Two builds, four passes.**
- **Release build, graphical** (profile-candidate's rules): frame time (median, p95, p99), CPU main and render thread, at 12 and 16 on fixed marks and at 16 on the live world; the 300 s profile at 16 on today's default.
- **Development build, batchmode with a graphics device**: the counters and checks below, at 12 and 16.
- **Development build, graphical**: GPU time per view at 16 (FrameTimingManager is on in a development build without the Player setting; if the first run shows otherwise, the probe copy's own ProjectSettings carries Frame Timing Stats, never a lane copy and never committed), and the ablations.
- **Repeatability.** A view's three medians must lie within 5% of each other; otherwise it is run once more and flagged.

**Counters.**
- `ProfilerRecorder` render counters (batches, SetPass calls, draw calls, triangles, vertices, shadow casters, visible skinned meshes), used only once check P6 shows they include the drawer's draws.
- The drawer's own counters (Visible Instances, Layer Culled, Frustum Culled, Excluded Renderers, Coverage %), read in the development build with `Profiler.enabled` on, the "GPU Resident Drawer" category on, the live feed paused and the portrait cameras idle. They count camera views only, never shadow views, and sum every camera in the frame. Coverage % and Excluded Renderers are recorded once, to show the house's drawing renderers (449 by the v2 survey, 450 by RS1's reader, §3.5) are on the drawer path.
- Shadow work: counted by the probe itself, mirroring URP's AdditionalLightsShadowAtlasLayout: the shadowed lights the main camera's culling keeps, times 6 faces (1 for a spot), at their tier's resolution, halved until all fit. It is counted inside the main camera's begin callback, after any scope hook, plus the classifier's casters per lamp. The atlas warning is only a cross-check (P7).

**Ablations** (development build, graphical, at 16; at V1, V4a and V6g): the baseline; lamp shadows off; bodies `forceRenderingOff`; SSAO off; Moon shadows off; rendering-layer confinement (D8 ii, through RS1's classifier); and the drawer's small-mesh culling at V1 and V2. Alternatively, GPU `ProfilerRecorder` samples on URP's pass markers.

**Checks in the player** (drawer on; the probe first asserts the drawer is active, and fails its run on any mismatch):
- **P1.** The drawer is active: Coverage % and Excluded Renderers.
- **P2.** Each room has a marker renderer with a unique colour. Moved at runtime to a layer outside the main camera's mask, it is absent from the main camera's pixels on the next frame's render; restored, it is present again.
- **P3.** A layer changed in `Update` is not seen by a manual `Camera.Render` in `LateUpdate` of the same frame (recorded; the design never relies on it).
- **P4.** A renderer instantiated and given its layer in one frame is drawn correctly from its first render.
- **P5.** The same as P2 for `forceRenderingOff`, for the record.
- **P6.** The engine's Triangles and Draw Calls counters include the drawer's draws: a marker grid of known size, on and off.
- **P7.** The shadow-map count against the atlas warning, one camera configuration at a time, in the development build if its URP compile defines `UNITY_ENABLE_CHECKS`, otherwise in an editor diagnostic run on the probe copy.

**Lane pins.** The lane's PlayMode pins (RS0) check the plain path; `Bodies_EveryAnimatorAlwaysAnimates` runs on the UMA lane.

**What RS0 decides.** P2 or P4 failing moves classification into an editor pass that writes the layers into the scene (§6, R1). The light-layer pin decides between the light hook and light layers. The ablations decide D8 (i) or (ii) and whether RS3 and RS4 move ahead of RS2a (D16). The frame times tell the owner what the work buys on this machine.

### 5.4 Lane budget

Inputs: a full set about 300 min; a player build about 31 min with its evidence wrapper (a 2026-10-18 build); a view 30 s, three times in timing passes; 3 min to start each player run.

| Run | What it covers | Lane time |
|---|---|---|
| Set A | RS0's probe, RS1, RSW | about 300 min |
| RS0's measurement | release build (31), timing at 12 and 16 (57), live world at 16 (29), the 300 s profile (8); development build (31), counters at 12 and 16 (23), GPU time (12), ablations (33) | about 225 min |
| Set B | RS2a, RS2b, RS3 | about 300 min |
| Probe B | development build and a counter-and-capture pass at 16 | about 45 min |
| Set C | RS4, RS6a, RS6b | about 300 min |
| Probe C | as Probe B, the shot views | about 45 min |
| Probe D | as Probe B, on RS5's head, before Set D | about 45 min |
| Set D | RS5 | about 300 min |
| RS7's measurement | RS0's runs again, the 300 s profiles at 12 and 16 on the new default, the look sheet | about 255 min |

That is about 30 hours of lane time, not counting re-runs: a red set is fixed on top and re-run over its stack, about 5 hours more. The owner's stops come after RS0's measurement (the numbers), after Probe B (the first room captures), after Probe C (the shots), and after Probe D, before Set D (the room view).

---

## 6. Risks and rollbacks

### 6.1 Risks

| # | Risk | Guard |
|---|---|---|
| R1 | The Resident Drawer does not see a runtime layer change on a static MeshRenderer. | The probe's P2 and P4 find out first, in the player, with the drawer on; the lane cannot (its copies run without the drawer). Fallback: an editor pass writes the room layers into the scene once, and every re-runnable house pass (HouseSetPieces, HouseRoomFinish, HouseCinematicLighting and the rest) keeps them, with a drift test. Runtime pieces take their layer in the frame they are created and never change it; a piece that moves goes to the shell. Bodies are skinned meshes, which the drawer does not take. |
| R2 | URP ignores the camera's mask for lights, or the begin and end hook flickers. | The hook is the default and does not depend on the mask (§3.2 d). If the hook ever misbehaves, lamps switch globally from the main scope, and the live feed's room loses its lamps' direct light (the feed has no shadows already). |
| R3 | The look: room edges without the neighbours' doorway and above-wall spill; the empty shell beyond the walls; furniture imprints in the bake; low shots looking over the walls at empty floors. | Captures before and after every visual slice, the owner's eye (D2, D7, D8), and the setting. D8 (ii) and the dark surround are the measured alternatives. Art options (taller close-shot walls, a raised fill) come after the captures, never before. |
| R4 | The camera's own subject vanishing at a door, or popping at doorways. | The subject is always drawn. A room changes 0.3 m inside the new floor, checked every frame, at most once per 0.6 s; bodies are drawn from their doorway band. Tested through all 10 doorways at walking and running speed: the 9 the baked NavMesh opens, and Kitchen-Private with a body carried through it (§2.1). |
| R5 | A stale scope after a camera move: a missed caller, a cut, a warp, a capture 2 frames after a 0.01 s move, a test's own shot. | The rig sets its rooms in the call that moves it; a shot that names nothing draws the whole house; a source scan lists every caller (`RoomScope_EveryShotNamesItsRooms`). |
| R6 | Runtime content left unclassified (drawn everywhere: safe), or moved after classification (hidden in the room it moved to: unsafe). | Pieces are classified in the frame they are created; one that moves goes to the shell; a test per staging point checks that every room-layer renderer lies in its room. |
| R7 | Clicking invisible things. | The click, hover and seated-pick filters (D13). Floors stay hittable. |
| R8 | Static handlers outlive play sessions (domain reload is off at play start). | Subscribe on enable, remove on disable, reset statics at `SubsystemRegistration`. `RoomScope_TwoPlaySessionsHoldOneSubscription`. |
| R9 | Test churn and flakes: about 130 tests added or changed (about 75 new, about 55 changed), many of them captures and camera tests, the PlayMode flake class. | Captures on a screen of their own; frozen cameras for synthetic clicks; renders measured, not reasoned about. RS2 and RS6 are split, and RS5, the largest, runs in a set of its own. |
| R10 | Ceremonies gather everyone, so the body saving is nil at the busiest moments. | V6g measures it in RS0 and RS7; D8 (ii)'s shadow layers are the one lever there. |
| R11 | The gain may be small on the reference machine. | The 3.2 ms median on record is a pre-lighting baseline with none of the lamps' 84 maps (§2.9), so it says nothing about the shadow cost. RS0 is the first measurement with the lamps and runs before anything visible changes. The shadow resolution gain, 128 px to 256-512 px, shows whatever the timings say. |
| R12 | Other branches add props and staging. | The classifier reads position, so new props join their room by themselves; one that straddles rooms stays in the shell. RS1's tests compute membership rather than pinning drawing numbers (§7). |
| R13 | Who is where disagrees with what is drawn. | RSW (D14) moves who is where to the floors; RS4's prompt rule draws anyone a live prompt names. |
| R14 | Layers 20-27 are a project-wide resource. | Claimed in this plan and told to the other sessions. The tests' 30 and 31 stay theirs. D8 (ii)'s rendering layers need no names to work. |
| R15 | UMA recreates the Animator on a race change. | AlwaysAnimate is pinned where the body completes; `Bodies_ARebuiltBodyKeepsAlwaysAnimate` checks it after a recreation, on the UMA lane. |
| R16 | Lane tests pass while the shipping drawer misbehaves. | Drawer behaviour is proven only in the player probe, which fails its run on a mismatch (§5.3). |
| R17 | The room view loses the way between rooms. | D17: the next stop is always pinned; every room keeps an icon and its count. Tested at every room's nearest and farthest zoom and three yaws. |
| R18 | Events in the player's room fire while the view is elsewhere. | D18's guards, with a test. |

### 6.2 Rollbacks

- **Players:** "Show the whole house" (D12) gives today's drawing, lights and free camera.
- **Tests and captures:** `HoldWholeHouse(owner)`, reference-counted. The verify runs also take `--gamesim-whole-house`.
- **Slices:** each reverts on its own.
  - Reverting RS5 brings back the 24 m default and the whole house in free play: free play's source is simply not live.
  - Reverting RS6a or RS6b returns their shots to the whole house.
  - Reverting RS3 brings back every lamp.
  - Reverting RS4 draws every body.
  - Reverting RSW brings back who is where by nearest marker; RS4's prompt rule still covers the HUD.
  - Nothing to migrate: no save, schema or rule ever moved.

---

## 7. Fit with the current queue, and saves

- **Branch.** `claude/room-scope` from main `772828ba`.
- **Props** (`claude/props-competitions`). The overlap is wider than the competition code:
  - it rewrites `EpisodeHouse.unity` (`0a79d0e4` swaps 16 stand-ins for authored props in the Bedroom, Games room, HoH suite and Kitchen groups), which changes §2.3's per-room numbers;
  - it reframes the W3 full-field capture (`4d96bf48`, `FullFieldView`), which moves V9 and its 42-map line;
  - it changes `EpisodePlayModeTests.CompetitionApparatus.cs`, which holds RS6a's Apparatus tests;
  - RS6a's competition scope and RS2a's stage-build hook meet it in `EpisodeDirector.CompetitionArena.cs`.
  - RS0's measurement and §2.3's table are taken on a tree with props merged, the tree that will ship; if props lands later, V9 and the table are re-taken (one counter pass). RS1's tests compute membership, so no drawing number in them moves with the props; the lamp homes they pin are the same on the props head (`e1f1acf9`: the same 37 lights). Whichever branch lands second takes the other's tests.
- **Dialogue D1** (`claude/dialogue-d1`): the cast screen's chip and the Strategy panel. No file overlap is expected; RS5's setting is in the display settings, not the cast screen.
- **All-Star** (`claude/all-star-histories`, `claude/all-star-step1`): schema 30, the simulation and persistence. No overlap.
- **The lane.** One Unity job at a time. Sets, probes and measurements are UMA runs that go alone and never beside the owner's editor. Queue them between the other branches' sets, not inside them.
- **Saves: nothing changes.**
  - No schema number, saved field, command kind, or story, dialogue or competition rules version is claimed.
  - Old saves, the OLD-SAVE-ACCEPTANCE fixtures, the goldens, the season digests and the dotnet subset do not move. RSW changes which walk-ins live play offers; a walk-in's command still names a room id from the same vocabulary.
  - The one new stored value is the display preference `Gamesim.WholeHouse`, in the preferences store beside the camera speed and the edge pan, which is not the save.
- **Claimed resources:** layers 20-27 and that preference key.

---

## 8. Other improvements the surveys found

These are outside the room scope. Each is small and optional, listed for the owner to take or leave.

1. **The house renders under full-screen panels** (the weekly recap, settings, menus). Holding the last frame, or skipping the house camera while a full-screen panel covers it, saves the whole frame there. The HUD is drawn by a stacked overlay camera, so this needs care and a capture test.
2. **A desktop Lean level.** The Mobile level is excluded on Standalone, so Windows players' Lean/Full switch has nothing to switch to. A desktop Lean level (no secondary-light shadows, one cascade) gives low-end machines a real choice. It changes ProjectSettings/QualitySettings.asset.
3. **Lamp shadows.** Editor/HouseLighting.cs:23-26 argued against ceiling-height shadows over 1.1 m walls, yet every lamp later got soft High-tier shadows. Hard shadows, or the Medium tier, cut atlas pressure in the overview too (D1, D8).
4. **The drawer's small-mesh culling** (§3.1, 16): one `PC_RPAsset` field that trims tiny dressing in every view, the exempt ones included. RS0 measures it.
5. **Per-frame NPC checks.** `HouseNpcMotion` walks each houseguest's hierarchy, skeleton included, five times per check and re-validates the floors every frame, twice for a walker (HouseNpcMotion.cs:341-471). Caching that is view-independent CPU.
6. **Faded name plates stay enabled** at alpha 0 past 14 m: disable the canvas instead.
7. **The two-shot's side choice** calls `FindObjectsByType<HouseNpc>` on each evaluation (HouseCameraRig.cs:545-564).
8. **Re-running the project setup** writes distance 48 and maximum 56 into the rig (EpisodeProjectSetup.cs:211-213). RS5 fixes it in passing.
9. **A standing profile with counters.** RS0's probe stays as a verify mode, so every later art pass can be measured the same way.

---

## 9. Review

Three independent reviews (gameplay, rendering, tests and process) checked v1 against the code; every finding below was re-checked before it was applied.

**Applied.**
- **The subject vanished at every doorway** (v1: drawn within 0.6 m, switched 0.45 m plus 0.4 s in, so 0.25-0.50 s undrawn at every crossing). Camera subjects are always drawn; the walker rule uses the floor boxes with a 0.3 m hysteresis, checked every frame; a doorway test for the player and a followed houseguest (§3.2 a, e; RS4).
- **Way-finding was lost in the room view** (icons hidden below 14 m, clickable only past 16 m). New D17, §3.2 (i), a §1.2 row, and tests at every room's nearest and farthest zoom.
- **Who is where uses the nearest marker, not floors**, so walk-ins and Talk could name hidden people. §1.1 and §3.3 corrected; D14's default is now a gameplay slice, RSW, before RS4, with the prompt rule as a guard.
- **Missing camera movers** (FrameHouseEvent, the activity warp, the veto close shot, TalkSpots, the opening cards, the tests' own shots). The rig now carries its rooms in every move (§3.2 a); a shot that names nothing draws the whole house; a source scan replaces the hand-written table; new §1.2 rows.
- **The framing gave 16% of the frame at yaw 45, past every name plate.** D4 now fixes the farthest zoom per room along its best heading; orbiting never pulls in; the dark surround is added to D2; plates are re-tuned.
- **RS2's interim zoom rule** emptied rooms under the cursor and popped at a threshold. Removed: each source goes live in its own slice and draws the whole house until then, so no interim rule ships.
- **Undefined changes of view** (unfollow, Tab, a load in a doorway, jury before a watch). Defined in §1.2, §3.2 (a) and RS5, with tests.
- **§1.2's "today" for a door was wrong** (the camera follows the walking player). Corrected; D5's default is now a cut, with the blink kept for warps and the other options listed.
- **Events in the player's room while the view is elsewhere.** New D18, built in RS5.
- **The atlas warning cannot measure a release player** (it logs once without `UNITY_ENABLE_CHECKS`). The probe counts shadow maps itself; the warning is an editor or development cross-check (P7).
- **Engine counters do not see the drawer's work**, and the drawer's own counters need a profiler-enabled development build. §5.3 now says how each is read, with calibration P6.
- **The September profile predates the lamps** (1 soft-shadowed light before `0fddd5d6`, 15 after). §2.9 and R11.
- **No attribution of cost.** Ablations added; they can reorder RS3 and RS4 ahead of RS2a (D16).
- **No busiest-frame view.** V6g added.
- **Rendering-layer confinement was missing.** §3.1 row 12, D1, D2 and D8 (ii); the lamps-below-wall-tops finding corrected §2.4, §3.1 row 11 and R3.
- **Baked furniture imprints on the shell.** Named in D2 and checked in RS2a's captures.
- **The drawer reads changes once a frame.** §2.8, §3.2 (b) and R1: pieces take their layer in their first frame; P3 and P4.
- **Verdict rules and repeatability.** RS0 is split into release and development builds, batchmode and graphical passes, with a 5% tolerance; the 300 s profile gives the verdict.
- **Lane tests run without the drawer.** The drawer checks moved into the player probe (P1-P7); §2.3 and §5.1 say so; R16.
- **Partial sets.** Every set is the full set; the budget was recomputed from the view arithmetic and the build time: about 30 hours, against v1's 15. v1's "21 hours qualified alone" comparison is dropped.
- **Unscheduled probe proofs.** Probes B, C and D are scheduled and budgeted.
- **The test lists were incomplete.** Added to RS2a, RS4, RS5 and RS6b, including `Gamesim.WholeHouse` in the Preferences test's keys; the Audio test's condition corrected (it changes regardless of doorways).
- **Body pins were vacuous on the no-UMA lane.** They moved to `Uma/Tests` with an assertion, plus a rebuild case; the 12 files are no longer cited as evidence.
- **The props overlap is wider.** §7 lists the scene, `FullFieldView` and the apparatus test file; RS0 measures the tree that will ship; RS1 computes membership.
- **Probe numbers that were not measurements.** Lamps are counted inside the begin callback after the hook; counters are calibrated; modelled numbers are expectations only.
- **Test traps.** Unscaled clocks, reduced motion and Inconclusive cases are in §5.1.
- **Guards without tests.** One test per staging point, a two-session subscription test, a HousePrototype test, hip-to-seat on the first drawn frame, and the light pin turning into an assertion.
- **Slice size.** RS2 split into RS2a and RS2b, RS6 into RS6a and RS6b, and RS5 runs alone.
- **The build route.** Named in §5.1, with the development-build method added to RS0 and the owner's view decided in D19.
- **Floors.** Raised in each set's floor commit.
- **Nits taken:** the views are comparative and the 12-body marks are stated; §3.5's counts are bounds, recomputed in RS1; whole-house views expect a small rise in draw commands; small-mesh culling is listed and measured.

**Changed in the applying.**
- Probe proofs run once per set head (B, C, D) rather than once per slice: the slices of a set share one build and one pass.
- D8 keeps lamps off as the default and makes confinement the measured alternative. Confinement needs a rendering layer on every renderer and body, so the plan measures it before choosing it.
- The reduced-motion rule says to set the state and assert it, not that the fixture runs full motion, because the fixture's reduced-motion state is not a single switch.
- The reviewers' test "zooming toward a neighbouring room's houseguest keeps them drawn" is not added: the interim rule it guarded is gone, and in the room view the cursor zoom stays inside the room.
- D14 takes both of the review's options: the floor fix as the default and the prompt rule as its guard.
- The plan names no local folders, because the repository is public: the probe copy and the play copy are described, not named.

**Rejected.** None.

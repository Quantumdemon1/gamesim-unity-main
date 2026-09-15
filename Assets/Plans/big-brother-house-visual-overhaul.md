# Big Brother House — "The Sims" Style Visual Overhaul

## Project Overview
- **Game Title:** Gamesim Big Brother
- **High-Level Concept:** A reality-TV social-simulation game presented as a cutaway "dollhouse" where the player observes and interacts with houseguests in a stylized Big Brother house.
- **Players:** Single player (player character + AI NPCs such as Maya).
- **Inspiration / Reference Games:** The Sims (dollhouse cutaway view, colorful glossy furniture); Big Brother TV set (neon room outlines, spotlights, competition circle) — see attached reference image (Texture2D InstanceID `14293651197076`).
- **Tone / Art Direction:** Sleek modern TV set. Dark, moody backdrop with vibrant, glossy, neon-accented furniture and glowing room outlines. High contrast, cinematic bloom.
- **Target Platform:** StandaloneWindows64 (PC).
- **Screen Orientation / Resolution:** Landscape (desktop).
- **Render Pipeline:** URP (active asset: `PC_RPAsset`, HDR enabled, 2048 main-light shadows, 4 cascades, MSAA off).

### Current State (verified)
- Scene: `Assets/Gamesim/Scenes/HousePrototype.unity`, geometry all under `House Architecture` (EntityId `35254:256`).
- Geometry is procedurally built from primitives by `Assets/Gamesim/Editor/HousePrototypeSetup.cs` (menu `Gamesim/U02/Create or Register House Prototype`). The generator only rebuilds if the scene file is missing, so the saved scene is the live source of truth.
- Materials: 11 flat `Universal Render Pipeline/Lit` colors, all smoothness `0.18`, no textures, no emission (`Assets/Gamesim/Art/Prototype/*.mat`).
- Lighting: one realtime directional light `Sun` (EntityId `35140:256`), intensity 1.8, warm; flat gray ambient `RGBA(0.60,0.67,0.72)`; default skybox; no baked lighting; no additional lights.
- Camera: single perspective `Main Camera` (EntityId `35121:256`) under `House Camera Rig` (EntityId `35348:256`), pitch 55°/yaw 45°, FOV 55, SolidColor dark-navy background. **Missing `UniversalAdditionalCameraData` / post-processing disabled.**
- Post-processing: **no Volume in scene at all.**
- Constraint: PlayMode tests (`Assets/Gamesim/Tests/PlayMode/HousePrototypeTests.cs`, `EpisodePlayModeTests.cs`, etc.) validate room reachability, NavMesh, labels, and named objects. **All changes must be additive** — do not rename, move, or delete existing GameObjects or change floor/wall collider bounds that affect NavMesh.

## Game Mechanics
This is a **visual-only** overhaul. No gameplay loop, controls, NavMesh, or interaction logic changes.
### Core Gameplay Loop
Unchanged — observe/navigate the house, talk to NPCs (`HousePlayerController`, `HouseNpc`, `HouseInteraction`), run competitions. Visual polish must not alter walkable areas, colliders, or NavMesh bake.
### Controls and Input Methods
Unchanged — New Input System via `HouseCameraRig` (WASD pan, RMB orbit, wheel zoom) and `HousePlayerController` (click-to-move). No input changes.

## UI
No screen-space UI changes in this plan (the reference image's side panels / buttons are a separate UI task). This overhaul targets the 3D scene look only. In-world `TextMesh` room labels are kept; optionally restyled to a brighter emissive color so they read against the darker set (additive, non-destructive).

## Target Look (derived from the reference image)
1. **Dark TV-set backdrop** — deep near-black/navy background, low cool ambient so colored lights and emissive accents pop.
2. **Glowing neon room outlines** — the signature element. HDR-emissive strips tracing each room's perimeter, color-coded (e.g. blue = active/HoH room, yellow = general rooms, red = a highlighted zone), made to glow via Bloom.
3. **Central gold competition circle** — concentric emissive gold rings on the Competition Yard floor.
4. **Glossy, colorful furniture** — raise material smoothness, add emissive accents (TV screen glow, podium light strips), keep the existing vibrant palette but make surfaces catch light.
5. **Spotlights & hanging lamps** — point/spot lights with small emissive lamp geometry over the competition circle, kitchen island, dining/private area; bloom makes them read as glowing fixtures.
6. **Cinematic post-processing** — Bloom (key effect), Tonemapping, Color Adjustments (contrast + saturation), Vignette, SSAO for contact shadows, optional subtle Depth of Field for the "toy dollhouse" feel.

## Key Asset & Context
### Files to create
- `Assets/Gamesim/Art/PostProcessing/HouseVolumeProfile.asset` — URP VolumeProfile (Bloom, Tonemapping, ColorAdjustments, Vignette, optional DepthOfField).
- `Assets/Gamesim/Art/Prototype/Emissive/*.mat` — new HDR-emissive materials for neon outlines (Blue, Yellow, Red), gold competition rings, TV screen, lamp/spot fixtures. Shader `Universal Render Pipeline/Lit` (or `/Unlit`) with `_EmissionColor` set to HDR intensity > 1.
- `Assets/Gamesim/Runtime/House/NeonRoomOutline.cs` (optional helper) — MonoBehaviour that traces a colored emissive border around a room's floor bounds so outlines stay data-driven and aligned.
- Generated hero props (see Step 5) under `Assets/Gamesim/Art/Generated/` if asset generation is chosen.

### Files to modify (additive)
- `Assets/Gamesim/Scenes/HousePrototype.unity` — add Global Volume, add `UniversalAdditionalCameraData` + enable post-processing on `Main Camera`, add fill/accent lights, add neon-outline objects, add competition circle, add props, tune `Sun` and ambient.
- `Assets/Gamesim/Art/Prototype/*.mat` — raise smoothness / add emission on select materials (Ink→TV, Brass→podium, etc.). Shared assets, so re-verify all users via dependency check before editing.
- `Assets/Settings/PC_RPAsset` renderer — add **SSAO** Renderer Feature (Screen Space Ambient Occlusion) if not present.
- `Assets/Gamesim/Editor/HousePrototypeSetup.cs` — mirror the new lighting/material/volume setup into the generator so future regeneration reproduces the improved look (keeps script and scene consistent). Additive only; must not break existing tests.

### Key facts for correct implementation (URP post-processing)
- HDR is already enabled on `PC_RPAsset` (required for Bloom + Tonemapping).
- Camera needs `UniversalAdditionalCameraData.renderPostProcessing = true`; verify the renderer's PostProcessData is not null.
- Global Volume: `isGlobal = true`, assign via `sharedProfile` (not `profile`) in editor code; Volume GameObject layer must be in the camera's `volumeLayerMask`.
- Persisting overrides requires `AssetDatabase.AddObjectToAsset(component, profile)` — each override is a sub-asset; set `overrideState = true` on every parameter written.
- Neon glow = HDR emissive material (emission intensity ~2–6) + Bloom (threshold ~0.9, intensity ~1.2–1.6, scatter ~0.6, high-quality filtering on).

### Design decisions (recommendations baked in; flag if you disagree)
- **Neon outline method:** thin emissive box/quad strips parented under a new `Neon Room Outlines` object, positioned from each room's floor bounds. *(Recommended — precise, controllable, no shader work. Alt: LineRenderer strips, or a fullscreen edge shader — both harder to align.)*
- **Props:** generate a small set of "hero" props (pool/snooker table, potted plants, hanging lamps, accent armchairs, dining table) via Unity Asset Generation using the reference image (InstanceID `14293651197076`) as the style reference, and primitive-build the smaller accents. *(Alt: build everything from primitives — faster, less premium; or generate everything — slower, iterative approvals.)*
- **Where changes live:** apply directly to the saved scene (fast, visible, testable) and mirror core material/lighting/volume into the generator script for consistency. *(Alt: generator-only — invisible until a destructive regenerate; not recommended.)*

## Implementation Steps

> **Progress (resuming after a second disconnect):**
> - ✅ **Steps 1–3 COMPLETE and saved** — Global Volume (Bloom/Tonemapping/ColorAdjustments/Vignette persisted), Main Camera post-processing, Sun 1.3 + 4 accent lights, cool dark ambient RGB(0.14,0.16,0.22), 6 HDR emissive materials, gloss polish, TV Screen.mat on `Television`.
> - ✅ **Step 4 COMPLETE and saved** — `Neon Room Outlines` (20 strips, color-coded) + `Competition Circle` (144 gold ring segments). Visually confirmed.
> - ✅ **Step 5 COMPLETE and saved** — `Decor` root: 4 hanging lamps, 4 potted plants, 3 wall-art frames, dining table + stools, small accents (47 pieces, all collider-free). New mats: Plant Leaf, Pot.
> - ✅ **Verification COMPLETE** — NavMesh intact (364 verts/162 tris), all 5 rooms reachable, PlayMode CalculatePath PathComplete for all rooms, all new geometry collider-free.
> - ⏳ **Step 6 remaining** — mirror the look into `HousePrototypeSetup.cs` (verified: script still has OLD values, unchanged).
> - ⏳ **Final verification remaining** — persistence check (enter/exit Play Mode), console clean.

### Step 1 — Enable post-processing pipeline + Global Volume
- **Description:** Add `UniversalAdditionalCameraData` to `Main Camera` and set `renderPostProcessing = true`. Create `HouseVolumeProfile.asset` and a `Global Volume` GameObject (isGlobal, layer in camera's `volumeLayerMask`). Add & persist overrides: **Bloom** (threshold 0.9, intensity 1.3, scatter 0.6, highQualityFiltering true), **Tonemapping** (Neutral to preserve vibrant colors), **ColorAdjustments** (contrast +12, saturation +12, slight postExposure), **Vignette** (intensity 0.32, smoothness 0.4). Run the skill pre-flight check afterward for zero console errors.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (with Step 2)

### Step 2 — Lighting & atmosphere pass
- **Description:** Darken/cool ambient (`RenderSettings.ambientLight` → deep cool tone, lower intensity) for the TV-set mood; keep `Sun` as warm key but consider lowering to ~1.3 and adjusting angle for stronger directional shadows. Add 2–4 accent lights (point/spot) over competition circle, kitchen island, and living/private areas. Optionally switch camera background to pure dark and/or add subtle fog. Keep realtime lighting (no bake needed at prototype stage).
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (with Step 1)

### Step 3 — Emissive materials + material polish
- **Description:** Create HDR-emissive materials (Neon Blue/Yellow/Red, Gold rings, TV screen, lamp fixtures). Run `Unity.GetDependency` on each shared `Prototype/*.mat` before editing; raise smoothness on floors/furniture (e.g. 0.35–0.6) so surfaces catch light, add emission to `Ink` TV / `Brass` podium accents. Do not change base hues (per approval to keep palette while adding gloss).
- **Assigned role:** developer
- **Dependencies:** None (but visual result validated after Step 1)
- **Parallelizable:** Yes

### Step 4 — Neon room outlines + gold competition circle
- **Description:** Add a `Neon Room Outlines` root; for each room (Living, Kitchen, Bedroom, Private, Yard) build thin emissive border strips aligned to the floor bounds from `HousePrototypeSetup` (e.g. Living floor center `(-7,-0.15,-5)` size `14x10`, etc.), color-coded. Add concentric emissive gold rings (thin cylinders/torus-like rings) centered on the Competition Yard floor `(0,-0.15,15)` to recreate the central circle. Optional `NeonRoomOutline.cs` helper to keep it data-driven.
- **Assigned role:** developer
- **Dependencies:** Step 3 (emissive materials), Step 1 (bloom to make them glow)
- **Parallelizable:** No

### Step 5 — Props & decoration
- **Description:** Add reference-matching props: pool/snooker table, potted plants, hanging lamps (with emissive shades + lights from Step 2), accent armchairs, a colorful dining table, wall-art frames, and a checkered bathroom-style floor accent. Either generate hero props via `Unity.AssetGeneration.GenerateAsset` using reference image InstanceID `14293651197076` (iterative, approval-based) or primitive-build them under `House Architecture` in a new `Decor` child (additive; add colliders only where they must not block existing NavMesh paths). Verify NavMesh still bakes/validates.
- **Assigned role:** developer
- **Dependencies:** Step 3 (materials), Step 2 (lights for lamps)
- **Parallelizable:** Partially (prop building parallel; NavMesh re-verify after)

### Step 6 — Mirror into generator script
- **Description:** Update `HousePrototypeSetup.cs` so a fresh regenerate reproduces the new look: material smoothness/emission, volume + camera post-processing, accent lights, neon outlines, competition circle, decor. Keep strictly additive and preserve all existing object names/bounds so `HousePrototypeTests` and episode tests still pass.
- **Assigned role:** developer
- **Dependencies:** Steps 1–5 (encode the finalized values)
- **Parallelizable:** No

## Verification & Testing
- **Console:** Zero errors/warnings after each step; run the URP post-processing pre-flight snippet (verifies URP active, HDR, camera `renderPostProcessing`, volume layer mask, profile overrides persisted via `AssetDatabase.LoadAllAssetsAtPath`).
- **Visual (Game view, not Scene view):** Capture multi-angle scene view / play the scene and confirm: neon outlines glow, competition circle reads, furniture is glossy, bloom on lamps, overall mood matches the reference. Compare against attached reference image.
- **Persistence:** Enter/exit Play Mode and reopen the scene — confirm volume overrides and emissive materials survive (guards against the missing `AddObjectToAsset` bug).
- **Gameplay integrity:** Run PlayMode tests (`HousePrototypeTests`, `EpisodePlayModeTests`, `EpisodeDiaryRoomPlayModeTests`) — all must still pass (room reachability, NavMesh, labels). Confirm NavMesh still bakes with any added prop colliders.
- **Regeneration parity (Step 6):** On a scratch copy, delete + regenerate via `Gamesim/U02/Create or Register House Prototype` and confirm the improved look is reproduced and tests pass.

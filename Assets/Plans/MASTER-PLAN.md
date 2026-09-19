# Gamesim Big Brother — Master Development Plan

*One plan, replacing eight. Written 2026-09-18 against the code at `ac00ee6` on
`port/game-flow-v2-pass` — 29 commits ahead of `main`, CI green, EditMode 1208/1208, PlayMode
152/152. Every status below was read from the tree, not from the plan it supersedes; where a plan
and the code disagreed, the code won and the disagreement is named.*

**Supersedes:** `asset-pack-integration.md` · `big-brother-house-visual-overhaul.md` ·
`big-brother-ui-ux-overhaul.md` · `camera-navigation.md` · `npc-behaviour.md` ·
`season-completion.md` · `uma-character-pipeline.md` · `web-parity.md` · `port-review-and-aaa-roadmap.md`.
All are recoverable from git at `ac00ee6`. Nothing they decided is dropped here; what they
*planned* is either marked done or carried forward into Part 3.

---

## Part 1 — Where the project actually is

### 1.1 The headline

**The simulation is finished. The presentation is a prototype.** Every system the web reference
runs offline is ported, deterministic, save-versioned and tested — 11,432 lines of pure C# with no
Unity dependency and 23,135 lines of tests behind it, more test than runtime. What sits on top of it
is twelve flat materials, a furniture pack, a synthesised chord for a theme, and a HUD built from
rectangles. The distance between those two halves is the whole of this plan.

### 1.2 Status by area — verified against the code

| Area | Source plan | Its claim | What the code says |
| --- | --- | --- | --- |
| **Simulation parity** | `web-parity.md` | "four of six event systems remain" | **Stale — all done.** Deals (schema 10), recap, minigames, social vocabulary (11), all six event systems, storylines (12), jury voting. |
| **NPC behaviour** | `npc-behaviour.md` | Phases A–D, E open | **A–D done.** E: the one real gap (jury) closed; memory-manager and decision-engine deliberately not ported — their jobs exist here already. |
| **Season flow** | `season-completion.md` | Phase 3 menu open, Phase 4 opening missing | **All four phases done.** Main menu, cast select, character creator, five-beat opening, spectator acknowledgement, season report. |
| **House visual** | `…visual-overhaul.md` | Steps 1–5 complete, 6 remaining | **Half true, and the half that matters is the wrong half.** Steps 1–5 landed in `HousePrototype.unity`. The *shipping* scene `EpisodeHouse.unity` has the neon outlines, the competition circle and 36 fitted lights — and **no Global Volume, no post-processing, no Decor root.** Step 6 (mirror into the generator) never happened. |
| **UI / UX** | `…ui-ux-overhaul.md` | "legacy `Text`, four hard-coded colours, no portraits" | **Stale — Steps 1–5 done.** TMP throughout, `UiTheme` (a static class, not the planned asset), rounded bordered panels, RenderTexture portraits, stings, lower-third, broadcast bug, social graph panel. **Step 6 (motion) not started; Step 7 (layout containers) four uses, not a pass.** |
| **Camera** | `camera-navigation.md` | four phases scoped | **"What it does now" is accurate; Phases 1–4 are all unshipped.** No pitch-to-distance, no cursor zoom, no occlusion cast, no follow indicator, no Tab cycle, no gamepad. Three `.inputactions` assets exist; the rig reads `Mouse.current` directly and uses none of them. |
| **Characters** | `uma-character-pipeline.md` | seam built, four known gaps | **Accurate.** Six authored Quaternius prefabs ship. One controller with `Speed` and `Seated` parameters and four clips — **no seated clip, so `Seated` is a parameter with nothing behind it.** `mood` and `stressLevel` are tracked per houseguest; the player's pair is printed as one line of text and **no houseguest's reaches a body or a face.** UMA behind the local-only define; 16k verts / 229 bones each, unprofiled at sixteen. |
| **Asset packs** | `asset-pack-integration.md` | STYLARTS to become the base look; Phase 1 seam "not blocked" | **Phase 1 not done — no `HouseCatalogue` exists; `HouseSetPieces` hard-codes 132 Kenney entries.** No pack was ever downloaded; `Assets/ThirdParty/` does not exist. **Part 4 replaces this plan's premise.** |
| **Audio** | — | — | **Zero recorded audio files.** Beds are chords synthesised in `HouseAudio.BuildBed()`. Cues, buses and the `Resources/Audio/Theme`/`Season` swap are wired and waiting. |
| **Verification** | `ACCEPTANCE_MATRIX.md` | — | A: passing. B: passing. C: thresholds derived windowed, V7 measured batchmode — **stale.** D: passed, D2 partial. **E: 0/5.** `PortVerification.Season` walks 34 clicks and **predates every system in row one.** |
| **CI** | — | — | Invariants only (UMA define, metas, GUIDs). The 1,360 tests run on a local D: mirror by `Tools/sync-and-run.sh`. |

### 1.3 What was deliberately not ported — decisions that stand

| Not ported | Why | Revisit if |
| --- | --- | --- |
| AI generation routes (events, storylines, NPC decisions) | Content from a network call cannot replay from a seed; the suite rests on replay. Every one has a deterministic fallback, and the fallback is what shipped. | An on-device model is adopted **and** its output travels inside the recorded command (§3.F). |
| Physical / Crapshoot minigames | `CompetitionCategory` never produces them; widening the rotation re-rolls every seeded season. | A rules-versioned rotation change is decided on, once. |
| Counter-offers, deal pending-queue, alliance stability, `_voteTracking` | Each is a store the save format has no room for, serving a number nothing reads. | The consumer arrives first. |
| `memory-manager`, `npc-decision-engine` | `RelationshipLedger`, `ThreatAssessment`, `WebEvictionVoting` already answer those questions. | Never, on current evidence. |
| Accounts, sign-in, cloud leaderboard, "unranked" notice | Serve a hosted multi-user app; a desktop build has no accounts. | Store platform work (§3.H) — and then as Steam, not as accounts. |
| Nine of seventeen traits with no source repertoire | Mapping them is authoring a personality system, not porting one. `TraitsThisProjectHasAndTheSourceDoesNotFallToTheDefault` pins it. | Somebody decides to author it. |

### 1.4 The pattern to look for first

The expensive-looking systems kept turning out to be **present and unwired.** The eviction vote had
weighed deals for months and never seen one. `WebRules` scored five competition categories the
player experienced identically. `boughtActionPoints` was incremented by a control whose budget
ignored it. `Seated` is an animator parameter with no clip. `mood` is a field with no face. Before
building anything in Part 3, check whether the consumer already exists.

---

## Part 2 — The constraints that govern every workstream

These are not preferences. Each one has broken the build or the suite when ignored, and each has a
test or a memory file behind it.

1. **Determinism.** Every simulation draw goes through `EpisodeState.randomState` via `Roll(s)`, and
   every draw happens *after* the guards that might skip it — a roll spent in an argument re-rolls
   the season even when nothing uses the number. Presentation-only randomness (minigame boards,
   target placement) uses a generator the run owns. `npcSocial.randomState` is advanced by
   real-time ticks and is unusable for anything replayed.
2. **The save contract.** `SaveJson.CheckDtoShape` matches stored objects field for field. **One
   new persisted field is one schema version**: a frozen validator for the previous shape, a
   migration, and a sweep of every fixture. Batch fields. Twelve versions exist; the next is 13.
3. **Rules versioning.** `blocRulesStartWeek`, `npcSocial.rulesStartWeek`,
   `socialBudgetRulesStartWeek`, `dealRulesStartWeek`, `eventRulesStartWeek`,
   `storyRulesStartWeek`. A rule that changes behaviour mid-season gets a boundary; a fixture
   that must stay quiet sets the boundary past its last week.
4. **The web build is the reference.** Numbers are the source's numbers. Where this port diverges
   it says so in the code and the reason is written down. Where the source's own shape looks like a
   bug (the memory-match crossover, the deal ladder's information rung), it is pinned, not fixed.
5. **Captions are a contract.** Tests and screen readers find controls by the words on them.
   Decorate beside a caption (tags, chips, portraits); never append to it. The `const string`
   captions in `EpisodeHud` are a public API.
6. **Knowledge boundaries.** Nothing reaches the player that their character does not know. NPC
   private traffic goes through `RelationshipLedger.Move`, never `EpisodeEngine.Change`, because
   `Change` feeds the player's `relationshipArcs`.
7. **Scene changes are additive.** PlayMode tests pin object names, floor and wall collider bounds,
   and NavMesh reachability. Nothing that moves a collider ships without the navigation audit
   passing, and the committed NavMesh asset is not re-baked casually — a fresh bake once
   disconnected the yard.
8. **Screens parent to the director; cards go on a scene root.** A screen with a control on it
   must be findable under `director`; a card carrying a houseguest's name must not be.
9. **Never commit `GAMESIM_UMA`.** `ProjectSettings.asset` must stay
   `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`. A clone with the define and no UMA cannot
   repair itself.
10. **Purchased packs are never committed.** The repository is public; the Asset Store EULA
    forbids it. Anything under `Assets/ThirdParty/` is gitignored and the project must run
    without it. *Blender-authored assets are ours and are the exception this plan is built on.*

Verification for every workstream: offline compile via `Tools/offline-compile.ps1` (and
`Tools/player-compile.ps1`, which catches editor-only API leaking into runtime code), then both
suites via `Tools/sync-and-run.sh`; `Tools/README.md` lists the traps. **Baseline to beat:
EditMode 1208, PlayMode 152.** Read the console's `Types: ["All"]`, not the DLL timestamps.

---

## Part 3 — The workstreams

Ordered by how far each moves the build toward a shipped look. Each carries the steps absorbed
from its source plan, with status.

### 3.0 — Finish the port *(days)*

Standing between "every item delivered" and "done."

| Step | Status | Note |
| --- | --- | --- |
| Merge PR #1 to `main` | **open** | Nothing blocks it. `main` has none of Part 1. |
| Extend `PortVerification.Season` to deals, events, storylines, minigames | **open** | The built player is verified only against the season it used to have. |
| Run Section E, once | **open** | Three fresh participants, one timed episode each, one losing run to its end. `PLAYTEST_PROTOCOL.md` is how. |
| Re-measure C1–C5 in a window, on named hardware, at sixteen | **open** | The thresholds are windowed; the last measurement was not. |
| Tier 5 writing pass — triple every template set | **open** | 112 dialogue strings, 6 events, 3 storylines, 6 jury reasons. A player sees repetition inside two seasons. No engineering. |
| D2 — one test that walks every action by keyboard | **open** | Every panel added this month has controls that test has not visited. |
| Carry the post-processing volume into `EpisodeHouse.unity` | **done 2026-09-19** | `Gamesim/U07/Carry the volume and decor to the episode house` — volume, camera post-processing flag, and the 46-object `Decor` subtree the first pass could not carry. |
| Housekeeping: `SampleScene` out of the build list; decide `HousePrototype`; delete the 42 MB unreferenced `QuaterniusBaseCharacters`; extract panels from the 2,230-line director | **partly done** | Build list is Bootstrap, HousePrototype, EpisodeHouse. `HousePrototype` stays: three PlayMode suites load it by name and it is the authored source the episode scene is dressed from. Deleting `Assets/Scenes/` and `QuaterniusBaseCharacters/` (43 MB, no GUID referenced outside the folder) awaits a human `git rm`. Director extraction open. |
| Repoint `README.md`, `COMMIT_NOTES.md`, `UNITY_PORT_ROADMAP.md` at this plan | **done with this document** | The root roadmap's "completion boundary" paragraph is superseded by §1.2. |

### 3.A — Environment art *(the largest visible gap; Part 4 is the how)*

**Absorbs:** the visual overhaul's six steps, the asset-pack plan's Phases 1–5.

| Step | Status |
| --- | --- |
| Post-processing volume, bloom, tonemapping, colour adjustments, vignette | Done in both (carried 2026-09-19; the episode camera had never had post-processing enabled, so the profile alone would have changed nothing) |
| Lighting and atmosphere pass | Done in both (36 fitted lights in the shipping scene) |
| Emissive materials, gloss | Done |
| Neon room outlines, gold competition circle | Done in both |
| Props and decoration (`Decor` subtree, 46 objects) | Done in both (carried 2026-09-19; the first pass had copied only the lamp shades and plants) |
| Mirror into `HousePrototypeSetup.cs` | **Resolved differently.** `HousePrototype.unity` is the hand-authored source of the art; the shipping scene is *derived* from it by the two `EpisodeHouseDressing` passes, which are re-runnable. `HousePrototypeSetup` regenerates only the shell, and regenerating it would discard the authored art — so it must not be re-run, and the plan no longer asks it to carry values it never produced. |
| SSAO renderer feature | Present in `PC_Renderer.asset` |
| Catalogue seam (`HouseCatalogue`, logical prop ids, resolution audit) | **Not done** — 132 hard-coded Kenney ids |
| Big Brother set pieces (pool, hot tub, long table, diary chair, have-not room) | **Not done** — and no pack ships them |
| STYLARTS swap | **Cancelled by Part 4.** Never downloaded; cannot be committed; authored for full-height rooms against 1.5 m cutaway walls — the plan's own first risk. |

**Remaining, in order:** build the catalogue
seam → replace the shell and set pieces with Blender-authored assets (Part 4) → per-room prop
replacement → clutter pass → mirror the final state into the generator.

**Watch:** the accessibility captures render with `camera.Render()` into a texture, which skips the
post-processing pass. Any bloom claim must be re-verified against a presented frame or the
contrast results in D6b stop being true.

### 3.B — Characters and animation *(the second-largest gap; Part 4 §4.5 is the how)*

**Absorbs:** the UMA pipeline plan and its four known gaps.

| Step | Status |
| --- | --- |
| Three-state body fallback (UMA → authored prefab → primitive rig) | Done, tested |
| `GAMESIM_UMA` presence bootstrap | Done, and the rule in Part 2 §9 |
| Stylised proportions, flattened shading, skin tones | Done |
| **Seated pose** | **Missing** — the `Seated` parameter exists and is driven; there is no clip |
| **Conversation blocking** — two people facing each other, talk/listen loops | **Missing** |
| **Reactions** — nominated, saved, evicted, won | **Missing** |
| **Faces from `mood` × `stressLevel`** | **Missing** — five moods and five stress levels exist per houseguest; the only reader is a text line about the player (`EpisodeDirector.cs:1384`) |
| Show-specific wardrobe | **Missing** — "Blender-authored content is still the plan" |
| UMA at a full house of sixteen | **Unprofiled** |

**Decide once: UMA or bespoke.** UMA gives variety cheaply and costs per frame; bespoke rigs give a
look and cost an artist. The animation set is needed either way and is the part that reads loudest.
Part 4 §4.5 covers both routes.

### 3.C — Audio *(wiring done; content absent)*

- A composed theme and a season bed, dropped into `Resources/Audio/Theme` and `Resources/Audio/Season`
  — the swap already exists.
- An SFX set keyed to `HouseAudio.Cue`: button, competition start/win, nomination, veto, eviction,
  finale. Room tone per room. UI foley.
- A mixer with buses, and a "reduced audio" preference beside reduced motion.
- A bark set for ceremonies covers most of the value of VO at a fraction of the cost.

### 3.D — UI and UX

**Absorbs:** the UI/UX overhaul's seven steps.

| Step | Status |
| --- | --- |
| 1 TextMeshPro | Done |
| 2 Theme, rounded bordered panels | Done — `UiTheme` is a static class rather than the planned ScriptableObject; adequate, and changing it now is churn |
| 3 RenderTexture portraits | Done |
| 4 Broadcast framing, stings | Done |
| 5 Relationships at a glance (`SocialGraphPanel`, trust chips) | Done |
| **6 Motion and feedback** | **Not started** — panels snap; gate everything on reduced motion |
| **7 Layout containers** | **Partial** — four `LayoutGroup` uses; the fixed chrome is still absolute-positioned |
| Controller and Steam Deck navigation | Not started — D2 is the test that will demand it |
| **Localisation** | Not started — no package, no tables, every caption a C# string. Needs a key layer that preserves the caption contract. |

**Do the idiom change before adding more panels.** Every panel built this month (deals, events,
storylines, minigames, recap) was built to the current idiom. A new idiom re-does them.

### 3.E — Cinematics and camera

**Absorbs:** the camera plan's four phases.

| Step | Status |
| --- | --- |
| Wheel zoom, click-to-frame, follow, orbit, pan, `F` | Done |
| **Phase 1** — pitch tied to distance, zoom toward cursor, occlusion pull-in | **Not started** |
| **Phase 2** — cast-rail focus, follow ring and chip, Tab to cycle | **Not started** |
| **Phase 3** — middle-drag, edge pan, gamepad, **move to an Input Actions asset** | **Not started** — `Assets/InputSystem_Actions.inputactions` exists and the rig does not use it; this is what makes the camera testable |
| **Phase 4** — duration easing, close-range name tags, ceremony framing presets | **Not started** |
| Cinemachine for conversation and ceremony framing | Not installed |
| Timeline for the ceremonies and the opening's five beats | Installed, unused |

**Watch:** reduced motion must remain total (D4). Follow-mode made an explicit decision about it;
everything added here makes the same decision deliberately. A Timeline that cannot collapse to
its final frame breaks a guarantee that currently passes.

### 3.F — Narrative and content

- **A writing pass first.** Every template set is the reference's offline minimum. Triple them.
  Days per set, no engineering, the cheapest large gain in this document.
- **Generated content without breaking replay, if wanted.** A generated situation is safe *if its
  text and choices travel inside the recorded command that introduced it.* Events and storylines
  already persist their choices for exactly this reason; the remaining step is to carry generated
  content in the `Advance` payload rather than deriving it inside the commit. `SENTIS_ANALYTICS_ENABLED`
  exists; Sentis is not installed. This is a project, not a task.

### 3.G — Systems depth *(only after the slice)*

Contextual-action generator (37 KB) · veto lobbying as a phase (22 KB) · alliance stability ·
the two unreachable minigames behind a rules-versioned rotation · deal counter-offers. Worth doing
once players are asking for more; they are not yet asking.

### 3.H — Platform and production

Windows 64 only today. Addressables for the art Part 4 produces (the prototype loads everything from
the scene; a real house will not fit that way) · Mac and Linux targets · quality tiers, resolution,
framerate cap · Steam: achievements over the existing event kinds, cloud saves over a save system that
already validates and rotates · a licensed Unity CI runner so the 1,360 tests run on every push.

### 3.I — The quality bar

Section E once, then every milestone. Windowed performance budgets on named hardware. Memory
ceilings. Load-time targets. Crash-free sessions measured in real sessions.

---

## Part 4 — The Blender pipeline

This is the part that changes the asset strategy, so it gets its own reasoning before its own steps.

### 4.1 Why Blender, and what it replaces

The asset-pack plan chose STYLARTS Stylized House Interior as the base look. That choice had three
problems it named itself and one it did not:

1. **It cannot be committed.** Public repository, Asset Store EULA. Every clone would open to
   Kenney anyway.
2. **It is authored for full-height rooms.** This house is a doll's-house cutaway: walls at
   **1.5 m**, the south wing at **1.1 m**, for an overhead camera. The plan expected "a large
   fraction of it to be unusable for that reason alone."
3. **It was never downloaded.** 2.1 GB, and the pipeline (URP or not) unverified.
4. **It is somebody else's look.** A stylised-PBR interior pack is what a hundred other projects
   look like. The visual overhaul's target — dark TV set, neon outlines, gold circle, glossy
   saturated furniture — is not a look any pack ships.

Blender-authored assets answer all four. They are ours, so they are committed and every clone gets
them. They are built *to* the cutaway, not capped to fit it. They are sized to this house's metre
scale from the first vertex. And they are the show's look rather than a catalogue's.

**Kenney stays as the bottom tier of the fallback chain.** That part of the old plan was right for
a reason that survives: the catalogue seam means a prop that is not yet authored still resolves to
something, and the house is never half-furnished during the transition.

### 4.2 Setup

- **Blender 5.2.2 LTS**, installed 2026-09-18 at `C:\Program Files\Blender Foundation\Blender 5.2\`.
  Nothing here needs a newer feature; staying on an LTS means the files open in two years.
- **Claude drives it through the MCP.** `mcp-for-blender` is installed and its add-on auto-starts
  a socket server on port 9876 whenever Blender is open; sessions started after 2026-09-18 have
  the tools (`execute_blender_code`, `get_viewport_screenshot`, `export_scene`, Poly Haven search
  and download). That makes every step below scriptable: the export checklist runs through
  `execute_blender_code`, a viewport capture is the review, and Poly Haven's CC0 textures and
  HDRIs are the one external source that *can* be committed. Telemetry is off on both sides.
  Paths and the `user_prompt` quirk are in `Tools/README.md`.
- **Sources live outside `Assets/`.** `ArtSource/` at the repository root, tracked with **Git LFS**
  (`*.blend`, `*.psd`, `*.kra`). Unity will try to import a `.blend` placed under `Assets/` by
  invoking Blender through the importer, which is slow, version-fragile, and fails on any machine
  without Blender — a clone must not depend on Blender being installed. Git LFS 3.7.1 is
  installed; GitHub's free LFS tier on a public repository is 1 GB of storage and 1 GB of
  bandwidth a month, so `git lfs track 'ArtSource/**/*.blend'` when the first source lands, keep
  sources lean (textures beside the `.blend`, never packed into it), and if the quota bites,
  sources move to a private art repository and only the exports stay here.
- **Exports live under `Assets/Gamesim/Art/Authored/<Category>/`** — `Shell/`, `SetPieces/`,
  `Furniture/<Room>/`, `Clutter/`, `Characters/`, `Wardrobe/`, `Animation/`. Each export is an
  FBX plus its textures; each folder gets a `.meta` like everything else.
- **One `.blend` per asset family, one collection per asset.** `bb_kitchen.blend` holds the
  kitchen's props as collections; the exporter walks collections. A single monolithic scene file
  becomes unmergeable the first time two people touch it.

### 4.3 Conventions — non-negotiable, because Unity import is where days are lost

| Convention | Rule | Why |
| --- | --- | --- |
| **Units** | Blender scene units = metres, unit scale 1.0. **1 Blender unit = 1 Unity unit = 1 m.** | The house is 28 × 20 m with 1.5 m walls, built in metres by `HousePrototypeSetup`. Anything else needs an import scale, and import scales are where "why is the chair 100× too big" lives. |
| **Axes** | Model with **−Y forward, Z up** in Blender. Export FBX with **Forward −Z, Up Y**, *Apply Transform* on. | Unity is Y-up, left-handed. This pairing is the one that arrives unrotated. |
| **Transforms** | Apply all (Ctrl-A → All Transforms) before export. Scale 1,1,1; rotation 0,0,0. | A rotated object with an unapplied rotation imports facing the wrong way and *looks* right in Blender. |
| **Origin** | At the floor contact point, centred in X/Y. Wall-mounted pieces: origin at the wall contact. | `HouseSetPieces.Plan` places props by origin. A prop with its origin at its centre floats half its height off the floor. |
| **Naming** | `bb_<category>_<name>[_<variant>]` — `bb_kitchen_stove`, `bb_set_diaryChair`, `bb_shell_wall_south`. Mesh, object and file share the name. | The catalogue resolves by logical id; the audit reports by name. Spaces and capitals break both. |
| **Topology** | Quads while modelling; triangulate on export (or let Unity, deterministically). No n-gons in the export. Hard edges by *auto smooth / smooth by angle*, not by split geometry. | N-gons triangulate differently per importer; split geometry doubles verts. |
| **Scale of detail** | The camera sits 4–34 m away, pitched 55°. Nothing smaller than ~2 cm reads. Model to that, not to a close-up. | Triangles spent on a drawer handle nobody can see are triangles the sixteen-body house pays for every frame. |
| **LODs** | `_lod0` / `_lod1` / `_lod2` suffixes on child meshes; Unity's importer builds an `LODGroup` from that naming automatically. Props: 2 levels. Shell: 1. Characters: 3. | The full house is ~175 props plus sixteen bodies. |
| **Materials** | Principled BSDF only, one material per texture set, named `bb_mat_<name>`. No procedural nodes that do not bake. | Only what bakes survives export. |
| **Textures** | Baked to power-of-two, 1024 for props, 2048 for hero pieces and characters. **URP/Lit packing:** Base Map (RGB albedo, A alpha) · Metallic map (**R metallic, A smoothness** — bake roughness and invert into the alpha) · Normal map (**OpenGL, +Y**, which is Blender's default) · Occlusion in its own map's G. Emission where the material glows. | URP/Lit reads exactly these channels. A Unity-side "convert roughness" step is a step that gets forgotten. |
| **Colliders** | Export a separate simplified mesh named `bb_<name>_col` for anything a houseguest must path around. **Do not add colliders to furniture that has none today.** | The NavMesh bakes from colliders; the furniture is collider-free by design because carving it broke seventeen tests (memory: *furniture is collider-free and carving it breaks NPCs*). Colliders are for the shell and the set pieces the navigation audit already accounts for. |

An export checklist as a Blender Python script — apply transforms, check origin at Z=0, check name
prefix, check no n-gons, export with the fixed FBX settings — is worth writing in the first week
and running on every export. It is the difference between a pipeline and a habit. Run it through
the MCP's `execute_blender_code` and a review needs nobody at the keyboard.

### 4.4 What to build, in order

Priority is by how much each piece changes the read of the shipping scene per hour spent.

**Tier 1 — the Big Brother set pieces** *(none exist in any pack; the old plan's Phase 2)*

| Piece | Where | What makes it the show |
| --- | --- | --- |
| **Pool and hot tub** | competition yard | The most recognisable thing about the backyard. Basin, coping, water plane (a shader, not geometry), jets, a seating ring on the tub, loungers around both. |
| **Long dining table + sixteen chairs** | kitchen | Replaces the round table. One chair per houseguest — the house does most of its arguing here. Sixteen because the cast can be sixteen. |
| **Diary room hero chair** | private room | Lit separately; the one piece the camera sees in close-up during reflections. 2048 textures. |
| **Memory wall** | living room | Sixteen portrait frames in a grid; the frames are geometry, the portraits are the existing RenderTextures — `MemoryWall` already refreshes them. |
| **HoH room** | HoH floor | The bed, the door with the key, the basket. The reward room should look like one. |
| **Competition yard podiums and rings** | yard | The gold rings exist as 144 primitive segments; replace with one authored ring mesh and podiums that match. |
| **Have-not room** | game room south | Cold palette, harsh surfaces. Set dressing only — the simulation has no have-not mechanic and this plan does not add one. |

**Tier 2 — the shell.** Walls, floors, wall caps, door frames, skirting, the cutaway edge itself
as an authored trim. **The collider bounds do not move.** The authored shell is visual geometry
placed over the existing box colliders, or replaces them with `_col` meshes of identical bounds,
and the navigation audit (`Gamesim/Audit house navigation`) passes before it is committed. The
28 × 20 footprint, the 1.5 m / 1.1 m heights, and every doorway are fixed inputs.

**Tier 3 — per-room furniture,** replacing the 132 Kenney entries by room: kitchen, living,
bedroom, bathroom, games, nomination. Each room is a `.blend`; each prop a collection; each export
resolves through the catalogue ahead of the Kenney fallback. A room is "done" when the resolution
audit shows no Kenney id in it.

**Tier 4 — clutter.** Mugs, books, cables, bottles, towels. The old plan was right that this is
what stops a dressed room reading as a greybox, and right that it is cheap. Instance-friendly:
one mesh, many placements, `GPU Resident Drawer` is already on in the PC renderer.

### 4.5 Characters — the two routes

**Route A — UMA stays, Blender supplies what UMA lacks.** The UMA plan said this is the plan for
show-specific clothing, and the pipeline for it is UMA's own: model the garment in Blender on
UMA's base mesh, export, create a *slot* (mesh) and *overlay* (texture) through UMA's tooling, add
a *wardrobe recipe*. `UmaBodyTint` then tints it like any other recipe. This route keeps every
existing test and the variety UMA gives for free. Cost: per-frame, unprofiled at sixteen — profile
first (§3.0).

**Route B — bespoke cast, replacing the Quaternius prefabs.** Model in Blender at the stylised
proportions the UMA plan measured (larger head, shorter body, ~1.72 m); rig with **Rigify** and
export as a Humanoid-compatible skeleton (Unity's Humanoid mapping handles Rigify's deform bones
when *Deform bones only* is set on export). Six to sixteen bodies at ~6–8k verts each with three
LODs is a fraction of UMA's cost. Cost: an artist's weeks, and the variety is only what is
authored.

**Either route needs the same animation set, and this is the gap that reads loudest:**

| Clip | Drives | Note |
| --- | --- | --- |
| `sit_idle`, `sit_to_stand`, `stand_to_sit` | the existing `Seated` parameter | Today a seated houseguest stands in a chair. This is the single most visible defect in the house and it is *one clip and two transitions.* |
| `talk_loop`, `listen_loop`, `nod`, `shrug`, `arms_crossed` | `SetTalking` and conversation state | Two people facing each other and taking turns is what a conversation looks like. |
| `react_nominated`, `react_saved`, `react_evicted`, `react_won` | the ceremony beats the stings already fire on | One-shots, additive over the idle. |
| `walk`, `idle` variants × 3 | `Speed` | Sixteen identical idles read as clones. |

Export as **Humanoid** clips (FBX per clip or one FBX with actions as takes), *Loop Pose* on the
loops, *Root Transform* baked, and assign them in `GamesimCharacter.controller` — which already
declares `Speed` and `Seated` and has empty states waiting.

**Faces.** `mood` (five values) and `stressLevel` (five values) are simulated per houseguest and
reach the screen only as a text line about the player. In Blender: **shape keys** — five mood targets and a stress target on the
head mesh, exported as blend shapes. In Unity: a small layer in `CharacterPresentation` that lerps
`SkinnedMeshRenderer` blend-shape weights from the two fields. That wires a system that has
never reached a body to the one place a player would see it. Route A: UMA supports blend
shapes on its base races; Route B: they come with the mesh.

### 4.6 Bringing it into Unity

- **FBX import settings, fixed per folder** via an `AssetPostprocessor` in `Gamesim.Editor` so
  nobody sets them by hand: *Scale Factor 1, Convert Units on, Bake Axis Conversion on,
  Read/Write off, Generate Colliders off, Materials: Extract to `<folder>/Materials`.* Characters:
  *Rig Humanoid*; props: *Rig None*. Animation FBX: *Humanoid, Loop Time on for `_loop` names.*
- **Materials on import** map to URP/Lit with the packed textures from §4.3; the postprocessor
  assigns them by the `bb_mat_` name so a re-export does not lose the assignment.
- **The catalogue seam** (`HouseCatalogue`) is where authored assets enter the scene: a logical id
  resolves to `Art/Authored/…` first and `Art/External/KenneyFurniture/…` second. `HouseSetPieces.Plan`
  moves from file names to logical ids in the same change. The **resolution audit** menu item
  reports which tier each prop resolved to — "is this still Kenney?" becomes a list.
- **Mirror the final state into `HousePrototypeSetup.cs`** (the visual overhaul's Step 6, still
  open) so a regenerated house is the authored house.

### 4.7 Verifying authored assets

Every export passes, in order:

1. **The export checklist script** (§4.3) — origin, transforms, names, topology.
2. **Import warnings** — zero. A warning about scale, axis or missing textures is a failed export.
3. **`Gamesim/Audit house navigation`** — every room reachable, no path through geometry, the
   committed NavMesh untouched unless the shell changed and the rebake guard is satisfied.
4. **PlayMode `HousePrototypeTests` and `EpisodePlayModeTests`** — object names and bounds
   unchanged, every station reachable.
5. **The resolution audit** — the prop resolved to the authored tier.
6. **A presented-frame capture** at 1280 × 720, 1600 × 900 and 2560 × 1440 — not
   `camera.Render()`, because that skips post-processing — reviewed against the dark-set target
   look and checked for D6b contrast.
7. **Build size and frame time** — measured after each tier, against the C1–C3 thresholds,
   windowed. Stylised PBR carries heavier textures than Kenney; the 871 MB player is the
   starting figure.

### 4.8 Production order for Part 4

1. Export checklist script, the `AssetPostprocessor`, and `git lfs track` for `ArtSource/` — one
   day, before any asset. *(The MCP that makes the checklist scriptable landed 2026-09-18.)*
2. **The seated clip** — one afternoon; the most visible defect in the house, fixed.
3. **The catalogue seam** — so every later asset has somewhere to land.
4. Tier 1 set pieces, pool first.
5. Tier 2 shell.
6. Tier 3 furniture, kitchen first (it has the long table).
7. Conversation and reaction clips; faces.
8. Tier 4 clutter.
9. Wardrobe (Route A) or the cast (Route B), whichever §3.B decided.

---

## Part 5 — Sequencing

The plan that got the simulation finished held one principle: *agency before decoration.* It now
points the other way. The agency exists; the next unit of player-visible quality is presentation,
and the Blender pipeline is the art track of the vertical slice.

| Phase | Weeks | Deliverable | Gate |
| --- | --- | --- | --- |
| **0 — Finish** | 1 | §3.0 complete: merged, verified, housekept, E run once, volume in the shipping scene | Section E has a first result; C1–C5 measured windowed |
| **1 — Vertical slice** | 5–7 | **One week of one season at final quality.** Part 4 tiers 1–2 and the seated clip (A, B); theme, bed and cue SFX (C); UI motion and layout (D); ceremonies on Timeline with Cinemachine framing (E) | *Does one week look like a shipped game?* If yes, the rest is production. If no, the answer is in 3.A or 3.B and nothing below fixes it. |
| **2 — Content** | 3–4 | Tier 5 writing pass, every template set tripled; Part 4 tier 3 furniture; conversation and reaction clips; faces | No repetition inside a season; every room off Kenney |
| **3 — Platform** | 2–3 | Addressables, three desktop targets, settings, Steam, localisation keys | A stranger can install and play it |
| **4 — Depth** | ongoing | §3.G; generated content if adopted; wardrobe or cast; clutter | Players are asking for more |

**What not to spend on:** more simulation before the slice; multiplayer, accounts or cloud; a new
HUD idiom before finishing the seven steps; the STYLARTS download.

---

## Part 6 — Definition of done, per increment

An increment is done when all of the following hold, and *only* then is it merged:

- Offline compile clean; **EditMode ≥ 1208 and PlayMode ≥ 152 passing**, run via
  `Tools/sync-and-run.sh`, results read from the XML.
- Every new asset has a `.meta`; no duplicate GUIDs; `GAMESIM_UMA` not in `ProjectSettings.asset`
  — the CI invariants job is green.
- Any new persisted field arrived as a schema version with a frozen validator and a migration test
  that validates the *chain*, not the step.
- Any scene change passed the navigation audit; the committed NavMesh is unchanged or the rebake
  guard was satisfied.
- Any new control has a caption constant and is reachable by keyboard.
- Any new animation or transition collapses under reduced motion.
- The relevant rows of `ACCEPTANCE_MATRIX.md` were re-run, not assumed.

---

## Appendix — What each superseded plan contributed here

| Plan | Absorbed into |
| --- | --- |
| `web-parity.md` | §1.2 row 1, §1.3, Part 2 §1–4, §3.F, §3.G |
| `npc-behaviour.md` | §1.2 row 2, §1.3, Part 2 §1 and §6 |
| `season-completion.md` | §1.2 row 3, §1.3 |
| `big-brother-house-visual-overhaul.md` | §3.A, §4.6, Part 2 §7 |
| `big-brother-ui-ux-overhaul.md` | §3.D, Part 2 §5 |
| `camera-navigation.md` | §3.E |
| `uma-character-pipeline.md` | §3.B, §4.5, Part 2 §9 |
| `asset-pack-integration.md` | §4.1 (its premise replaced, its seam and set-pieces kept), Part 2 §10 |
| `port-review-and-aaa-roadmap.md` | Part 3's structure, Part 5 |

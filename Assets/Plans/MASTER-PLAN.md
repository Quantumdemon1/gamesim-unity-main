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
| **Camera** | `camera-navigation.md` | four phases scoped | **Phase 1 shipped 2026-09-19** (pitch follows the distance with the authored pitch as the orbit's offset, wheel zooms toward the cursor, a boom occluder beyond the zoom minimum pulls the camera in). Phases 2–4 unshipped: no follow indicator, no Tab cycle, no gamepad. Three `.inputactions` assets exist; the rig reads `Mouse.current` directly and uses none of them. |
| **Characters** | `uma-character-pipeline.md` | seam built, four known gaps | **Half right.** Six authored Quaternius prefabs ship. The controller had `SitDown` and `StandUp` clips all along — what was missing was anything in the house that ever *set* `Seated`, and every clip, `Idle` and `Walk` included, was imported non-looping, so a walk froze after one cycle. **Both fixed 2026-09-19:** two seated venues (the long table, the loungers) and a facing per slot; `Idle`/`Walk` loop; and by the end of the day the rig had its own authored takes — seated idle, seated and standing talk, listen, four reactions — wired into the controller. `mood` and `stressLevel` are tracked per houseguest; the player's pair is printed as one line of text and **no houseguest's reaches a body or a face.** UMA behind the local-only define; 16k verts / 229 bones each, unprofiled at sixteen. |
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
| Extend `PortVerification.Season` to deals, events, storylines, minigames | **written 2026-09-19; standalone walk passed (89 commands, a winner)** — two gates fixed on the way (a refused proposal records no deal; the ballot is offered only once the night reaches the voting stage); the full season walk is the next build's job | `PortVerification.Season.Systems.cs`: the weekly recap (which would have stranded the old walk after the first eviction), pending house events, one played minigame, an optional deal, and the season's storyline/event/deal counts in the report. The first standalone run showed `-nographics` cannot host the portrait RenderTextures — `Tools/build-and-verify.sh` runs `-batchmode` alone now — and then failed at its own first save/reload check, which now reports what it saw. **That failure was real:** a houseguest body cloned for a season larger than the authored five copied its template's motion owner and agent, and the coordinator refused every such clone its rebind — `FitHousematesToCast` strips them now, `NpcRuntime_ASeasonSeatedBeyondTheSixthSlotBindsAndSurvivesAReload` pins it, and the standalone season is to be rerun. |
| Run Section E, once | **open** | Three fresh participants, one timed episode each, one losing run to its end. `PLAYTEST_PROTOCOL.md` is how. |
| Re-measure C1–C5 in a window, on named hardware, at sixteen | **measured at twelve 2026-09-19 on the full authored house, uncapped: median 3.20 ms, p95 4.32, p99 6.28 — above the proposed thresholds, recorded in the matrix** | A roster seats twelve and the builder will not pad a season with the other roster, so sixteen is a save's bound, not a cast; the verifier now clamps and says so. Windowed 1600×900 on the reference machine: median 3.24 ms, p95 4.45 ms, p99 6.32 ms over 300 s and 82,928 frames. The 2 / 3 / 4 ms thresholds came from the primitive prototype; the shell, 139 authored props and twelve bodies cost about 2.5× that. Next: a clean re-run after the verifier fix, then either a profile of where the frame goes or thresholds that name this scene |
| Tier 5 writing pass — triple every template set | **done 2026-09-19** | 17 dialogue blocks × 6 voices × 3 variants, 6 situations × 3 narratives, 3 storyline chapters × 3, 6 jury reasons × 3. Wording is chosen by week (jury: by seat), never by a roll, so a seeded season plays out identically however it is phrased — `WritingPassTests` pins that. Labels and captions untouched. |
| D2 — one test that walks every action by keyboard | **done 2026-09-19** | `Accessibility_EveryPanelIsWalkableAndCommittableByKeyboard`: a whole season plus notebook, settings, conversation, recap and report, committed only by `Submit`. It found three real defects on its first runs — the recap and report never took focus, an auto-hidden scrollbar sat in the ring where Down and Tab both refuse to land, and the creator's name field rebuilt the form from its own teardown — all fixed in `EpisodeHud`, `EpisodeDirector` and `CharacterCreator`. |
| Carry the post-processing volume into `EpisodeHouse.unity` | **done 2026-09-19** | `Gamesim/U07/Carry the volume and decor to the episode house` — volume, camera post-processing flag, and the 46-object `Decor` subtree the first pass could not carry. |
| Housekeeping: `SampleScene` out of the build list; decide `HousePrototype`; delete the 42 MB unreferenced `QuaterniusBaseCharacters`; extract panels from the 2,230-line director | **done but for the two deletions** | Build list is Bootstrap, HousePrototype, EpisodeHouse. `HousePrototype` stays: three PlayMode suites load it by name and it is the authored source the episode scene is dressed from. Deleting `Assets/Scenes/` and `QuaterniusBaseCharacters/` (43 MB, no GUID referenced outside the folder) awaits a human `git rm`. **Director extracted 2026-09-19**: seven topic partials (Challenge, Ceremony, Journal, Conversation, Season, Camera, Opening) carry 1,396 lines verbatim; the main file is 953 lines of configuration, Update, the command path, Render and the lifecycle. |
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
| Catalogue seam (`HouseCatalogue`, logical prop ids, resolution audit) | **Done 2026-09-19** — `HouseCatalogue.Resolve` (authored `bb_` ids first, registered replacements, then the Kenney kit), both furnishing passes route through it, `Gamesim/U07/Audit set piece resolution` lists every id by tier: 51 ids, 3 authored, 48 Kenney, 0 missing at the seam; **56 ids, all authored, 0 missing after Tier 3's first pass** |
| Big Brother set pieces (pool, hot tub, long table, diary chair, have-not room) | **Most of Tier 1 landed 2026-09-19** — pool, hot tub, loungers, the long table and sixteen chairs, the diary chair, the HoH bed, the competition rings and the three podiums authored and placed; the yard re-laid so the water stands clear of the podiums. the HoH door with its key, the basket and the have-not cots. **Tier 1 complete.** |
| STYLARTS swap | **Cancelled by Part 4.** Never downloaded; cannot be committed; authored for full-height rooms against 1.5 m cutaway walls — the plan's own first risk. |

**Remaining, in order:** replace the shell and set pieces with Blender-authored assets (Part 4) → per-room prop
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
| **Seated pose** | **Done 2026-09-19** — the clip was there; nothing set the parameter. `HouseMeetingCoordinator` has two *seated* venues (two chairs facing each other across the long table, two loungers by the pool) whose slots are the placed seats' floor positions, every lease carries a facing per slot (into the seat, or toward the other speaker at a standing chat), and `CharacterPresentation` sits, talks and turns to it once arrived. `Idle` and `Walk` loop now (they never did). Pinned by `Meeting_ASeatedVenue…`, `NpcRuntime_ASavedTableMeeting…` and `HouseSeatedVenueTests` (slots on chairs, in the shipping scene) |
| **Conversation blocking** — two people facing each other, talk/listen loops | **First pass 2026-09-19** — a facing per lease slot turns the pair toward each other (or into their chairs); `Talk_loop`, `SitTalk_loop` and `Listen_loop` authored on the shipped rig by `bb_anim_casual.py`; `Talking` drives them. Listen is exported and not yet wired |
| **Reactions** — nominated, saved, evicted, won | **First pass 2026-09-19** — four one-shots authored on the rig, wired from Any State on triggers while standing; the director asks the nominees, the saved, the evicted and the winner at their beats (`Reactions_…` PlayMode test) |
| **Faces from `mood` × `stressLevel`** | **Done 2026-09-19** — `FaceExpression` builds five blend shapes at runtime on the shipped mesh's Face submesh (the Quaternius head is two white eye shapes on a dark head, no mouth, no brows, no UVs: the emoticon's vocabulary — inner corners down, up, a squint to the lid, narrow, wide), one mesh per source mesh shared by every body of that kind; `CharacterPresentation` pushes the contestant's two words on every attach, eased, immediate under reduced motion; the six bodies the prefabs use import Read/Write for it. Two `Faces_*` PlayMode tests. Blender shape keys were the plan's route; the runtime route needs no key per body and no FBX re-export, and gives every body the same five by one rule |
| Show-specific wardrobe | **Missing** — "Blender-authored content is still the plan" |
| UMA at a full house of sixteen | **Unprofiled** |

**Decide once: UMA or bespoke.** UMA gives variety cheaply and costs per frame; bespoke rigs give a
look and cost an artist. The animation set is needed either way and is the part that reads loudest.
Part 4 §4.5 covers both routes.

### 3.C — Audio *(wiring done; first content 2026-09-19)*

- A composed theme and a season bed, dropped into `Resources/Audio/Theme` and `Resources/Audio/Season`
  — the swap already exists. **Rendered 2026-09-19** by `ArtSource/audio/bb_music.py` under Blender's Python: a sixteen-second C-major fanfare and a forty-six-second A-minor bed, both seamless loops (rendered twice, second pass kept); `HasRecordedMusic` is true for the first time. `bb_check.py` reports peak, loudness, offset, clipping and the seam.
- An SFX set keyed to `HouseAudio.Cue`: button, competition start/win, nomination, veto, eviction,
  finale. Room tone per room. UI foley. **Eleven cues rendered 2026-09-19** by `bb_cues.py` into `Resources/Audio/Cues/<Cue>`; `HouseAudio` loads a recording when one ships and composes the old tone when it does not. Room tone per room and UI foley remain.
- A mixer with buses, and a "reduced audio" preference beside reduced motion. **The preference landed 2026-09-19**: "Reduce sound" in the settings stops the room tone and halves the cues and the music; every cue still sounds (`HouseAudio.SetReducedAudio`, `Audio_*` PlayMode test). The mixer remains.
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
| **6 Motion and feedback** | **First pass 2026-09-19** — a closing panel fades out on a ghost canvas that owns no controls (`HudFade`), a pressed button dips (`HudPress`), a meter's fill travels to its new value (`HudFill`); the modal and the status line already rose in. Every one is nothing under reduced motion (`Motion_*` PlayMode tests). Left: card travel is the stings' own; hover states are Unity's |
| **7 Layout containers** | **Partial** — four `LayoutGroup` uses; the fixed chrome is still absolute-positioned |
| Controller and Steam Deck navigation | **First pass 2026-09-19** — the UI module's default map already walked panels from a pad; the house's shortcuts (Escape, J, F5, R, E, Space) now come through a Shortcuts map in `HouseCamera.inputactions` with Start, Select, the left stick's press, North, West and South beside them, and the camera has the sticks, triggers and shoulders (§3.E Phase 3). Start opens the settings when nothing is open, because a pad has no other way there. `Controller_*` PlayMode test. Open: the cast screen and the main menu are keyboard-and-mouse only; a pad has no way to type a speech; button glyphs in the HUD's hints |
| **Localisation** | **Key layer in place 2026-09-19; no table ships.** `Localisation` maps an English caption to translated text from `Resources/Localisation/<language>`, the HUD asks it at its one text sink and its prompt line, and a control keeps its English caption as its name and key while the words change — the caption contract holds. The language is a display preference, shown only once a table ships. Open: the overlays (menu, cast screen, ceremonies, report) draw their own text and do not ask yet; composed sentences fall through untranslated until written as formats; no translation exists |

**Do the idiom change before adding more panels.** Every panel built this month (deals, events,
storylines, minigames, recap) was built to the current idiom. A new idiom re-does them.

### 3.E — Cinematics and camera

**Absorbs:** the camera plan's four phases.

| Step | Status |
| --- | --- |
| Wheel zoom, click-to-frame, follow, orbit, pan, `F` | Done |
| **Phase 1** — pitch tied to distance, zoom toward cursor, occlusion pull-in | **Done 2026-09-19.** The authored pitch is kept as the orbit's offset, so the first frame reframes nothing and reduced motion stays total (it took absorbing the in-flight pitch on the toggle). The occlusion cast starts at the zoom minimum: the sofa beside the focus can never yank the camera onto the floor, and the pull-in never passes what the player could zoom to. Three `Camera_*` tests |
| **Phase 2** — cast-rail focus, follow ring and chip, Tab to cycle | **Done 2026-09-19.** A cast-rail portrait is a button (the name chip is its caption, so the keyboard ring and a screen reader find it) that follows that houseguest and lets go on a second press; `FollowRing` rides a gold disc under whoever the rig follows, however chosen; the HUD names them in a chip redrawn when the subject changes; Tab and Shift+Tab cycle the active house in cast order, only with no panel open and no control focused — inside a panel Tab is the ring's. `Follow_*` PlayMode tests |
| **Phase 3** — middle-drag, edge pan, gamepad, **move to an Input Actions asset** | **Done 2026-09-19.** `HouseCameraActions` builds one map in code — Orbit, OrbitRate, Pan, Drag, Zoom, ZoomRate, Recenter, Point, Next, Previous — with a keyboard-and-mouse binding and a gamepad binding on every control that has both, and the rig reads only the map (amounts and rates are separate actions, so a stick's speed never depends on the frame rate). Middle-drag keeps the ground under the cursor; the edges pan in a full-screen window and by opt-in in a windowed one (the cursor leaves a window through the band that would pan); the right stick orbits, the left pans, the triggers zoom, the stick's press recenters, the shoulders are Next and Previous for the director. `Gamesim/U07/Export the camera actions` writes `Assets/Gamesim/Input/HouseCamera.inputactions` from the code (the code is the truth) and the episode scene's rig carries it. `HouseCameraActionsTests`, three `CameraInput_*` PlayMode tests. Open: the director still reads Tab itself rather than the map's Next/Previous; the settings panel has no edge-pan toggle for windowed play |
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
the scene; a real house will not fit that way) · Mac and Linux targets (**the editor on this machine has only the
Windows and WebGL modules — installing the Mac and Linux modules through the Hub is a human step before a
target can be built**) · ~~quality tiers, resolution,
framerate cap~~ **done 2026-09-19**: the settings panel's Display block cycles the quality tier (the
pipeline's Lean and Full levels), the frame rate (VSync, 30, 60, 120, uncapped), full screen or a
window, and edge panning, kept in PlayerPrefs beside the sound and accessibility preferences
(`EpisodeDirector.Display`, `Display_*` PlayMode test); resolution follows the window · Steam:
achievements over the existing event kinds, cloud saves over a save system that already validates
and rotates · a licensed Unity CI runner so the 1,360 tests run on every push.

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
- **Sources live outside `Assets/`.** `ArtSource/` at the repository root. The first assets are
  *procedural*: a `bpy` script per set piece (`ArtSource/setpieces/bb_set_*.py`) builds it from
  boxes, rings and discs through `ArtSource/tools/bb_build.py` and exports it through the checklist
  in `bb_export.py` — a few kilobytes of text that regenerates the FBX exactly, so nothing needs
  LFS until a hand-modelled `.blend` arrives. When one does, it is tracked with **Git LFS**
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

Textures are baked by `ArtSource/tools/bb_bake.py` (2026-09-19): a procedural surface on a unit
plane, Cycles on the CPU, to an sRGB albedo and a tangent normal, square and power of two, which
`AuthoredAssetImporter` imports as colour and as a normal map respectively. Every mesh `bb_build.py`
makes carries a world-scale box UV projection (one metre to one UV unit), so a tile is the same size
on every piece and across a seam between two. The first sets are the floors and the wall plaster.

The export checklist is `ArtSource/tools/bb_export.py` (2026-09-19): every export goes through
`export_collection`, which refuses a collection that breaks a convention — name prefix, applied
transforms, root origin on the floor, no n-gons, mesh named like its object — and writes the FBX
with the one set of settings above. Its counterpart in Unity is
`Assets/Gamesim/Editor/AuthoredAssetImporter.cs`, and `AuthoredAssetImportTests` pins the contract
on the assets that ship. Run either side headless (`blender --background --python <script> -- <out>`)
or through the MCP's `execute_blender_code`; a review needs nobody at the keyboard.

### 4.4 What to build, in order

Priority is by how much each piece changes the read of the shipping scene per hour spent.

**Tier 1 — the Big Brother set pieces** *(none exist in any pack; the old plan's Phase 2)*

| Piece | Where | What makes it the show |
| --- | --- | --- |
| **Pool and hot tub** | competition yard | The most recognisable thing about the backyard. Basin, coping, water plane (a shader, not geometry), jets, a seating ring on the tub, loungers around both. **Authored 2026-09-19** — `bb_set_pool` (7.0 × 4.4 m raised deck, tiled basin, steps, translucent lit water, `_col`), `bb_set_hottub` (2.4 m drum, seat ring, `_col`), `bb_set_lounger`; single LOD so far; not yet placed — placement is the catalogue seam's job. |
| **Long dining table + sixteen chairs** | kitchen | Replaces the round table. One chair per houseguest — the house does most of its arguing here. Sixteen because the cast can be sixteen. **Authored and placed 2026-09-19** — `bb_set_diningtable` (4.8 × 1.1 m on two trestles) and `bb_set_diningchair` ×16, seven a side and one at each end. |
| **Diary room hero chair** | private room | Lit separately; the one piece the camera sees in close-up during reflections. 2048 textures. **Authored and placed 2026-09-19** — `bb_set_diarychair`, a velvet throne on a brass plinth behind the diary marker, facing the camera; its own light and close-up framing still open. |
| **Memory wall** | living room | Sixteen portrait frames in a grid; the frames are geometry, the portraits are the existing RenderTextures — `MemoryWall` already refreshes them. **Authored and hung 2026-09-19** — `bb_set_memorywall`: two rows of eight lit frames on a dark backing, each frame its own object so the runtime can tint it, unpacked by `MemoryWallBuilder` into the "Memory frame NN" hierarchy the runtime and its tests already read; `MemoryWall` shows one frame per houseguest and centres them along the rows, so a six-person season is not six keys at one end of sixteen. The bookcase that stood in front of it moved to the north wall. |
| **HoH room** | HoH floor | The bed, the door with the key, the basket. The reward room should look like one. **Bed authored and placed 2026-09-19** — `bb_set_hohbed`, velvet and brass under the wing's 1.1 m wall. **Door, key and basket the same day** — `bb_set_hohdoor` dresses the divider's gap into the nomination room: brass threshold, jambs, the leaf standing open into the suite with the gold key on it and the HoH plaque, all 1.1 m so nothing tops the cutaway; `bb_set_hohbasket` sits on the coffee table. |
| **Competition yard podiums and rings** | yard | The gold rings exist as 144 primitive segments; replace with one authored ring mesh and podiums that match. **Authored and placed 2026-09-19** — `bb_set_compring` (three 96-sided annuli at the primitives' radii, neon gold that glows on import) over the 144 segments, `bb_set_podium` ×3 on the blocks' own footprints; the primitives stay unlit with the colliders the NavMesh was baked from. The yard was re-laid at the same time: the pool's first placement stood on podium 3, so it now runs long-ways along the east wall with the loungers at its south end and the hot tub in the north-west corner (`EpisodeHouseYardTests` pins the layout). |
| **Have-not room** | game room south | Cold palette, harsh surfaces. Set dressing only — the simulation has no have-not mechanic and this plan does not add one. **Dressed 2026-09-19** — two `bb_set_havenot_cot` steel cots with thin cold mattresses along the game room's south wall. |

**Tier 2 — the shell.** Walls, floors, wall caps, door frames, skirting, the cutaway edge itself
as an authored trim. *(Walls, caps, skirting and jambs landed 2026-09-19 as `bb_shell_house`,
generated from the scene's own colliders. Floors textured 2026-09-19: `bb_tex_floors.py` bakes oak and
walnut planks, slate and lawn through `bb_bake.py`, and `HouseFloorDressing` lays one tiled material
per room floor. The wall material is the baked plaster on the slab since the floors commit; the shell's caps are the cutaway edge's trim. Open: a trim along the floor's own cut edge, if the camera ever shows it.)* **The collider
bounds do not move.** The authored shell is visual geometry
placed over the existing box colliders, or replaces them with `_col` meshes of identical bounds,
and the navigation audit (`Gamesim/Audit house navigation`) passes before it is committed. The
28 × 20 footprint, the 1.5 m / 1.1 m heights, and every doorway are fixed inputs.

**Tier 3 — per-room furniture,** replacing the 132 Kenney entries by room: kitchen, living,
bedroom, bathroom, games, nomination. Each room is a `.blend`; each prop a collection; each export
resolves through the catalogue ahead of the Kenney fallback. A room is "done" when the resolution
audit shows no Kenney id in it. **First pass 2026-09-19, and the audit shows no kit id in any
row of either pass:** `bb_set_kitchenrun` (fridge, doors, sink and tap, drawers, stove with hob
rings and oven door, end panel) by its own row, and thirty-three stand-ins through the catalogue's
replacement table — stool, bin, sofa, armchair, floor and table lamps, bookcase with its books,
side table, coffee table, speaker, rugs, plants, single and double beds, bunk, television and its
console, desk, round table, the dining chair for the nomination room's chairs, the ensuite's tub,
shower, basin and toilet, the bar, the counter appliances, cabinet, fridge, pillow and coat rack.
Rows scale by height, so every piece is authored at the height its rows ask for. Still to do: a
second pass on the pieces that read weakest at 4 m, and the furnishing pass (U02) has not been
re-run, so the "Furnishings" root still holds the kit models it placed on the prototype's
primitives.

**Every room off the kit — 2026-09-19.** The U02 furnishing root, placed once on the prototype's primitives, still
carried 27 kit models after the catalogue learned to replace ids; re-run through the catalogue it holds eighteen
authored stand-ins, the television rows are gone (HouseSetPieces places that pair) and the sofa is one piece.
The shipping scene references no kit model. `HouseFurnishing`'s Fit modes still scale a stand-in into the
primitive's footprint, so a re-run after a resized primitive is the check to make.

**Tier 4 — clutter.** Mugs, books, cables, bottles, towels. The old plan was right that this is
what stops a dressed room reading as a greybox, and right that it is cheap. Instance-friendly:
one mesh, many placements, `GPU Resident Drawer` is already on in the PC renderer. *(Begun
2026-09-19: `clutter_pieces.py` builds a mug, a bottle, a book stack, a towel, a cushion, a laptop
and a bar tray, one thin `bb_set_*.py` each; fourteen rows lift them onto the long table, the
counter, the sofa, the HoH coffee table and tub, the desk and the bar.)*

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
| `sit_idle`, `sit_to_stand`, `stand_to_sit` | the existing `Seated` parameter | **Wired 2026-09-19** with Quaternius' `SitDown`/`StandUp` (the one-shot holds its last frame as the seated idle). A breathing `sit_idle` loop is still worth authoring; the transitions are in the controller. |
| `talk_loop`, `listen_loop`, `nod`, `shrug`, `arms_crossed` | `SetTalking` and conversation state | Two people facing each other and taking turns is what a conversation looks like. **`Talk_loop`, `SitTalk_loop`, `Listen_loop` authored 2026-09-19** on the shipped Generic rig (`ArtSource/tools/bb_anim.py`, `ArtSource/animation/bb_anim_casual.py`); nod/shrug/arms_crossed open. |
| `react_nominated`, `react_saved`, `react_evicted`, `react_won` | the ceremony beats the stings already fire on | One-shots, additive over the idle. **All four authored and wired 2026-09-19** — from Any State on a trigger while standing, back to idle; not additive yet (they cut). |
| `walk`, `idle` variants × 3 | `Speed` | Sixteen identical idles read as clones. |

Export as **Humanoid** clips (FBX per clip or one FBX with actions as takes), *Loop Pose* on the
loops, *Root Transform* baked, and assign them in `GamesimCharacter.controller` — which already
declares `Speed` and `Seated` and has empty states waiting.

**Faces.** `mood` (five values) and `stressLevel` (five values) are simulated per houseguest and
reached the screen only as a text line about the player. **Done 2026-09-19 by a third route:** the
shipped heads have no mouth, no brows and no UVs, so the five shapes are built at runtime from the
Face submesh's two eye clusters (`FaceExpression`), and `CharacterPresentation` lerps the weights
from the two fields. A Blender shape key remains the route for a head that has features to move;
UMA bodies have no Face material and show no expression until Route A is taken.

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

1. ~~Export checklist script and the `AssetPostprocessor`~~ **done 2026-09-19** (`bb_export.py`,
   `bb_build.py`, `AuthoredAssetImporter`, `AuthoredAssetImportTests`). LFS waits for the first
   hand-modelled `.blend`; procedural sources need none.
2. ~~**The seated clip**~~ **done 2026-09-19** — it was never the clip: seated venues, a facing per slot, and looping `Idle`/`Walk` (see §3.B).
3. **The catalogue seam** — so every later asset has somewhere to land.
4. Tier 1 set pieces, pool first — pool, hot tub, loungers, the long table and its sixteen chairs, the
   diary chair and the HoH bed authored and placed 2026-09-19 (eight scripts, all through the
   checklist, all pinned by `AuthoredAssetImportTests`); the competition rings, podiums and the
   memory wall, the HoH door and basket and the have-not cots the same day (fourteen scripts).
   **Tier 1 is complete.**
5. Tier 2 shell — **first pass 2026-09-19**: `ArtSource/shell/extract_walls.py` reads the 24 active wall
   and fence colliders out of the shipping scene, `bb_shell.py` builds caps, skirting and doorway
   jambs over them, and the set-pieces pass places the export and switches the primitive walls'
   renderers off with their colliders untouched (reachability 28/28 after). **Floors 2026-09-19**:
   the first baked textures (`bb_bake.py`: Cycles on the CPU, headless; albedo + tangent normal,
   power of two), laid per room by `HouseFloorDressing`, pinned by `AuthoredTextureTests`. Still to
   come: floor trim at the floor's cut edge only - the wall material landed with the floors (the slab is still a flat tone).
6. Tier 3 furniture, kitchen first (it has the long table) — **first pass complete 2026-09-19**: the
   kitchen run and thirty-three catalogue stand-ins; no plan row resolves to the kit.
7. Conversation and reaction clips; faces — **clips first pass 2026-09-19** (eight takes on the
   shipped Generic rig; the importer takes `Animation/Generic` as Generic, names clips after their
   takes and loops `*_loop`; `AuthoredClipWiring` builds the states). Faces open.
8. Tier 4 clutter — **begun 2026-09-19**, seven pieces on fourteen rows.
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

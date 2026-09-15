# Big Brother — UI / UX Overhaul

Companion to `big-brother-house-visual-overhaul.md`, which deliberately scoped out screen-space UI
("the reference image's side panels / buttons are a separate UI task"). This is that task.

## Current State (verified)

- All UI is **built procedurally in C# at runtime** — no prefabs, no UXML, no authored layout.
  `Assets/Gamesim/Runtime/Episode/EpisodeHud.cs` (~29 KB) constructs every panel, button and label
  in `Begin()`, tearing the canvas down and rebuilding it on each call.
- **Legacy uGUI `Text` with `LegacyRuntime.ttf`** (`Resources.GetBuiltinResource<Font>`). No SDF, no
  TextMeshPro, and `supportRichText = false` everywhere.
- Four hard-coded colours are the entire visual system: `Ink` (.035,.055,.085), `Surface`
  (.085,.13,.18), `Accent` mint (.5,.93,.78), `Paper` (.95,.96,.98).
- Panels are flat `Image` rectangles — no sprites, no corner radius, no border, no shadow.
- Fixed chrome is absolutely positioned with magic numbers (`Anchor(objective, …, new Vector2(18,-210),
  new Vector2(294,54))`). Five fixed panels: Brand, Navigation, Objective, Exploration controls,
  Status, plus a centred Interaction prompt and a 790×680 modal.
- `CanvasScaler` is `ScaleWithScreenSize` at 1600×900, match 0.5 — correct and worth keeping.
- **Genuinely good work already present, do not regress it:** hand-wired explicit keyboard navigation
  with focus restoration across rebuilds (`preferredSelection`), Tab/Shift+Tab traversal,
  `RevealSelection` scroll-into-view, a `LateUpdate` pass that grows action rows once wrapped label
  height is known, a public `FontScale`, and `EpisodeSpeechInputField` guarding Esc/Tab.
- The 3D scene is now a dark neon TV set with bloom and authored furniture and a rigged six-person
  cast. **The HUD has no visual relationship to it** — flat slate rectangles over a cinematic set.

### The constraint that shapes sequencing

PlayMode tests bind to the UI in two ways, and they are not the same:

- `DiaryHasButton(caption)` matches **`button.name == caption`** — the GameObject name, which
  `Action()` sets from the caption string. **Button captions are a test API.** Renaming a caption is
  a behavioural change, not a copy edit.
- `ActiveDiaryText()` reads **`GetComponentsInChildren<Text>()`** — the legacy component type.
  `TMP_Text` does not derive from `UnityEngine.UI.Text`, so a TextMeshPro migration silently returns
  empty text and fails those assertions.

Seven PlayMode files reference `Text` (47 references). Any TMP migration must update those helpers in
the same change, and the suite must be green before the commit lands.

## Target Experience

The game is a television broadcast. The UI should read as the **show's graphics package**, not as a
debug overlay sitting on top of it: broadcast typography, a lower-third for dialogue, a persistent
week/phase bug, and faces — it is a game about six people, and the UI currently never shows one.

## Design decisions (recommendations baked in; flag if you disagree)

- **TextMeshPro over legacy Text.** TMP ships inside `com.unity.ugui` 2.6.0, already in the manifest —
  no new package. This is the single largest legibility win and unlocks rich text for inline emphasis
  (names, threats, alliance colour-coding). *(Alt: keep legacy Text — cheaper, but caps the ceiling on
  every later step.)*
- **A `UiTheme` ScriptableObject** replacing the four consts and the magic numbers, with sliced sprites
  for rounded panels. Restyling then costs one asset edit rather than a pass over 29 KB of layout code.
- **Character portraits via RenderTexture**, newly possible now that rigged models exist: a small
  off-screen rig renders each houseguest's head to a texture the HUD samples. Gives the social sim
  actual faces in nominations, votes, and the notebook. *(Alt: pre-rendered PNG portraits — cheaper and
  no runtime cost, but goes stale whenever wardrobe colour or model changes.)*
- **Keep procedural construction.** Rewriting to UI Toolkit or prefabs would throw away the keyboard
  navigation and focus-restoration work, which is the best part of the current HUD. Refactor its
  *styling and layout*, not its architecture.
- **Captions are frozen** unless a step explicitly updates the matching test constant. The public
  `const string` captions at the top of `EpisodeHud` are the contract.

## Implementation Steps

### Step 1 — TextMeshPro migration
- **Description:** Swap `NewText` to build `TextMeshProUGUI`; replace `Font` with `TMP_FontAsset`;
  map `resizeTextForBestFit` to TMP auto-sizing and `HorizontalWrapMode` to TMP equivalents. Update
  the seven PlayMode helpers that read `Text` — `ActiveDiaryText()` in `EpisodeDiaryRoomPlayModeTests`
  and its siblings — to read `TMP_Text`. Generate a font asset with the punctuation the copy actually
  uses (`·`, `↑`, `↓`, curly quotes) or they render as tofu.
- **Dependencies:** None. **Parallelizable:** No — everything else builds on it.
- **Gate:** Full PlayMode suite green before commit.

### Step 2 — Theme extraction and panel styling
- **Description:** Introduce `Assets/Gamesim/Art/UI/UiTheme.asset` holding colours, corner radius,
  spacing scale and type ramp. Replace `Panel()`'s flat `Image` with a 9-sliced rounded sprite. Add a
  subtle border and drop shadow so panels sit above the bloom rather than dissolving into it. Retune
  the accent from flat mint toward the scene's neon palette so HUD and set agree.
- **Dependencies:** Step 1. **Parallelizable:** Yes, with Step 3.

### Step 3 — Character portraits
- **Description:** Off-screen portrait rig: one camera, a `RenderTexture` per houseguest, head-framed
  on the persona prefab from `Resources/GamesimCharacters/`. Feed portraits into nomination rows, vote
  tallies, jury questioning and the notebook. Reuse the existing `wardrobeColor` so a portrait matches
  that houseguest's in-world wardrobe.
- **Dependencies:** Step 2 for framing style. **Parallelizable:** Yes, with Step 2.
- **Watch:** six live RenderTextures is cheap, but render them **once on cast change**, not per frame.

### Step 4 — Broadcast framing
- **Description:** Convert the Status bar into a lower-third for dialogue and beats. Turn the
  Objective panel's week/phase line into a persistent broadcast bug. Add stings for the dramatic
  moments the sim already models but renders as plain text swaps — nomination, veto, eviction, jury
  vote.
- **Dependencies:** Steps 1–2. **Parallelizable:** No.

### Step 5 — Relationships at a glance
- **Description:** The simulation tracks relationships, alliances, promises, oaths, suspicion and
  voting blocs; none of it is glanceable — it lives behind the Notebook. Add a compact relationship
  strip (portrait + trust indicator + alliance tag) to the modal when a decision involves other
  houseguests, so nominations and votes are made with visible information.
- **Dependencies:** Step 3. **Parallelizable:** No.
- **Note:** must respect the existing knowledge-boundary rules — show only what the player knows.

### Step 6 — Motion and feedback
- **Description:** Panels currently snap. Add short fades and slides on open/close, a press response
  on buttons, and a count-up on vote tallies. **Gate every one of these on the existing reduced-motion
  flag** (`CharacterPresentation.SetReducedMotion` is already wired through `EpisodeDirector`).
- **Dependencies:** Steps 2, 4. **Parallelizable:** Yes, with Step 5.

### Step 7 — Layout containers
- **Description:** Replace the absolute-positioned fixed chrome with anchored layout groups so panels
  reflow instead of overlapping at extreme aspect ratios, and so `FontScale` at its maximum does not
  push text outside its panel. Lowest visual payoff, highest maintainability payoff — do it last, when
  the design has stopped moving.
- **Dependencies:** Steps 1–6. **Parallelizable:** No.

## Verification & Testing

- **Tests:** full EditMode + PlayMode suite green after every step. Step 1 is the risky one; the
  others should be inert to the suite as long as captions and button GameObject names are unchanged.
- **Caption contract:** diff the `const string` captions in `EpisodeHud` against the strings asserted
  in `EpisodeDiaryRoomPlayModeTests` / `EpisodePlayModeTests` before committing any step.
- **Accessibility:** exercise `FontScale` at minimum and maximum, confirm no clipping and no
  overlapping panels; confirm Tab / ↑ / ↓ / Enter still traverse and commit; confirm reduced motion
  disables every animation added in Step 6.
- **Visual:** capture the Game view (not the Scene view) at 1600×900, 2560×1440 and 1280×720, and
  confirm the HUD reads against the bloom-heavy dark set rather than washing out.

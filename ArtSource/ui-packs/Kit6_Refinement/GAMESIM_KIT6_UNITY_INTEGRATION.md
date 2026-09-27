# Unity integration — Refinement Kit 6

## What ships, and what does not

This is an **additive visual kit**, not a replacement UI framework. It supplies 71 transparent, white/tintable runtime PNG sprites, 44 matching original SVG icon/illustration sources, eight standalone 1920×1080 layout previews, five before/after review boards, an icon preview, a manifest, and a manual Editor import helper.

There are no replacement scenes, prefabs, font files, portraits, baked lighting, materials, runtime controllers, gameplay state or animation clips. The C# helper configures only the sprites listed in the kit manifest. It has not been compiled or executed inside your Unity project. The previews are composited design references, not in-engine screenshots of completed changes.

## Install without changing production behavior

1. Commit/stash the current branch and capture the five baseline UI states using the same resolution, player, save, phase, text scale and UI preferences.
2. Copy the supplied `Assets/GameSim/UI/RefinementKit6` directory into your project. Keep `DesignSources`, `Docs`, and `Previews` outside Assets unless you intentionally need them in the project. Do not import preview pages as runtime UI textures.
3. Inspect the optional Editor helper. After the Editor compiles it, choose **Tools > GameSim > Refinement Kit 6 > Apply Sprite Import Settings**. This is manual, not an automatic global texture postprocessor. It never scans/reimports unrelated project assets.
4. The importer reads `Settings/asset_manifest.json`. Every entry contains the exact dimensions, intended Image type, and slice border in Unity order: **left, bottom, right, top**. Do not reuse the approximate 28–40 px defaults from earlier packs.
5. Bind the white masks to your existing UiTheme roles through `Image.color`. No generated hex palette should overwrite the project's palette.
6. Apply first to a duplicate visual test scene or isolated existing prefab variant. Reuse the original selectable objects, callbacks, captions and data bindings.
7. Run your existing EditMode/PlayMode/UI tests and compare screenshots before promoting the change.

Unity's `TextureImporter.spriteBorder` uses X=left, Y=bottom, Z=right, W=top [U1]. A Sliced Image preserves corner regions while stretching the center and appropriate edge regions; a sprite requires defined borders [U2]. The import helper applies these definitions but **does not change Image components** to Sliced: you must set the corresponding live Image type during integration.

## Exact sprite construction

| Family | Texture | Border L,B,R,T | Intended live use |
|---|---:|---:|---|
| panel fill / edge rest / edge focus | 128×128 | 24,24,24,24 | Sliced; 20-unit nominal corner |
| card fill / edge rest / edge focus | 128×128 | 18,18,18,18 | Sliced; 14-unit nominal corner |
| button fill / edge rest / edge focus | 128×128 | 14,14,14,14 | Sliced; 10-unit nominal corner |
| pill fill / edge | 128×64 | 32,30,32,30 | Sliced; shallow stretchable center |
| panel focus halo | 192×192 | 64,64,64,64 | Separate decorative Sliced child, expanded 32 each side at 1:1 PPU |
| meter track / rounded fill | 128×24 | 12,11,12,11 | Follow manifest; avoid whole-value image swaps |
| portrait mask/ring | 256×256 | 0,0,0,0 | Simple, Preserve Aspect |
| icons | 256×256 | 0,0,0,0 | Simple, Preserve Aspect; 24–32 reference-unit display |
| empty-state illustrations | 640×440 | 0,0,0,0 | Simple, Preserve Aspect; text remains live |

The manifest is authoritative for import numbers. All state variants within a control family use identical texture bounds. State changes should alter tint or decorative-layer visibility, not resize the control or move its hitbox. Glow is a separate child and is never the source of the hit target.

The pack uses 100 sprite pixels-per-unit as an import baseline. Match this with the existing Canvas reference pixels-per-unit / Image pixels-per-unit multiplier intentionally. Do not call SetNativeSize on a tested control. Canvas Scaler affects the overall UI, fonts and borders; changing it globally is not a local styling operation [U3].

## Theme binding — roles, not a new palette

`THEME_ROLE_BINDINGS.json` maps conceptual roles to existing project meaning. It intentionally does not name unverified C# fields for surface/text colors. Connect to the actual UiTheme API after inspecting it.

- Surface and SurfaceRaised: the existing dark panel/fill roles.
- TextPrimary and Muted: the existing readable text and secondary text roles.
- OutlineResting: quiet neutral outline.
- Accent: focused or selected action/row.
- Allied: positive relationship indicators.
- Conflict: negative trust/tension indicators, according to your current project's distinction from Danger.
- Danger: nomination, destructive/urgent risk where the project uses it.
- Gold: genuine power/achievement; not every empty record, location count or neutral mood.

The shipped RGB is white with alpha geometry. That makes recoloring through Image.color predictable. The PNG has no active name, timer, fill percentage, juror count, ballot, or portrait embedded. Unity Image supports a tint color and Raycast Target configuration [U4].

## Opaque reading surfaces, not dimmed text

Start reference comparisons with the main notebook surface around 0.94–0.98 opacity. This is a proposed visual range, not a new hard-coded global token. Adjust the **background Image**, not the entire CanvasGroup. Lowering group alpha multiplies all descendants, including text, creating the very weak contrast seen in the supplied pages [U5].

Use a distinct flat scrim behind a focused page when needed. A scrim is UI; it does not require modifying exposure, sky, grade, probes or the baked lighting. No blur plugin is required for a readable first pass. A full-screen record page may visually cover the ordinary right rail, but must not flip the saved compact-HUD preference or remove its data components.

## Organization: attach visuals to existing controls

Illustrative visual pattern, not a replacement production hierarchy:

```text
ExistingScreenRoot                 [existing presenter and state]
├── ExistingHeader                 [title + existing Close control]
├── ExistingContentViewport
│   ├── ReadingSurface             [new Image, decoration only]
│   └── ExistingContent
│       └── ExistingRowButton      [same callback/caption/hit rect]
│           ├── SurfaceFill        [card_fill, theme Surface]
│           ├── RestingEdge        [card_edge_rest]
│           ├── FocusDecoration   [off at rest; not a Selectable]
│           ├── Portrait          [approved sprite / actual avatar render]
│           └── ExistingCaption   [TMP, still found by existing tests]
└── ExistingFooterActions          [outside long scrolling content]
```

Keep decorative Images `raycastTarget=false`. A modal blocker is an intentional exception: it should stop clicks reaching the game, while a non-modal social message should not. `CanvasGroup.interactable` and `blocksRaycasts` are distinct from alpha; alpha zero alone is not a complete hidden/input state [U5]. Do not place a non-blocking CanvasGroup on the interactive root and accidentally disable its descendants. Follow the current mode manager's contract for whether gameplay pauses.

Use one clipped scrolling region for long content, with header and relevant confirm/back actions outside it. `RectMask2D` is appropriate for rectangular clipping on coplanar uGUI content [U6]. Do not blindly place ContentSizeFitter on a RectTransform already being size-driven by a layout group; use the project's existing layout arrangement or test the new one in isolation.

## Data-driven meters and gauges

Earlier example bars included a drawn partial value. Do not crop/scale those as though they were an empty track. Here, use `meter_track` behind `meter_fill_rect` under a rounded mask. Bind `fillAmount` or a masked width to a real normalized value. A signed trust display requires a confirmed min/max/zero mapping; do not assume −100..100. It is acceptable to ship the first pass with only the exact signed number.

Never tint a gray overlay over a portrait and describe that as true desaturation. If an evicted portrait must be grayscale, use an approved material/portrait-render path or a real grayscale sprite; this kit's review previews apply a true grayscale transform only to demonstrate that state.

## Sprite atlas and memory

Start with one small atlas for Chrome/Widgets/Icons. Keep large concept previews out of it. Use a separate portrait atlas or the project's current portrait approach. For Canvas UI, disable atlas rotation; Unity's atlas documentation explicitly warns about rotated Canvas textures [U7]. Use Full Rect import for this kit's deliberately padded nine-slice sprites; tight packing is not the objective of this refinement.

Do not claim a particular draw-call reduction from an atlas before profiling the actual masks, materials and canvases. These sprites work with the normal uGUI material; no new shader or bloom dependency is required. Optional focus halo is already a soft alpha mask.

## Responsive reference layout

The previews are 1920×1080 **reference-space proposals**, not an instruction to reset the production resolution or scaling. `LAYOUTS_1920x1080.json` supplies rectangles as x/y/w/h with a top-left origin.

For a top-left anchored RectTransform, anchorMin=anchorMax=(0,1), pivot=(0,1), anchoredPosition=(x,−y), sizeDelta=(w,h), assuming those are your Canvas reference units. Adapt to the existing anchor system instead of forcing this on unrelated screens.

Suggested reference type sizes: screen heading 32–40, row/title 22–26, body 22–24, secondary 18–20, tiny technical metadata 14–16 only sparingly. At 1280×720 a 24-unit reference body line may display around 16 px with proportional scaling. Test the actual game window and UI scale; the screenshots alone do not reveal all Canvas settings.

At smaller effective widths, prefer reflow: rooms become one column, roster metadata wraps, and detail panels become a dedicated page. Do not shrink all labels to force a desktop two-column view to fit. Keep 24–32 padding around primary content and at least 12–16 between related controls. Verify these proposals against the existing rect tests before promotion.

## Skin-only versus approved layout change

**Skin-only first:** tinting, quiet default border, image backgrounds, adding decorative child Images, replacing a source sprite while retaining the same rect/callback/caption. Even these changes still need testing.

**Separate layout patch:** new columns/tabs, content-sized cards, moved footer actions, visible navigation state changes, revised copy, reflow, or moving controls. These intentionally affect RectTransform and caption-based tests. Approve the specific diff, rebase only intended visual assertions, and keep behavior/privacy tests intact. “Preserve 1,650 tests” does not mean pretend a new layout has identical geometry.

Remember the prior test-coverage issue with saved HUD preferences and `SaveRootOverride`. Exercise both test and normal preference paths in isolation; do not assume a suite that bypasses preferences covered the visible production mode.

## Primary Unity documentation

[U1] TextureImporter.spriteBorder: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TextureImporter-spriteBorder.html
[U2] UI Image.Type.Sliced: https://docs.unity3d.com/560/Documentation/ScriptReference/UI.Image.Type.Sliced.html
[U3] Canvas Scaler: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html
[U4] Image: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Image.html
[U5] Canvas Group: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/class-CanvasGroup.html
[U6] RectMask2D: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-RectMask2D.html
[U7] Sprite Atlas: https://docs.unity3d.com/6000.2/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html

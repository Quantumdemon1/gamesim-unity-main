# ArtSource

The Blender side of the authored-asset pipeline (MASTER-PLAN Part 4). Nothing in here is imported
by Unity; what Unity imports is the FBX each script exports into `Assets/Gamesim/Art/Authored/`.

| Path | What it is |
| --- | --- |
| `tools/bb_export.py` | The export checklist as code. `export_collection` refuses a collection that breaks a convention (bb_ name, applied transforms, root origin on the floor, no n-gons, mesh named like its object) and writes the FBX with the fixed settings Unity's importer expects: metres, −Z forward / Y up with the transform baked, triangulated, materials by name. |
| `tools/bb_build.py` | Builders for stylised pieces — boxes, tilted boxes, cylinders, rings, discs, planes — quads and triangles only, positioned in world metres, joined into one object whose origin is the floor contact point. |
| `tools/bb_smoke.py` | One cube, one material, one export: the smallest thing that exercises the whole path. |
| `tools/bb_anim.py` | Humanoid take authoring on the Mixamo skeleton the shipped takes share (65 bones, 30 fps): loads `bb_anim_PoseAtEase.fbx`, poses it in anatomical terms (spine flex, arm raise, elbow bend, finger curl, a two-bone `reach` to a point with the palm turned), keys every frame, asserts the story-take rules in `check()` and exports the armature alone with the settings Gate 0 proved (`FBX_SCALE_NONE`). Its docstring records Gate 0's numbers. |
| `tools/bb_anim_roundtrip.py` | Gate 0: exports a shipped take untouched through `bb_anim`, re-imports it and compares every bone at every 13th frame; optionally writes the breathing base take Unity's probe compares with `bb_anim_PoseAtEase_loop.anim`. |
| `tools/bb_anim_look.py` | Renders a take (or a pair of the same take facing each other, `--pair d`, each stepped `--step s` to its own right) to a contact sheet: a capsule body skinned to the skinless armature, left blue and right red, front, side, top or three-quarter views at chosen frames, Workbench. |
| `animation/bb_anim_react_*.py` | One script per story reaction take (`shocked`, `tearful`, `embrace`, `furious`, `stormoff`), each exporting `Animation/Humanoid/bb_anim_React_<beat>.fbx` after `bb_anim.check()` passes; the embrace also checks two copies of itself facing at the pair distance, each a side step to its own right (`EmbracePair.PairDistance` and `SideStep`). |
| `reference/` | The visual target: the twelve mockups (`mockups/mockup-NN.webp`) and the captures of the build they are measured against (`before/`), per `Assets/Plans/VISUAL-TARGET.md`. Not imported by Unity. |
| `characters/bb_render_head.py` | Renders a front headshot of any body FBX to a PNG, with shape keys set on the command line, for looking at a face's geometry before and after a change. The houseguests themselves are UMA bodies built at runtime, so no character is authored here (the Quaternius-based authored bodies were removed on 2026-09-27). |
| `setpieces/bb_set_*.py` | One script per set piece. The script *is* the source: it regenerates the FBX exactly, so there is no `.blend` to keep and nothing for Git LFS until a hand-modelled asset arrives. |
| `shell/extract_walls.py` | Reads the shipping scene's text serialisation and writes `shell/house_walls.json`: every thin, tall BoxCollider under `House Architecture` — the walls, dividers and fences the NavMesh is baked from — with world centre, size and active state. |
| `shell/bb_shell.py` | The Tier 2 shell: for each active wall in `house_walls.json`, a slab with a cap along its top, skirting on both faces and a jamb at every end that meets no other wall (the doorways); fences get slab and cap. Exports `Assets/Gamesim/Art/Authored/Shell/bb_shell_house.fbx`, origin at the world origin. The set-pieces pass places it and switches the primitive walls' renderers off — their colliders stay, because the NavMesh is baked from them. |

## Regenerating an asset

```bash
"/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python ArtSource/setpieces/bb_set_pool.py -- "Assets/Gamesim/Art/Authored/SetPieces/bb_set_pool.fbx"
```

Then let the editor refresh. `AuthoredAssetImporter` (in `Gamesim.Editor`) fixes the import settings
for anything under `Art/Authored/`, turns a child named `*_col` into a convex MeshCollider that does
not render, makes a material named `*water*` or `*glass*` a translucent URP surface, and imports
`Characters/`, `Wardrobe/` and `Animation/` as Humanoid with `*_loop` clips looping.
`AuthoredAssetImportTests` (EditMode) pins all of that on the assets that ship, including that every
`setpieces/bb_set_*.py` has its export and every export has its script.

## Conventions the checklist enforces

- Names: `bb_<category>_<name>[_<variant>][_lodN][_col]`; object, mesh and file share the name.
- 1 Blender unit = 1 metre; scale (1, 1, 1) and rotation (0, 0, 0) applied.
- A root object's origin is (0, 0, 0) and its lowest vertex sits at z = 0 — the floor. Children
  (water surfaces, `_col` shapes) are parented without moving.
- Quads and triangles only. Cylinder and disc caps are triangle fans for that reason.
- One Principled BSDF per material, named `bb_mat_<name>`, base colour only unless a texture is
  baked; the same material name is reused across assets so Unity shares the extracted `.mat`.

The full rationale, the Unity-side texture packing and the LOD, collider and scale-of-detail rules
are §4.3 of `Assets/Plans/MASTER-PLAN.md`.

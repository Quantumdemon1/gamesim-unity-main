# ArtSource

The Blender side of the authored-asset pipeline (MASTER-PLAN Part 4). Nothing in here is imported
by Unity; what Unity imports is the FBX each script exports into `Assets/Gamesim/Art/Authored/`.

| Path | What it is |
| --- | --- |
| `tools/bb_export.py` | The export checklist as code. `export_collection` refuses a collection that breaks a convention (bb_ name, applied transforms, root origin on the floor, no n-gons, mesh named like its object) and writes the FBX with the fixed settings Unity's importer expects: metres, −Z forward / Y up with the transform baked, triangulated, materials by name. |
| `tools/bb_build.py` | Builders for stylised pieces — boxes, tilted boxes, cylinders, rings, discs, planes — quads and triangles only, positioned in world metres, joined into one object whose origin is the floor contact point. |
| `tools/bb_smoke.py` | One cube, one material, one export: the smallest thing that exercises the whole path. |
| `reference/` | The visual target: the twelve mockups (`mockups/mockup-NN.webp`) and the captures of the build they are measured against (`before/`), per `Assets/Plans/VISUAL-TARGET.md`. Not imported by Unity. |
| `characters/bb_char_*.py` | One script per authored body, on the shipped rig: the CC0 base re-dressed for a houseguest and exported through `export_character` as FBX and GLB into `Art/Authored/Characters/Generic/`, where the importer takes it as a readable Generic rig and `Gamesim/U07/Build the authored character prefabs` writes its `Resources/GamesimCharacters/<id>` prefab. The file name is the template id with underscores. |
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

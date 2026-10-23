# Original appliance and personal-case candidates

Prepared 20 October 2026 for the GameSim art work. These are isolated candidates,
not imported GameSim assets or accepted Unity rendering results. No project,
scene, editor, test source, navigation data, or other chat's asset package changed.

| Asset | Intended existing placements | Unity width / height / depth | Triangles | Materials |
|---|---|---|---:|---:|
| bb_set_mini_fridge | HoH and Game Room Mini fridge | 0.50 / 0.70 / 0.50 m | 3,880 | 1 |
| bb_set_headphone_case | Bedroom Headphone case | 0.16 / 0.06 / 0.16 m | 2,308 | 1 |

Both have bottom-centre origins, front at Blender -Y / Unity -Z, applied scale
and rotation, UVs, finite normals, and one original 1024-pixel PBR atlas each.
The mini fridge includes a rounded metal cabinet, inset door and gasket, handle,
hinge covers, ventilation, service panel and feet. The case has two shaped fabric
shell halves, zipper tape and individual teeth, piping, pull tab and rear binding.
They are closed, static dressing assets. No door animation, contents, interaction,
collider, rig or simulation behavior is added by these files.

## Files and reproduction

- `author_appliances.py`: deterministic original geometry/texture source; refuses an existing output directory.
- `mini-fridge-01/` and `headphone-case-01/`: FBX, self-contained GLB, editable part-level `.blend`, original texture maps, front/rear previews, per-asset technical receipt and provenance.
- `candidate-contact-sheet-01.png`: front/rear views; fridge above, case below. Each model is framed separately, not at a shared scale.
- `roundtrip-validation-01.json`: all four independent FBX/GLB imports passed dimensions, floor contact, triangle count, UV and finite-normal checks. Actual referenced 1024-pixel image payloads loaded in each import. Both GLBs contain embedded base-color, metallic/roughness and normal textures.
- `native-texture-references-01.json`: FBX relative texture references parsed and verified against actual companion files.

Use Blender 5.2.2 LTS with `--background --factory-startup --threads 2
--python-exit-code 1 --python author_appliances.py -- mini_fridge <absent-folder>`.
Use `headphone_case` for the other model. Blender's bundled NumPy generates all
texture pixels locally. No downloads, third-party model/image inputs, provider
services or purchases were used. No external branding is embedded.

## Native Unity handoff

The FBX uses the existing `bb_export.py` convention: metres, -Z forward / Y up,
FBX_SCALE_ALL, applied unit/axis transform, mesh-only, triangulated, no animation.
The `.fbm` companion directory is required alongside its FBX for the referenced
base-color and normal images. Keep its relative paths intact.

The additional `Textures/` folder includes:

- `_albedo.png`: sRGB base color.
- `_normal.png`: tangent-space OpenGL +Y normal; source material strength 0.3.
- `_orm.png`: R = neutral white occlusion, G = roughness, B = metallic; linear.
- `_metallic.png`: Unity R = metallic, A = smoothness; linear.

The neutral occlusion channel is not a baked contact-occlusion claim. FBX does
not carry the glTF packed metallic/roughness node wiring. Bind the Unity metallic
map explicitly in the external material using the exact material names
`bb_mat_mini_fridge_atlas` and `bb_mat_headphone_case_atlas`. Do not assume the
generic importer reconstructs this shader correctly from the FBX material.
The GLB retains its full standard metallic/roughness material; its opaque material
currently exports double-sided. A project material should use its intended
opaque/culling policy after actual imported-normal inspection.

Once the active source owner releases the art integration boundary, use the
existing `HouseRoomFinish.PieceAt` / `HouseCatalogue` seam. Keep native dimensions,
aggregate bottom-centre supports, current root names and yaw. Existing dressing
semantics remove colliders/static flags and suppress cast shadows. Preserve
the exact HoH/Game Room/Bedroom placement formulas; do not move nearby furniture
or broaden a scene rebuild to integrate these meshes.

Current source call sites were read from `D:/GamesimConsolidation`:
`HouseRoomFinish.cs:435,531` (mini fridges), `:649` (headphone case), `:953`
(PieceAt). The source owner is coordinating the actual admission. Storage bins,
flight cases and laundry work are intentionally outside this asset batch.

## Validation boundary

Blender source rendering and independent import/data checks have completed.
Canonical packaging, Unity material construction/import, saved-scene integration,
in-room renders, gameplay/clearance checks and target-machine performance remain
separate steps. No human approval, Unity visual quality, runtime performance or
shipping acceptance is claimed here.

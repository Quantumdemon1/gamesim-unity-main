# GameSim original packaging family

12 models cover 13 planned house placements: coffee tin, food jar, cereal carton, two kitchen snack bags, two HoH snack bags, three board-game boxes, and two personal-care variants used at three placements.

All geometry, label artwork, and PBR texture data were authored procedurally for this task. No downloaded assets, third-party typefaces in textures, reference-image generators, or paid services were used. Preview-sheet captions use the locally installed Windows Segoe UI font; it is not included in any model texture or redistributed here. Rights are reserved for the GameSim project. This is not a CC0 declaration or a legal assignment of rights.

## Deliverables

- `build_packaging.py`: deterministic original Blender source generator, seed 20261020.
- `Sources/*.blend`: editable mesh, UV and material sources with packed texture images.
- `GLB/*.glb`: self-contained binary glTF, including albedo and metallic/roughness images.
- `FBX/*.fbx`: native static-model FBX. Keep each sibling `<model>.fbm` texture directory with the FBX until Unity material remapping is complete.
- `Textures/*_albedo.png`: 512px sRGB package-print maps.
- `Textures/*_orm.png`: 512px linear R=occlusion, G=roughness, B=metallic maps.
- `Textures/*_metallic.png`: optional Unity URP companions, linear R=metallic, A=smoothness. These are generated from the same ORM data by `finish_packaging.py`.
- `Previews`: three-quarter, front and back CPU-rendered images, an overview, and two multi-angle sheets.
- `Reports`: individual bounds/export receipts, an aggregate build receipt, material mapping, and an independent export audit.

## Coordinate and material contract

Blender sources are in metres, Z up, with the product front at -Y. Each source has one mesh object, applied rotation/scale, and a floor-centre origin at (0,0,0). Expected Unity X/Y/Z dimensions are explicit in the individual receipts. The FBX uses `axis_forward='-Z'`, `axis_up='Y'`, `FBX_SCALE_ALL`, baked space transform, triangles, face smoothing, no animation, and no leaf bones, following the project's read-only `ArtSource/tools/bb_export.py` settings. There are no collider helpers, lights, cameras, or skeletons in the exported models.

Match label materials by their exact names in `Reports/material-map.json`; the three game boxes use label slot 1, while the other models use slot 0. Solid trim and any solid tray/base are separate slots; keep their authored base-colour, roughness and metallic factors rather than applying the label maps to every slot. Each model stays within 2,500 triangles and three materials. FBX does not reliably preserve a Blender shader graph that separates packed ORM channels. The GLB carries that PBR graph as standard glTF data; for FBX in URP, remap the explicit albedo and Unity metallic/smoothness companions using `Reports/material-map.json`. Do not mark `_orm.png` as sRGB.

## Verification and limits

`audit_packaging.py` reparses the binary GLB container and opens each GLB and FBX in a fresh Blender scene. It checks dimensions, floor origin, finite geometry/UVs, triangle and material budgets, nondegenerate faces, closed manifold geometry after welding importer seam duplicates, texture availability, 512px embedded images, normals/UV/tangents, and absence of animation or rig data. Receipts include SHA-256 hashes.

These are isolated candidate assets. No Unity project was written and no shared Unity editor was run. Unity import/material checks, placement checks at house scale, collision/interaction checks, and final visual acceptance belong to the integration pass. Blender preview lighting is review lighting, not evidence of appearance in the house's Unity lighting.

## Rebuild

Use Blender 5.2.2 background mode, factory startup, `-t 2 --python-exit-code 1 --python build_packaging.py`. Optional exact model names after `--` limit a rebuild. Run `finish_packaging.py` with Python/Pillow after all models exist, then run `audit_packaging.py` in Blender. Review the contact sheets and final audit before integration. Canonical game-dev package admission and provenance are handled by the parent integration task.

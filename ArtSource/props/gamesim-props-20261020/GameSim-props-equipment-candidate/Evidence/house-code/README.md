# Staged house small props integration

This is an external integration candidate, not a project import or Unity acceptance result.
No file under a GameSim project was edited; no Unity editor operation or test was run.

Current checks: both guarded existing-file patches and the combined four-file patch pass
git apply --check against D:/GamesimConsolidation. All four staged source files compiled:
45 current Editor sources with the two editor overrides, then both staged EditMode files.
Compilation used cached NoUMA Runtime/Simulation dependencies; this is partial semantic
validation, not a current whole-project build. Source hashes match the compiler receipt.
See validation-receipt.json and ../competition-code/house-partial-probes/probe-results.json.

## Scope

The HouseRoomFinish postimage changes 12 placement call sites and two loop ID expressions,
covering 16 currently saved placement instances using 14 native authored models. Existing
PieceAt owns instantiation, placement at the measured bottom centre, collider removal,
shadow suppression and clearing static flags. Names, support expressions and yaw are retained.

Kitchen gets coffee tin, jar, cereal and two snack bags. The original red kitchen label maps
to the new gold snack design; blue stays blue. HoH gets its own smaller gold and blue bags
and one mini fridge. The game room gets the second mini fridge and three separately named
strategy/alliance/social boxes. Bedroom gets one headphone case and three toiletries using
the existing even-index cosmetic / odd-index toiletry rule.

Storage bins, gift box, shoe boxes, laundry basket, suitcase, accepted flight cases, towels,
books, magazines, drinks, furniture, house geometry and all gameplay stay unchanged.

Six existing RoomFinishTests lines switch from renderer names to direct placement roots and
aggregate bounds. Their support, footprint, stacking and separation assertions remain.

## Reviewed native layout

Place only the reviewed 14 FBX files directly in:

`Assets/Gamesim/Art/Authored/HouseSmallProps/`

Place their explicit `bb_set_*_albedo.png` and `bb_set_*_metallic.png` maps in the `Textures/`
subfolder. The mini fridge and headphone case also supply `bb_set_*_normal.png`; the other
twelve models do not have normal maps. Preserve each sibling `<model>.fbm/` directory if
retaining the FBX's diffuse references, but final Unity materials explicitly bind the maps
in `Textures/`, not `.fbm`. The source ORM maps remain provenance/export inputs and are not
assigned to Unity metallic slots. Canonical GLBs are separate delivery artifacts, not these
native FBX import sources. Use the appliances' **-02** exports containing tangents.

The materials command creates/remaps only this batch's `Materials/*.mat` files, sets the
texture types/color spaces, and explicitly requests imported normals plus Mikk tangents.
URP/Lit is already the project's active material convention. Metallic maps contain R metallic
and A smoothness; the helper uses multipliers of one. Solid trim/base slots retain their
source PBR factors and never receive label art. Normal-map strength is 0.3 for the two
appliances. No global importer hook or broad folder scan is added.

## Admission sequence for the integrating owner

1. Resolve the other chat's current source/test ownership and choose an isolated native
   validation project. The staging does not override that work or its test freeze.
2. Run `python verify_stage.py <project-root>` before copying anything. It verifies exact
   source hashes, staged hashes, new destination absence, and both patches with `git apply
   --check`. Source drift requires review and a new guard; do not force old postimages over it.
3. Admit the reviewed native models/maps and the staged source files. Keep existing .meta
   GUIDs for edited sources; let Unity create metadata for new files. Capture those new metas.
4. While that editor is idle, run the explicit menu command **Gamesim/U07/Prepare authored
   house small props (14 models)**, or its executeMethod entry point
   `Gamesim.Editor.HouseSmallPropMaterials.Prepare`. It refuses Play Mode/compiling and checks
   the complete batch before writing. It does not open, rebuild or save a scene.
5. Compile and run `HouseSmallPropAssetTests` excluding the Explicit saved-scene case. There
   are 28 native asset cases: 14 geometry/origin/UV/normal/tangent/topology checks and 14
   external material/texture/remap checks. Missing native assets are failures, not skips.
6. The scene owner can separately replace the selected existing placements. This package
   does not run HouseRoomFinish.Apply or HouseSetPieces.Apply. Do not rebuild unrelated
   room dressing just to admit these candidates.
7. After a reviewed scene save, explicitly run the saved-scene 16-instance adoption test,
   RoomFinishTests, and the relevant authored-import tests. Check actual house screenshots,
   culling/labels, support contact, no shadow/collision/static regressions and performance.

## Validation boundary and rollback

Blender export/reimport proof is recorded beside the authored candidates. Staged source
review and any offline compilation receipts are separate from native Unity import proof.
No native test, room screenshot acceptance, saved-scene replacement, performance check,
asset admission or integration completion is claimed here.

The explicit command intentionally changes only this batch's importer metas, map metas and
external materials. Preserve before-images/GUIDs in the integrating worktree for rollback;
new asset creation/import metadata is not promised to be fully Undo-able. Existing artist
material remaps outside this batch cause preflight failure instead of being overwritten.

For BaseColor the helper follows the installed glTFast convention observed by the parent
reviewer in ShaderGraphMaterialGenerator.cs:409 (baseColorLinear.gamma). The local URP/Lit
shader declares an ordinary [MainColor] Color property. This is a concrete compatibility
choice, not a claim that every Unity color property has identical conversion behavior.
See the Unity-version [Material.SetColor documentation](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Material.SetColor.html).
Native material appearance still requires comparison under the actual house lighting.

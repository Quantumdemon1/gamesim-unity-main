# GameSim house props and competition equipment — integration candidate

Implemented outside the shared project on 20 October 2026. This bundle contains
real model exports, editable Blender sources, Unity integration patches, material
preparation code and native test source. **It has not been imported into the game.**

GameSim Dev's current source is `D:/GamesimConsolidation`, observed HEAD `f1b93b1c`.
Its latest recorded work owns dialogue/UMA validation and a source freeze. This
session could read its recorded chat but had no tool for sending to it; ownership
of this batch has not been acknowledged by that chat. No shared project, scene,
save, Unity process or validation runner was changed by this work.

## Delivered scope

| Batch | Assets | Existing placements or use |
|---|---:|---|
| House packaging | 12 models | Coffee tin, jar, cereal, four snack bags, three game boxes, three toiletries |
| Appliances/personal case | 2 models | Two mini fridges and one headphone case |
| Competition geometry | 39 reusable parts | Signals, memory pairs, endurance grip, dice tray and word console |

The 14 house models cover **16 of the current 24 primitive room-finish placements**.
The remaining eight are three storage bins, two shoe boxes, a laundry basket, a
suitcase and a gift box. GameSim Dev already prepared the storage-bin package and
researched several of the others. Its three flight cases are already integrated.
Those assets, accepted house furniture, characters, dialogue and game rules are
outside this batch.

The competition patch replaces only the meshes of existing named instrument
parts. It retains their transforms, dynamic fitting, contact planes, actor/rolling
clearance registries, public readouts, tint, overlay behavior and disposal rules.
Invalid or absent kit entries retain primitive geometry. Imported meshes are
borrowed and never destroyed by apparatus cleanup. The five assembled GLBs are
reference assemblies; Unity uses the 39 separate FBXs and still supplies actual
game text, audience repeaters and moving state.

## Bundle contents

- `ProjectOverlay/`: nine C# postimages and the reviewed native model/texture files.
  This is a staging layout, not a command to overwrite a project.
- `Patches/`: apply **only** `competition-authored-geometry.patch` and
  `all-house-source.patch` after checking their baseline guards. Component patches
  retained under Evidence are alternatives, not additional patches to apply.
- `CanonicalPackages/`: 19 verified GLB packages, including provenance and policy
  receipts. The two appliance packages are 1.0.1; the other packages are 1.0.0.
- `Authoring/`: editable .blend files, original maps, deterministic generators and
  geometry manifest. Generation scripts/receipts retain the original local paths;
  reproduce in a new writable work directory after adjusting those paths.
- `Previews/`: Blender review renders. These are not Unity screenshots. Blank
  equipment readout panels are filled by the existing runtime, not by static art.
- `Evidence/`, `native-bridge.json`, `bundle-manifest.json`: file hashes, audits,
  source guards, patch checks and explicit compilation limitations.
- `verify_bundle.py`: read-only integrity and optional current-project checks.

## Verification completed

All **19 canonical packages** passed static policy validation with zero errors and
warnings, and their package hashes verified. Independent Blender checks passed for
28 house imports (14 FBX and 14 GLB), plus all 39 normalized equipment FBXs. Checks
cover the applicable dimensions/origins, geometry, UVs, normals, texture dependencies
and budgets. The normal-mapped appliance GLBs also contain tangents.

The 12 packaging models total 10,808 triangles, at most 1,424/model and three
materials/model, with 512px maps. The fridge is 3,880 triangles and the case 2,308,
each with one material and 1024px maps. These are geometry budgets, not frame-time
measurements. The equipment reference assemblies range from 1,264 to 4,356 triangles;
the real runtime also creates audience-facing repeaters and text.

The house candidate compiled with all 45 current Editor sources and both staged
EditMode test files against cached NoUMA Runtime/Simulation dependencies. The new
equipment kit and its Editor builder passed partial compilation. A whole-source
replay stopped at two missing `CaptureConversationNavigationBaseline` calls in
`PortVerification.Season.Systems.cs`; the unchanged baseline reproduced both.
GameSim Dev is handling that correction. Test probes against cached Runtime also
lack the new six-argument apparatus factory overload. **No whole-project compile,
native Unity test execution, room acceptance or performance pass is claimed.**

Code review confirmed unchanged placement expressions and twenty competition
behavior methods. A second reviewer matched all 28 packaging material slots and
all 60 house import dependencies. The material helper uses named slots rather than
assuming that labels always occupy slot zero.

## Integration sequence for GameSim Dev

1. Finish or release the active source/test freeze and confirm this asset batch
   does not overlap another claimed task. Keep ownership of storage-bin admission.
2. Run `python verify_bundle.py --project D:/GamesimConsolidation` from the unpacked
   bundle. It reads and checks; it does not copy assets or launch Unity. If a target
   changed, rebase the small patch instead of replacing the file with a postimage.
3. Keep canonical package admission separate from native import. Use the current
   GameSim vendor-lock/receipt custody plan if admitting packages to
   `ArtSource/Packages/game-dev/<asset>/<version>`. This bundle has not admitted them
   or changed vendor workflow coverage. `native-bridge.json` binds the original
   FBX companions; they are parallel exports from the bound Blender sources, not
   renamed copies of a canonical GLB.
4. Integrate the two patches and only their listed native files in the owner's
   isolated checkout. Retain existing .meta GUIDs and capture metadata for new files.
   Import all 53 FBXs before invoking either asset preparation command.
5. Verify Unity MCP project root, state and active scene. While the selected editor
   is idle, run `Gamesim.Editor.HouseSmallPropMaterials.Prepare` and
   `Gamesim.Editor.CompetitionApparatusMeshKitAssets.Build`. These prepare only the
   batch materials and optional mesh kit. They do not rebuild or save a scene.
6. Run `HouseSmallPropAssetTests` (28 ordinary cases), the six ordinary
   `CompetitionApparatusMeshKitPlayModeTests`, and existing apparatus fixtures.
   Then explicitly run the complete installed-kit gate. Native import must prove
   identity transforms, material appearance and unit-mesh axes; Blender's +90 X
   import transform is not proof of Unity's axis conversion.
7. Let the scene owner adopt the 16 selected placements, then run the explicit
   saved-scene adoption case and `RoomFinishTests`. The HouseRoomFinish patch makes
   future generation use these models, but **this bundle does not change the saved
   scene**. Avoid broad `HouseSetPieces.Apply` or unrelated room rebuilds.
8. Review actual house/competition captures with UMA and primitive bodies, readout
   privacy, grip/press contact, full-field clearance, cancellation/reduced motion
   and measured performance before accepting the batch.

Material color handling follows the observed installed glTFast convention, with
native appearance still pending. Unity documents property-dependent behavior in
[Material.SetColor](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Material.SetColor.html).

The detailed component instructions and evidence are in
`Evidence/house-code/README.md` and `Evidence/competition-code/INTEGRATION.md`.

# House garment source

`Assets/Gamesim/Uma/Editor/HouseGarmentAuthoring.cs` generates four original garment
meshes from front/back panels, curved sides, shoulder bridges and trim strips.
The crew-neck knit also has long sleeves. Installed clothing geometry is never
copied. Reference-body surface positions and skeleton influences provide fitting
and binding; the project owns the new topology, UVs and procedural textile images.
The installed t-shirt supplies the shader/channel configuration, cloned into
project-owned material assets. UMA remains an installed dependency.

The C# source is the editable pattern. Dimensions are relative to the actual
humanoid landmarks; lengths below are Unity metres on the reference body.

| Pattern part | Definition / UV rectangle |
| --- | --- |
| Front / back | 24 columns, 20 rows, hip to upper-arm height; front `(0.02,0.04)-(0.46,0.66)`, back `(0.50,0.04)-(0.94,0.66)` |
| Side panels | Six curved divisions, hem to 75% torso height; `(0.95,0.04)-(0.99,0.505)` |
| Shoulder bridges | Connect front/back outside the central eight-column neck opening |
| Neck trim | 0.018 m rise; `(0.54,0.70)-(0.94,0.76)` |
| Knit sleeves | 14 rings per arm, surface fitted from armhole through elbow to 95% of elbow-to-hand distance; left/right UV islands below |
| Sleeve UVs | Left `(0.02,0.70)-(0.24,0.96)`, right `(0.28,0.70)-(0.50,0.96)` |
| Vest armhole trim | 0.012 m outward rise; `(0.54,0.80)-(0.94,0.85)` and `(0.54,0.87)-(0.94,0.92)` |
| Clearance | Knit 0.012 m; vest 0.008 m; knit cuff adds 0.005 m |
| Textile / thumbnail | 1024 px neutral albedo, normal, mask PNGs per style; 256 px rendered sprite per fit |

Each vertex interpolates every meaningful source triangle bone influence. The
current weighted skin matrix is inverted to recover bind-space coordinates; the
original skeleton names, adjustment bones and bind poses remain intact. This is
how the installed Human Female/Male 3.0 physique DNA affects the clothes: the body
DNA scales/translates/rotates those bones. Facial blendshapes do not fit a shirt,
and the male body's already-baked `MaleBody` shape must not be applied again.

The torso mask preserves the neck and hem. Vest masks also preserve the upper
outer torso near the armholes. Knit arm masks preserve shoulders, wrists and
hands. Flags target the **effective built slot names**, including the male race's
generated baked slots, rather than assuming the raw UDIM slot names. These baked
slots are regenerated from the installed base recipe in each new process.

| Permanent ID | Recipe / slot stem | Body | Style group |
| --- | --- | --- | --- |
| `gamesim.knit.crew.a` | `Gamesim_KnitCrew_A` | Human Female 3.0 | `gamesim-knit-crew` |
| `gamesim.knit.crew.b` | `Gamesim_KnitCrew_B` | Human Male 3.0 | `gamesim-knit-crew` |
| `gamesim.vest.competition.a` | `Gamesim_CompetitionVest_A` | Human Female 3.0 | `gamesim-competition-vest` |
| `gamesim.vest.competition.b` | `Gamesim_CompetitionVest_B` | Human Male 3.0 | `gamesim-competition-vest` |

Suffixes are `_Recipe`, `_Slot`, `_Mesh`, `_Thumbnail`, `_TorsoHide` and, for the
knit, `_ArmsHide`. All four recipes occupy the existing `Chest` slot. Their first
overlay color is private/unshared and white; the existing Chest fabric color
changes it. There are no new rig, wardrobe slot, save fields or shared channels.

## Isolated batch authoring

Run only after the pinned baseline gate passes and the coordinator copies this
source into the isolated UMA acceptance project. The exporter rejects play mode,
non-batch editors and a project not explicitly named by the authorization flag.
It also requires the project's leaf directory to contain `Acceptance`.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -projectPath 'D:\GamesimAcceptance' `
  -executeMethod Gamesim.Uma.Editor.HouseGarmentAuthoring.Build `
  -gamesimGarmentAuthoringRoot 'D:\GamesimAcceptance' `
  -logFile 'D:\GamesimAcceptance\Logs\house-garment-authoring.log'
```

Do not add `-quit`: a bounded editor update runner owns completion and exits 0
on success or 1 on failure. The runner allows twenty minutes and writes progress,
error and every asset/meta path to `Logs/house-garment-authoring.json`. Do not use
`-nographics`; UMA atlases and thumbnails require graphics. Addressables are not
supported by the synchronous `GenerateNow` reference-body path.

Start a **new** process with the same flags and
`-executeMethod Gamesim.Uma.Editor.HouseGarmentAuthoring.Verify` and a separate
verification log. This resolves all four recipes/slots through the saved project
Global Library, builds them on fresh bodies, checks effective mask application
and private dyes, and verifies identical index bytes before/after. Its JSON report
is `Logs/house-garment-verification.json`. Verification removes transient index
entries only in memory and never saves assets.

The author creates the exact preferred UMA override at
`Assets/UMAProjectData/Resources/AssetIndexerProject.asset`, cloning the installed
index. It uses public `RuntimeInitializeOnLoad()` to reset the cache, registers
the new assets there, removes nonpersistent baked slots before saving and saves
only owned assets with `SaveAssetIfDirty`. The installed index is never saved.

Recover only these generated ownership paths into the garment worktree:

- `Assets/Gamesim/Uma/Content/HouseGarments` and its folder/asset `.meta` files.
- `Assets/Gamesim/Uma/Resources/Gamesim/CharacterCatalog/HouseGarments.asset` and `.meta`.
- The four UMA override files: `Assets/UMAProjectData.meta`,
  `Assets/UMAProjectData/Resources.meta`,
  `Assets/UMAProjectData/Resources/AssetIndexerProject.asset` and `.meta`.
- New parent-folder `.meta` files listed in the authoring manifest, if absent from source.

Acceptance sync and source audits must allow exactly those four index files while
retaining all other local `UMAProjectData` caches. NoUMA copies omit the override.

## Acceptance

Authoring success and fresh-process verification establish asset structure and
discovery; they do not establish visual fit. Review both bodies from front/back,
seated, walking/running, raised arms and pool poses, at the creator's accepted
height and torso extremes. Check seams, cuffs, armholes, neck opening, mask edges,
garment penetration and thumbnail framing. The lifecycle PlayMode fixture
`UmaHouseGarmentLifecyclePlayModeTests` covers stable IDs, compatible paired
substitution, profile/custom-cast/save reload, all five outfits and independent
red/blue actor dyes. Generated assets should be committed only after these checks.

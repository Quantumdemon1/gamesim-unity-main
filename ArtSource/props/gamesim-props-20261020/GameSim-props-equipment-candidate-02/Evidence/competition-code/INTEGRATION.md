# Staged competition geometry seam

This is a candidate handoff, not an applied change. All files are outside the
shared `D:/GamesimConsolidation` project. No Unity operation was performed.

## Contract

- `part-manifest.json` defines 39 entries for five families and 23 explicit part
  roles. Each source mesh is authored at its own metre dimensions and normalized
  offline to the existing centred unit envelope. A mesh for another aspect ratio
  is not stretched to fit at runtime.
- Runtime code keeps every named GameObject, Transform, renderer, material,
  readout and solid/raised/yaw registry. Only `MeshFilter.sharedMesh` changes.
- Missing, ambiguous, malformed or dimension-mismatched entries retain the
  existing owned primitive mesh. Readability is not required for borrowed meshes.
- Bounds must have zero center and unit size within 1e-4 on every axis, as well
  as fit inside the unit envelope. A smaller or offset contained mesh fails the
  contact/pivot contract and falls back. The builder checks this through the same
  resolver before it writes a kit.
- Existing intentional upright, pedestal and grip-meter scaling remains intact.
  Authored support profiles must tolerate this range. Named hand-contact planes
  must remain solid and the die must not carry fake pips/results.
- Borrowed kit meshes and the ScriptableObject are never destroyed by apparatus
  cleanup. The stage still releases its own fallback mesh and materials.

## Integration sequence for the project owner

1. Recheck both baseline hashes in `handoff-manifest.json`; rebase if either
   target file changed. The patch already passed `git apply --check` against
   observed HEAD `f1b93b1c64972b9e0491f0a27e1ebe5771dbe8de`.
2. Apply `competition-authored-geometry.patch` in the owner's isolated integration
   checkout. Preserve existing metadata; let the authorized Unity import create
   metadata for the three new C# files and retain those GUIDs thereafter.
3. Import the parent's 39 `bb_comp_<Family>_<PartKind>.fbx` files under
   `Assets/Gamesim/Art/Authored/Competition/`. Keep the source generator and manifest
   under `ArtSource/competition-apparatus/`. The existing authored importer already
   owns model import settings; this candidate adds no new importer or packages.
4. Run `Gamesim/Competition/Build authored apparatus mesh kit` (or the explicit
   `Gamesim.Editor.CompetitionApparatusMeshKitAssets.Build` method). It validates
   all parts before writing only
   `Assets/Gamesim/Resources/Gamesim/Competition/ApparatusMeshKit.asset`.
   The actual Unity import must confirm baked model transforms are identity,
   normalized mesh bounds, one mesh/submesh, normals/UV0, and no collider/rig.
   Blender import rotation is not proof of Unity mesh-axis correctness.
5. Run `Gamesim.PlayModeTests` filtered to
   `CompetitionApparatusMeshKitPlayModeTests` and
   `CompetitionApparatusPlayModeTests`. The existing primitive-only fixtures now
   inject a null kit, preserving their precise baseline geometry expectations.
6. Explicitly run
   `Kit_InstalledAuthoredAssetsCoverEveryFamilyAndFitTheExistingReservations`.
   This production asset gate fails if the kit is absent or any physical part
   silently fell back; it exercises every referenced mesh and all families.
7. Run the existing in-house apparatus acceptance fixtures with actual UMA and
   primitive bodies. Review native overhead and close-camera captures for all
   families, live front/audience glyphs, body/hand contact, opacity, silhouettes,
   rolling clearance, pause/reduced motion, cancellation and release. Run the
   existing full-field 12/16 and veto audience coverage, then measure the actual
   build on the project's established target hardware before production acceptance.

## Validation actually performed

- Read-only current-source inspection and exact preservation checks for 20
  behavior/ownership method bodies: see `static-validation.json`.
- Patch applicability check passed; source target hashes still matched baseline
  at handoff. Nothing was applied to the shared checkout.
- Offline C# replay used current Consolidation sources with candidate file
  overrides and existing Unity 6000.6 reference caches, with all outputs here.
  Simulation compiled. Runtime reported the same two existing CS0103 errors as
  an unmodified baseline: missing `CaptureConversationNavigationBaseline` in
  `PortVerification.Season.Systems.cs` at lines 382 and 468. The UMA-cache attempt
  reported those same errors. No candidate-specific diagnostic was emitted before
  that stop, but Editor and test assemblies were not reached.
- Six regular native test cases and one explicit installed-asset gate are
  supplied as source. The fallback fixture now uses actual undersized and
  off-center contained geometry, in addition to direct bounds rejection cases.
- A second bounded semantic probe compiled the standalone Kit and Editor builder
  against the cached NoUMA Runtime. The two test source files emitted only two
  CS1501 diagnostics at their new six-argument Create calls, because that overload
  is absent from the cached Runtime. See `partial-probes/*.log` and `*.sarif`.
  No source substitution or stubs were used. This checks the other referenced
  APIs but does not replace compilation with the new Runtime or actual Unity tests.
- The stricter-bounds full candidate replay still encountered the same two
  baseline Runtime CS0103 errors. These are partial checks, not a successful
  whole-project compile or native acceptance claim.

`baseline-no-uma.log`, `candidate-uma.log`, the generated response files and
`handoff-manifest.json` retain the exact paths and hashes. The first candidate
NoUMA attempt's response files are in `compile-evidence/`.
The refreshed candidate is recorded in `candidate-followup-no-uma.log` and
`compile-followup-evidence/`; `compile-partial-probes.ps1` reproduces the bounded
cached-Runtime probes. `refresh-handoff.py` rebuilds the patch and integrity receipts.

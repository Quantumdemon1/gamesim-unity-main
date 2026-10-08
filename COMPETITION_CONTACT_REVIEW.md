# Competition hand contact and readable endurance progress - 2026-10-05

This is a presentation/QA increment in the isolated integration worktree, not
live-project promotion, final art approval, desktop acceptance or completion of
the Unity port. The separate Wave C gameplay branch remains unmerged.

## Reproduced failures and cause

The closed `g22u1` full UMA configuration at `eda7312a` passed Edit2540/2540 and
UMA77/77, but general Play928/931: actual dice, endurance and signals captures
failed their hand-contact assertion. The controller retained11832 artifacts,
with no source drift, timeout, forced process stop or cleanup error. It remains
a FAILED baseline, never relabeled by later focused results.

Diagnostic commit `bab5de18` records actual arm/target geometry on failure.
`g22contact1` reproduced3/3 failures. `g22contact2` measured the actual finger
bones without changing the pose: wrist reach0.43653m, target distances0.50655-
0.53366m; middle knuckle0.08838m beyond the wrist, distal finger0.14088m beyond it.
The rig's Hand bone is its wrist pivot. Asking that pivot to touch the prop
incorrectly excluded the real hand's length. The recorded arm scales were
uniform0.76685, not evidence of a nonuniform-scale distortion.

## Every material change

- `CompetitionInstrumentPose.cs` (`f9b9412a`) now solves against the actual middle
  proximal bone for gripping and middle distal bone for pressing/rolling. A rig
  lacking those optional bones falls back to its actual wrist, never a synthetic
  offset. Only the existing upper/lower arm rotations are borrowed and restored.
  Bone positions/scales, actor root, hips, feet, collider, navigation, instrument
  contact positions and the3.5cm contact threshold are unchanged. Existing wrist-
  based apparatus placement still owns physical clearance; only the contact
  endpoint passed to the solver changes. This is not a new animation clip.
- The in-house captures independently measure those actual rig bones against the
  prop, rather than trusting HasContact alone. Failure diagnostics retain both
  wrist geometry and actual finger offsets/reach. Two new tests cover rotated/
  scaled hierarchies, no stretching/body movement, rotation restoration, and an
  unreachable prop that must retain a large honest error.
- The corrected contacts exposed a later screenshot-audit problem: `g22contact3`
  passed11/12, with endurance exceeding the existing20-million intersection work
  limit. `bc51bc6f` caches conservative64-triangle bounds over retained actual
  mesh faces. Empty groups are skipped; exact triangles and segment lengths still
  decide every obstruction, with original order/material coverage preserved.
  The same work limit now also counts renderer/block visits. It is not raised or
  reset per candidate. Vertex/triangle inventory caps,30 camera candidates,
  opaque visibility, text readability and100% progress-glyph clearance remain.
- A new dense32,768-triangle test compares accelerated queries with direct face
  queries (including back/edge/short segments), then checks1000 genuinely open
  sight lines within1 million total added visits. The old full-face scan would
  require32,768,000 triangle tests. Existing joined-bounds, transparent-submesh,
  non-readable-mesh, mirrored-glyph and real-occluder fixtures still run.
- The completed audit then exposed a real layout defect: `g22contact4` passed
  13/14, but the contestant's torso obscured the waist-high endurance progress
  plate from every allowed inspection eye. `782dd314` moves the EXISTING front/
  audience labels and their two backings above the fitted top brace. Default
  grip readout height is1.73m; fitted center tracks0.14m above the brace center.
  Other instrument families keep their prior readout position. No new prop,
  material, primitive, collider or external asset is added by this change.
- `CompetitionApparatus.cs` retains references to those two owned backings and
  shares its existing height-setting helper with the grip. New tests verify both
  faces, actual backing/brace separation, multiple reaches/heights/radii,
  unchanged attempt/RNG/root and the existing full station reservation.
- Four new Play tests in total; floor903 becomes907. Edit2540, UMA77 and pure1501
  are unchanged on this branch. When combining Wave C `dda012a6`, preserve all
  increments: expected floors Edit2928 / Play962 / UMA77 / pure1925. Full UMA
  general Play includes additional conditional cases beyond the NoUMA floor.
- No simulation rule, command, save schema, fixture/golden, caption, font asset,
  scene or migration changed. GAMESIM_UMA remains local-only and uncommitted.

## Closed evidence

Each focused run retained its exact before/after source manifests and an empty
input-drift report. Failures are retained, not counted as passes.

| Run | Source | Result | Meaning |
| --- | --- | --- | --- |
| g22contact1 | bab5de18 | 0/3, exit2 | Wrist-target failures reproduced |
| g22contact2 | bab5de18 + recorded diagnostic edit | 0/1, exit2 | Real finger offsets measured |
| g22contact3 | f9b9412a | 11/12, exit2 | Contacts pass; later audit work bound fails |
| g22contact4 | bc51bc6f | 13/14, exit2 | Audit completes; real progress occlusion exposed |
| g22contact5 | 782dd314 | 15/15, exit0 | Focused contacts, geometry, visibility and readout pass |

Final focused duration96.9926931s, zero failures/skips; root controller60122
closed0 after its final audit. The15 cases are the complete10-case apparatus
group, the new dense inspection case, and four actual in-house dice/endurance/
memory/signals cases. This is NOT a full suite or standalone-player result.

Retained final proof and9 native PNGs (15 files,14,427,557bytes):
`D:/CodexGamesimEvidence/integration-20261004/g22contact5-evidence`.
Original and copied hashes were compared; originals remain unchanged.

- Final XML SHA256 `7608775a776662b5d20afca852260b91c4e88094005fa1e9fdf4b2da40a4bd24`.
- Final summary SHA256 `125bc80fe6519be856b4cd8205ff993f778c593c05d5bbb49f7708d1609b117a`.
- Final input-drift SHA256 `84b1c14e68260082e3e79edff0a78f4ebf1942cfd6c593246044ec2149cbb87d`.
- Endurance world PNG SHA256 `be8370475cfc8d036f5754c6e46636b70ec43df3864ac6d433b01b6dcd3edf95`.
- Dice world PNG SHA256 `0584687794193923c9c651a06d47713d7b77cbd964216295ad01913877c6883c`.
- Signals world PNG SHA256 `c4ec5222a3e20066720ac220f38717a42b1c2322542cbccb835adfa308f9024a`.

Each implementation compiled8/8 fresh assemblies outside Unity before native
execution. Final `contact-readout-offline-01.log` SHA256
`40fc4e4a2c003baed55557f91043cd361ecfb9b6a575cf126191b6cc8053c759`;
output `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/6837648af55a4e5e872d88deac9be5d6`.
Compilation is not runtime evidence. No new pure/source-parity run is claimed.

Root reviewed the three final world images: hands reach the actual instruments,
and the endurance progress is legible above the contestant. These are1600x900
native test captures, not1080p performance proof, final hand-animation polish or
approval of the still-procedural competition props. Coherent real-asset completion
remains required. Independent reviewers are quota-unavailable; review was by root.

## Preservation and next gates

Live project edits/fonts/scenes/settings/recovery/saves/builds are untouched.
Before diagnostic execution,421 earlier root captures were copied and hash-checked
into `captures-before-contact-diagnostic-g22u1`; mixed historical provenance was
explicitly retained. Six contact3 and eight contact4 PNGs were likewise retained
before later runs. No delete, force-kill, remote publication or service change.

Next: full same-candidate UMA and NoUMA suites, then separate shipping/player
verification. Do not promote from a15-case focused pass. The staged economy/
reply work, E2 all-action balance, E4/E5, all four REQUIRED Wave D systems, real
assets, actual1920x1080 at60FPS GTX1060 profiling and human E1-E5 acceptance remain.

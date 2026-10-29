# Competition apparatus

`Assets/Gamesim/Runtime/Presentation/CompetitionApparatus.cs` is the canonical
source for these original, modular runtime instruments. It constructs its own
box mesh, owned materials, and named parts in metres under each leased house
anchor. It creates no collider, serialized scene object, navigation surface
or extra simulation rule.

## Authored geometry

Each named part can borrow an authored mesh in place of the box: 39 parts across
the five families, from the 20 October props batch
(`ArtSource/props/gamesim-props-20261020/GameSim-props-equipment-candidate-02`,
LicenseRef-GameSim-Original; its `ORIGINAL-ASSET-NOTICE.txt` is copied beside the
imported parts). The parts are
`Assets/Gamesim/Art/CompetitionKit/bb_comp_<Family>_<PartKind>.fbx`, each
normalised to the centred unit box the part scales, so only `MeshFilter.sharedMesh`
changes: transforms, materials, readouts, fitting and the clearance registries
stay as they are. `Gamesim/Competition/Build authored apparatus mesh kit`
(`CompetitionApparatusMeshKitAssets.Build`) checks every import and writes
`Assets/Gamesim/Resources/Gamesim/Competition/ApparatusMeshKit.asset`, which
`CompetitionApparatus.Create` loads. `CompetitionApparatusMeshKit.TryGetMesh`
takes a part only when its family, kind and reference dimensions match exactly.

**Why not `Art/Authored`.** A part is a centred unit block an instrument scales
into place, not a prop that stands on the floor, so it cannot meet the props'
floor contract (`AuthoredAssetImportTests.EveryAuthoredModelStandsOnTheFloorWithABbName`:
lowest point at y = 0), and that test's list of exceptions is not to grow. The
parts have their own folder and their own import rule,
`CompetitionApparatusMeshKitImporter`: the props' geometry settings (metres, no
import scale, the axis conversion baked once, not readable, no collider), with
no lightmap UVs (the apparatus is built at run time and never lightmapped) and
no materials (the apparatus owns every material it draws).

**The axis.** Unity design coordinates are (x width, y height, z depth), with
the contestant at -z of every part and the audience at +z. Exported -Z forward,
Y up with the transform baked (as `ArtSource/tools/bb_export.py` does), Blender
(x, y, z) arrives in Unity as (x, z, y): the house's measured map
(bb_set_kitchenrun, bb_set_comp_backdrop, the flight case 7fa9e69b). So the
generator sends a design point to Blender as `bvec(v) = (v[0], v[2], v[1])`
and it comes back unchanged. The batch's own map, `(v[0], -v[2], v[1])`, came
back as (x, y, -z): every part mirrored front to back, the housings' rear lids
and the keycaps' bases toward the contestant, and no bounds, identity or kit
check could tell. The parts here were regenerated with the corrected map; each
is the batch's part mirrored in z, vertex for vertex.
`CompetitionApparatusMeshKitPlayModeTests`' installed-kit case reads the
imported vertices and asserts the front of the twelve parts that have one.

Sources beside this README: `build_competition_art.py` (the Blender generator),
`part-manifest.json` (the 39 entries and their reference dimensions),
`competition_parts.blend` (the scene the generator saves) and
`authoring-receipt.json` (its record of the run: Blender 5.2.2, the generator's
and manifest's hashes, and each FBX's hash and triangle count). Regenerate
headless into a directory that does not exist yet, then copy `Parts/*.fbx` into
`Assets/Gamesim/Art/CompetitionKit/`:

    blender --background --factory-startup --python build_competition_art.py -- --manifest part-manifest.json --out <new directory>

A rerun reproduces the generator and manifest hashes in the receipt, but not the
FBX hashes. Blender writes the export time into each FBX, and it keys object ids
on Python's per-process string hash. So two runs differ in bytes while matching
node for node. Compare a rerun's geometry with the parts, not its hashes.

**A stale assembly placement.** The generator's Signals assembly (`placements`,
written to `Assemblies/bb_comp_Signals.glb` and its preview) still puts the
response pad at (0, 1.16, .39), in front of RIGHT, where the runtime had it
until the props batch's second review. The runtime now stands it beside the
cross, at (.43, 1.22, .455), with `FitPress` moving only its height. Nothing in
Unity reads the assemblies, and the receipt's `sourceSha256` pins the generator
byte for byte, so the placement changes at the next rebuild, with a new receipt.

`CompetitionApparatusMeshKitPlayModeTests` covers the borrowed geometry, and its
installed-kit case fails every suite when the shipped kit is missing or any part
of any family falls back to the box. The null-kit path is only for isolated
fixtures (`CompetitionApparatusPlayModeTests`).

| Definition | Instrument |
| --- | --- |
| Signal Sprint; Switchback Signals | Four-direction target console and response pad |
| House Memory; First Impressions | Sixteen-tile pair console |
| Hold Your Ground; Pressure Cooker | Raised grip handles, grip scale and pressure counterweight |
| Roll the Dice | Three physical dice in a raised tray |
| Word Scramble; Houseguest Scramble | Twelve-letter tray console |

The definition and its published mechanics stay authoritative. The player's
instrument reads the actual `MiniGameRun`. Memory reveals only the current
preview, matched pairs, and cards the player has flipped. Revealed tile names
match the board's symbol captions. Grip height/meter and pressure bands, lit
directions, landed dice and letter selection all follow that attempt. Other
entrants show Ready; no private performance or NPC input is fabricated.

Every approach finishes before raised apparatus becomes active. An instrument
faces beyond the final segment of the actor's complete native route. Capsule
clearance uses the nearest raised solid in its family, including the pad and
tray, and the actor's actual stopping offset from the anchor. The actor root,
anchor, collider, agent and simulation are not moved or reconfigured. Words are
gated under the existing HUD; solids remain as the stage. Releasing a stage
destroys its owned mesh/material instances.

Placement reserves the complete oriented family envelope, the actor, and the
native arrival tolerance before leasing a route. It queries every solid physics
layer, including Furniture, and checks the whole envelope against the yard floor
and other station/audience reservations. Raised grip fitting also has a bounded
shoulder/arm envelope; if the actual solids exceed their reservation, the attempt
returns to the briefing without moving the actor. Apparatus remains hidden until
every approach route finishes, so scenery cannot obstruct an entrant still
crossing the stage.

Only roots with an owned native stage route are excluded from the physics query.
An unleased, skipped or seated houseguest still occupies real space even when
their body clears the contestant capsule. After animation, the scoped contact
component fits the hidden geometry and calls the stage's clearance gate before
activation or bone contact. An unowned body entering the envelope cancels the
attempt and releases every stage owner without changing the seeded season.
The fitting component is owned independently of humanoid pose capability, so
primitive bodies receive the same clearance gate and release semantics.
Paused presentation retains only already-proven arrivals whose owned roots stay
stationary; the native arrival API still exclusively gates GO. During the ranked
finish plate the scenery can hide, but a new obstruction cannot cancel the
finished result's impending commit. The actual kept-roll fixture covers that
window with an unleased forward houseguest.

`CompetitionInstrumentPose` runs after the existing humanoid animation. It fits
the endurance bar from actual shoulder position and arm lengths, applies two
arm rotations to place the hands on the real handle, and gives a short right-arm
press on a real hit or roll. It owns no hip, foot or root transform. It detects a
replacement animator each frame and restores only unchanged bone rotations
that remain its own. Cancellation, pause and release end that contact. The
primitive fallback uses the same instrument/progress without a humanoid pose.

The dice read on both sides. Each value is in the instrument face's dark ink
(102138) on its paper die; the audience's copy stands on that die's own rear
face, in the part above the back rail, and moves with the die at every pedestal
fit. The roll readout under the tray comes down with the tray's underside at the
pedestal's lowest fits instead of sinking into the bed. The roll's press aims
0.15 m in front of a die's centre, clear of the 0.12 m its corners sweep while it
turns, so the fingers stop short of a rolling die: the press is a gesture, not a
touch. The consoles' audience-side keycaps sit flush on the housing's rear face,
and each key's audience letter stands 5.5 mm off its own cap, as the contestant's
does (it stood on a shared plane 20-25 mm off the caps).

**Follow-up: the counterweight bracket.** The endurance rig's counterweight
guide (`CounterweightGuide`, with the `Counterweight` on it) stands about 8.5 cm
behind the rig frame with nothing joining it to the brace or the uprights; in
the fitted rig its top rises 2.5 cm above the top brace, and a side view shows
it free in the air. The guide cannot simply move forward: the 0.17 m-deep
counterweight would then cut into the frame. The fix is a new part kind, a
bracket from the top brace's rear face to the guide's top (about
`(0, top, Front + .10)` at `(.05, .03, .12)`), with the guide capped at the
brace's top. A new kind is appended to `PartKind` and needs an authored part in
the kit, so it waits for the next authoring batch (review of 2026-10-25).

The assembly keeps its existing wide camera. Endurance's translucent play area
gets a closer three-quarter camera in play so the held arms and pressure rig can
be read. Reduced-motion behavior and the original camera release remain in
force.

## Verification and rendered review

`CompetitionApparatusPlayModeTests` covers all nine definitions, finite distinct
family geometry, no colliders, clearance at an actual stopping offset, word
gating, resource release, memory disclosure and repeated presentation reads
against an identically seeded control run. No new scoring implementation is
duplicated in the instrument.

The `EpisodePlayModeTests.Apparatus_InHouse...` tests use real station
arrival, actual mouse/keyboard game input, and cancel/release or a kept ranked
roll. Batch runs capture the actual director attempt and pose; they do not
substitute a fixture game or call a gameplay handler directly. With an active
humanoid they require hand error below 3.5 cm before the physical-interaction
capture. The normal UI and a temporary close review lens are read in the same
real input state, without an intervening slow frame changing either view into
a paused screen. Neither lens moves an actor, anchor, lease or game value.

Expected PNGs at the isolated project root:

- `competition-apparatus-mental-world.png` and `-mental-ui.png`
- `competition-apparatus-endurance-world.png` and `-endurance-ui.png`
- `competition-apparatus-signals-world.png` and `-signals-ui.png`
- `competition-apparatus-dice-world.png`, `-dice-ui.png`, `-dice-result.png`
- `competition-apparatus-words-ready-world.png` and `-words-ready-ui.png`
- Corresponding `words-running`, `words-paused` and `words-resumed` world/UI pairs
- `competition-apparatus-full-field-12-mental.png`, `-12-endurance.png`, `-12-luck.png`
- `competition-apparatus-full-field-16-mental.png`, `-16-endurance.png`, `-16-luck.png`
- `competition-apparatus-veto-audience-paused-world.png` and `-veto-audience-paused-ui.png`

The word fixture uses real Pause/Resume pointer input and real letter keys. Ready
and paused readouts show `?` on both console faces; a stopped timer, the same
puzzle and selected letters are asserted across resume. The largest valid regular
roster has twelve entrants; stored seasons support sixteen. Separate actual
save/reload fixtures stage both counts for mental, endurance and dice apparatus
around representative rear towers, a studio camera and a solid
that clears an actor capsule but would intersect its former forward console.
It verifies fitted meshes, complete reservations and neighbouring participant
clearance, then captures each full field. The sixteen-person fixture adds four
distinct valid contestants to the regular snapshot and validates the stored
season; it does not extend the production roster or change the schema.
The actual twelve-person veto fixture stages six competitors and six audience
members, pauses through the real pointer control and keeps the proven occupied
scenery visible while the native paused-arrival count remains zero.
These temporary solids complement the
integrated review with the actual authored house amenities.

Review the actual pictures for console clearance, readable front/back output,
hands touching the raised bar, shoulders/elbows, fingers, pad/dice contact,
the player's silhouette in the native endurance view, and the committed result.
These source and numerical checks do not certify visual quality; that gate
requires the serialized Unity run and rendered review on both body models.

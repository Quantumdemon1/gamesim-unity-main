# Competition apparatus

`Assets/Gamesim/Runtime/Presentation/CompetitionApparatus.cs` is the canonical
source for these original, modular runtime instruments. It constructs its own
box mesh, owned materials, and named parts in metres under each leased house
anchor. It creates no collider, serialized scene object, navigation surface,
dependency asset or extra simulation rule.

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
These temporary solids complement the
integrated review with the actual authored house amenities.

Review the actual pictures for console clearance, readable front/back output,
hands touching the raised bar, shoulders/elbows, fingers, pad/dice contact,
the player's silhouette in the native endurance view, and the committed result.
These source and numerical checks do not certify visual quality; that gate
requires the serialized Unity run and rendered review on both body models.

# Camera and navigation

What was broken, what was fixed, and what to build on it.

## What was actually wrong

Three separate faults presented as one complaint ("I can't zoom in").

**The wheel was doing something, just nothing you could see.** The rig read
`mouse.scroll.ReadValue().y * 0.0125f`. That multiplier is tuned for the Windows convention where
one wheel notch is a delta of 120, giving 1.5 metres a notch. Input System backends that normalise
scroll to about 1.0 per notch instead produce **0.0125 metres a notch** — one eightieth of a metre.
The code ran, the value changed, and the camera moved a distance no one could perceive. A control
that does nothing visible is indistinguishable from a control that is not wired up, which is why
the on-screen hint looked like a lie.

Fixed by folding both conventions onto a notch count before applying the step, so the same code
behaves identically either way rather than being correct on one backend by luck.

**There was no setting that could frame a person.** `minimumDistance` was 10 metres. In a house
whose rooms are 14 × 10, ten metres is still a whole-room shot. Even with a working wheel, "zoom in
on a houseguest" was not reachable. Now 4.

**Clicking a houseguest did nothing whatsoever.** The click handler tested for the player, then for
a walkable surface, and returned. A person is neither, so a click on one fell out of the bottom
with no effect and no feedback — the first gesture anybody tries.

## What it does now

| Gesture | Behaviour |
| --- | --- |
| Wheel | Zooms, 2 m per notch, 4 m to 34 m |
| Click a houseguest | Frames them at 7 m, head height, and follows them as they walk |
| Click the player | Same, and selects them |
| Click the floor | Releases the follow and walks the player there |
| `F` | Releases the follow and recenters on the player |
| WASD / arrows | Pans, releasing the follow — a pan asks to look elsewhere |
| Right-drag | Orbits, **without** releasing — turning around someone is the point |

Following is honoured under reduced motion. Conversation framing deliberately is not, because that
one is automatic and unrequested; this one is a direct answer to a click, so suppressing it would
withhold a result the viewer asked for. Reduced motion removes the easing, not the outcome.

## Phase 1 — make zoom feel like a camera rather than a slider

**Tie pitch to distance.** This is the single biggest "why does it feel wrong" item left. The rig
holds a fixed pitch and only orbit changes it, so zooming in gives a close-up still looking down at
55° — a top-down shot of the top of someone's head. Real dollhouse cameras ride a curve: steep and
overhead when pulled back, near eye level when pushed in. One curve from distance to pitch turns
zoom from a scale change into a move.

**Zoom toward the cursor, not the pivot.** Currently the wheel slides along the camera's own axis
toward whatever the pivot happens to be, so zooming in on something at the edge of frame pushes it
out of frame. Zooming toward the cursor is what every strategy and sim camera does and it removes
a constant small correction.

**Pull in on occlusion.** At 4 m the camera sits roughly 2 m back and 3 m up; inside a dressed room
that now holds 175 props, it will clip through furniture and wall stubs. A sphere-cast from focus
to camera, pulling the camera in to the first hit, is the standard fix. This is also the item most
likely to get fiddly, so it should be built with a visible debug draw.

## Phase 2 — make people the primary navigation target

The house is a set with six people in it. Navigation should be organised around them, not around
coordinates.

**The cast rail already lists all six.** Clicking a portrait there should focus that houseguest.
Far more discoverable than hitting a figure that is twenty pixels tall from the default distance,
and it works when they are behind a wall.

**Show what is being followed.** A ring under the subject, and a chip reading *Following Maya —
F to release*. Right now the state is invisible: a camera that follows with no indicator looks like
a camera that has stopped responding to pan.

**Tab to cycle.** Once following is a first-class mode, stepping through the cast is nearly free
and makes "what is everyone doing" a two-second sweep.

## Phase 3 — input completeness

- **Middle-drag and edge pan.** WASD alone is a keyboard-only pan on a mouse-driven game.
- **Gamepad.** The Input System is already the input path; the rig reads devices directly.
- **Move to an Input Actions asset.** The rig currently reads `Mouse.current` and `Keyboard.current`
  inline. That is why the wheel bug was invisible to tests — there is no seam to inject a synthetic
  scroll at. An actions asset makes the bindings rebindable *and* makes the camera testable.

## Phase 4 — polish

- **Duration-based easing.** The blend is an exponential rate, so framing approaches its target but
  never quite arrives and the time taken depends on the distance travelled. An ease-in-out over a
  fixed duration always lands, and lands predictably.
- **Name tags at close range**, fading out as you pull back.
- **Framing presets for ceremonies**, so the nomination and eviction shots are composed rather than
  inherited from wherever the player left the camera.

## What to watch

**The rig is not covered by tests in the place that broke.** `Camera_ReportsStartupFramingOverTime`
reports distance without grading it, which is deliberate, but nothing asserts that a wheel notch
changes the distance at all — the whole bug lived in a gap no test looks at. Phase 3's actions asset
is what makes that testable; until then, camera input is verified by hand.

**Every change here touches accessibility.** Reduced motion has real assertions behind it, and the
follow mode had to make an explicit decision about it. Anything added to the camera needs the same
decision made deliberately rather than inherited.

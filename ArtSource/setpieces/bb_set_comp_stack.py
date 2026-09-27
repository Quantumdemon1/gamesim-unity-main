"""The stacking prop: a peg on a plinth with four discs threaded on it.

This is what the houseguests are actually doing in mockup-05, and it is the only prop in the yard
that says what the competition IS. The lanes and the gates say "a course"; the stack says "put
these in order before they do". A competition the player watches without knowing the task is a
crowd scene.

Four discs, because the challenge card in the mockup names four colours; threaded loosely enough
that the stack reads as stackable rather than as a solid cone. The discs are authored white and
named for their glow, and the placement step tints a lane's set to its colour. 1.35 m to the top
of the peg, so a 1.8 m houseguest works at chest height. Origin on the floor under the centre.
Furniture, so no collider - the yard's NavMesh was baked without it and a prop in the walk path
would cost seventeen PlayMode tests.

    blender --background --python ArtSource/setpieces/bb_set_comp_stack.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_comp_stack")

ink = X.material("ink_stack", (0.06, 0.06, 0.08), metallic=0.3, roughness=0.5)
steel = X.material("frame_steel", (0.62, 0.64, 0.68), metallic=0.9, roughness=0.3)
disc = X.material("neon_disc", (0.90, 0.94, 1.00), roughness=0.3,
                  emission=(1.0, 1.0, 1.0), emission_strength=2.6)

PLINTH = 0.30         # how high the plinth stands
PEG = 1.05            # the peg above it
DISC_H = 0.10         # one disc
GAP = 0.015           # daylight between discs, so the stack reads as separable

parts = [
    B.box("plinth", (0.95, 0.95, PLINTH), (0.0, 0.0, PLINTH / 2), ink),
    # A lit reveal under the plinth's lip, the way the podium has one: it lifts the prop off a
    # dark deck instead of letting it merge into it.
    B.box("reveal", (0.99, 0.99, 0.02), (0.0, 0.0, PLINTH - 0.03), disc),
    B.cylinder("peg", 0.045, PEG, (0.0, 0.0, PLINTH), steel, segments=16),
]

z = PLINTH + 0.02
for i in range(4):
    parts.append(B.ring("disc_%d" % i, 0.30, 0.055, DISC_H, (0.0, 0.0, z), disc, segments=28))
    z += DISC_H + GAP

stack = B.join(parts, "bb_set_comp_stack")
X.link_only(stack, coll)
path = X.output_path("bb_set_comp_stack.fbx")
X.export_collection(coll, path)
X.report(coll, path)

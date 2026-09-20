"""The competition backdrop: the dark panelled wall the neon stage is built against.

mockup-05's yard is not a patio with lights on it - it is a stage, and what makes it one is the
wall behind. Dark panels with cool vertical tubes at intervals, a lit line along the base, and the
lettering hung on it. Without the wall the gates and the lanes float in front of whatever the yard
already had; with it they read as a built set.

Twenty-eight metres, the width of the competition floor, and 3 m tall - taller than the 2.6 m
gates in front of it, so the gates read against it rather than over it. Eight tubes at 3.5 m
centres, which lands them between the three lanes rather than on them. Panel seams are cut as
shallow reveals rather than modelled as separate panels: at the competition camera's distance a
groove and a gap look the same and one costs twenty times the triangles.

Origin at the centre of the base, on the floor, with the lit face toward -y (Unity -z, the house
and the camera). A backdrop, so no collider: it stands outside the walk area.

    blender --background --python ArtSource/setpieces/bb_set_comp_backdrop.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_comp_backdrop")

panel = X.material("ink_backdrop", (0.05, 0.055, 0.07), metallic=0.2, roughness=0.65)
reveal = X.material("ink_reveal", (0.02, 0.02, 0.03), roughness=0.9)
neon = X.material("neon_backdrop", (0.62, 0.80, 1.00), roughness=0.25,
                  emission=(0.55, 0.78, 1.0), emission_strength=3.0)

WIDTH = 28.0
HEIGHT = 3.0
DEPTH = 0.14
TUBE = 0.06
TUBE_H = 2.40
TUBES = 8
SEAM = 0.03           # how proud the reveal strips stand, so they catch a shadow line

parts = [
    B.box("wall", (WIDTH, DEPTH, HEIGHT), (0.0, 0.0, HEIGHT / 2), panel),
    # A lit line along the foot, which is what lifts the wall off a dark deck.
    B.box("plinth_glow", (WIDTH, SEAM, 0.05), (0.0, -DEPTH / 2 - SEAM / 2, 0.06), neon),
    # A horizontal reveal at head height breaks the 28 m into something with a scale to it.
    B.box("reveal_high", (WIDTH, SEAM, 0.04), (0.0, -DEPTH / 2 - SEAM / 2, 2.05), reveal),
]

# The tubes, spread evenly and inset from the ends so neither one lands on a corner.
span = WIDTH - 3.5
for i in range(TUBES):
    x = -span / 2 + span * i / float(TUBES - 1)
    parts.append(B.box("tube_%d" % i, (TUBE, SEAM, TUBE_H),
                       (x, -DEPTH / 2 - SEAM / 2, 0.30 + TUBE_H / 2), neon))
    # Each tube sits in a slightly wider dark reveal, so it reads as set into the wall.
    parts.append(B.box("tube_slot_%d" % i, (TUBE * 3.2, SEAM * 0.6, TUBE_H + 0.2),
                       (x, -DEPTH / 2 - SEAM * 0.3, 0.20 + (TUBE_H + 0.2) / 2), reveal))

wall = B.join(parts, "bb_set_comp_backdrop")
X.link_only(wall, coll)
path = X.output_path("bb_set_comp_backdrop.fbx")
X.export_collection(coll, path)
X.report(coll, path)

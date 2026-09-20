"""The disc crate: the open box of spare discs beside each lane.

Small, and it earns its place by answering a question the stack raises. A peg with four discs on
it says "put these in order"; a crate of loose discs beside it says where the next ones come from,
which is the difference between a prop and a task. mockup-05 has one per lane, low and dark, just
inside the lane's edge.

Open-topped, 1.05 x 0.85 x 0.42 m - sized from the disc rather than guessed, because a crate
narrower than the thing it holds puts a disc through its own wall. The discs are the stack's own
0.60 m across, so the cavity is 0.95 x 0.75 and the deepest offset still clears the side by 5 cm. The discs are named for their
glow and tinted to the lane where they stand, like the stack's. Origin on the floor under the
centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_comp_crate.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_comp_crate")

ink = X.material("ink_crate", (0.07, 0.07, 0.09), metallic=0.2, roughness=0.6)
disc = X.material("neon_disc", (0.90, 0.94, 1.00), roughness=0.3,
                  emission=(1.0, 1.0, 1.0), emission_strength=2.2)

W, D, H = 1.05, 0.85, 0.42
T = 0.05              # how thick the walls are

parts = [
    B.box("floor", (W, D, T), (0.0, 0.0, T / 2), ink),
    B.box("wall_n", (W, T, H), (0.0, D / 2 - T / 2, H / 2), ink),
    B.box("wall_s", (W, T, H), (0.0, -D / 2 + T / 2, H / 2), ink),
    B.box("wall_e", (T, D - T * 2, H), (W / 2 - T / 2, 0.0, H / 2), ink),
    B.box("wall_w", (T, D - T * 2, H), (-W / 2 + T / 2, 0.0, H / 2), ink),
    # Three discs lying in the bottom, offset so they read as dropped rather than placed.
    B.ring("spare_0", 0.30, 0.055, 0.09, (-0.12, -0.05, T), disc, segments=24),
    B.ring("spare_1", 0.30, 0.055, 0.09, (0.11, 0.04, T + 0.09), disc, segments=24),
    B.ring("spare_2", 0.30, 0.055, 0.09, (0.00, -0.02, T + 0.18), disc, segments=24),
]

crate = B.join(parts, "bb_set_comp_crate")
X.link_only(crate, coll)
path = X.output_path("bb_set_comp_crate.fbx")
X.export_collection(coll, path)
X.report(coll, path)

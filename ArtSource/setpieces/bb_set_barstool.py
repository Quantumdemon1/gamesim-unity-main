"""A bar stool: a round padded seat on a brass column with a foot ring.

Replaces the kit's stoolBar wherever the plan puts one - the kitchen's three, the game room's
three at its bar. 0.38 m across, 0.78 m to the seat. Origin at the floor under the column.
Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_barstool.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_barstool")

brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
cushion = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.6)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

SEAT = 0.78
parts = [
    B.cylinder("base", 0.17, 0.02, (0.0, 0.0, 0.0), brass, segments=24),
    B.cylinder("column", 0.025, SEAT - 0.06, (0.0, 0.0, 0.02), brass, segments=12),
    B.ring("foot_ring", 0.16, 0.14, 0.02, (0.0, 0.0, 0.24), brass, segments=24),
    B.cylinder("seat_plate", 0.15, 0.02, (0.0, 0.0, SEAT - 0.06), ink, segments=24),
    B.cylinder("seat", 0.19, 0.06, (0.0, 0.0, SEAT - 0.06), cushion, segments=24),
]
stool = B.join(parts, "bb_set_barstool")
X.link_only(stool, coll)

path = X.output_path("bb_set_barstool.fbx")
X.export_collection(coll, path)
X.report(coll, path)

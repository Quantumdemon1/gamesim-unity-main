"""A single bed: an oak frame with a headboard, a linen mattress, a teal throw and a pillow.

Stands in for the kit's bedSingle. 0.95 x 2.0 m, 0.55 m to the mattress top, 0.85 m at the
headboard, which is at -y. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_bed.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_bed")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
linen = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.9)
throw = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.85)

W, L = 0.95, 2.0
FRAME_Z, MAT_T = 0.30, 0.18
parts = [
    B.box("frame", (W, L, 0.12), (0.0, 0.0, FRAME_Z - 0.06), oak),
    B.box("headboard", (W, 0.05, 0.85), (0.0, -L / 2 + 0.025, 0.425), oak),
    B.box("mattress", (W - 0.06, L - 0.10, MAT_T), (0.0, 0.02, FRAME_Z + MAT_T / 2), linen),
    B.box("throw", (W - 0.02, L * 0.55, 0.04), (0.0, L / 2 - L * 0.55 / 2 - 0.05, FRAME_Z + MAT_T + 0.02), throw),
    B.box("pillow", (W - 0.30, 0.34, 0.10), (0.0, -L / 2 + 0.26, FRAME_Z + MAT_T + 0.05), linen),
]
for i, (x, y) in enumerate([(-W / 2 + 0.05, -L / 2 + 0.05), (W / 2 - 0.05, -L / 2 + 0.05),
                             (-W / 2 + 0.05, L / 2 - 0.05), (W / 2 - 0.05, L / 2 - 0.05)]):
    parts.append(B.box("leg_%d" % i, (0.06, 0.06, FRAME_Z - 0.12), (x, y, (FRAME_Z - 0.12) / 2), oak))

bed = B.join(parts, "bb_set_bed")
X.link_only(bed, coll)
path = X.output_path("bb_set_bed.fbx")
X.export_collection(coll, path)
X.report(coll, path)

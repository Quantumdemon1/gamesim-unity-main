"""A double bed: the single bed's shape at 1.6 m wide, with two pillows. 1.6 x 2.0 m, 0.85 m at the headboard.

Stands in for the kit's bedDouble. Headboard at -y. Origin at the floor under the centre.
Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_doublebed.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_doublebed")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
linen = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.9)
throw = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.85)

W, L = 1.6, 2.0
FRAME_Z, MAT_T = 0.30, 0.18
parts = [
    B.box("frame", (W, L, 0.12), (0.0, 0.0, FRAME_Z - 0.06), oak),
    B.box("headboard", (W, 0.05, 0.85), (0.0, -L / 2 + 0.025, 0.425), oak),
    B.box("mattress", (W - 0.06, L - 0.10, MAT_T), (0.0, 0.02, FRAME_Z + MAT_T / 2), linen),
    B.box("throw", (W - 0.02, L * 0.55, 0.04), (0.0, L / 2 - L * 0.55 / 2 - 0.05, FRAME_Z + MAT_T + 0.02), throw),
    B.box("pillow_l", (W * 0.42, 0.34, 0.10), (-W * 0.24, -L / 2 + 0.26, FRAME_Z + MAT_T + 0.05), linen),
    B.box("pillow_r", (W * 0.42, 0.34, 0.10), (W * 0.24, -L / 2 + 0.26, FRAME_Z + MAT_T + 0.05), linen),
]
for i, (x, y) in enumerate([(-W / 2 + 0.05, -L / 2 + 0.05), (W / 2 - 0.05, -L / 2 + 0.05),
                             (-W / 2 + 0.05, L / 2 - 0.05), (W / 2 - 0.05, L / 2 - 0.05)]):
    parts.append(B.box("leg_%d" % i, (0.06, 0.06, FRAME_Z - 0.12), (x, y, (FRAME_Z - 0.12) / 2), oak))

bed = B.join(parts, "bb_set_doublebed")
X.link_only(bed, coll)
path = X.output_path("bb_set_doublebed.fbx")
X.export_collection(coll, path)
X.report(coll, path)

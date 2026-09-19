"""An armchair: the sofa's shape at one seat, coral over teal, on brass feet.

Stands in for the kit's loungeChairRelax and loungeDesignChair. 0.9 m wide, 0.85 m deep,
0.88 m tall (the taller of the two rows' heights; the other scales it down a hair). Back at +y.
Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_armchair.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_armchair")

velvet = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)
cushion = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.6)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, D, H = 0.9, 0.85, 0.88
SEAT = 0.42
parts = [
    B.box("base", (W, D, SEAT - 0.12), (0.0, 0.0, 0.12 + (SEAT - 0.12) / 2), velvet),
    B.box("back", (W, 0.20, H - 0.12), (0.0, D / 2 - 0.10, 0.12 + (H - 0.12) / 2), velvet),
    B.box("arm_l", (0.18, D - 0.05, 0.26), (-W / 2 + 0.09, -0.02, SEAT + 0.07), velvet),
    B.box("arm_r", (0.18, D - 0.05, 0.26), (W / 2 - 0.09, -0.02, SEAT + 0.07), velvet),
    B.box("seat", (W - 0.40, D - 0.28, 0.12), (0.0, -0.06, SEAT), cushion),
    B.box("back_cushion", (W - 0.42, 0.14, 0.32), (0.0, D / 2 - 0.27, SEAT + 0.18), cushion),
]
for i, (x, y) in enumerate([(-W / 2 + 0.10, -D / 2 + 0.10), (W / 2 - 0.10, -D / 2 + 0.10),
                             (-W / 2 + 0.10, D / 2 - 0.10), (W / 2 - 0.10, D / 2 - 0.10)]):
    parts.append(B.cylinder("foot_%d" % i, 0.03, 0.12, (x, y, 0.0), brass, segments=10))

chair = B.join(parts, "bb_set_armchair")
X.link_only(chair, coll)
path = X.output_path("bb_set_armchair.fbx")
X.export_collection(coll, path)
X.report(coll, path)

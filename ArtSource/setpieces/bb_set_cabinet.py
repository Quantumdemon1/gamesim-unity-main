"""A kitchen cabinet unit: one door under a stone counter, the kitchen run's idiom at 0.6 m wide.

Stands in for the kit's kitchenCabinet, which the furnishing pass repeats along a counter. 0.6 x
0.62 x 0.92 m. Door at -y. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_cabinet.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_cabinet")

cream = X.material("cabinet_cream", (0.88, 0.85, 0.78), roughness=0.5)
stone = X.material("counter_stone", (0.22, 0.24, 0.27), roughness=0.3)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, D, H = 0.6, 0.62, 0.92
TOP = 0.04
parts = [
    B.box("plinth", (W, D - 0.08, 0.10), (0.0, 0.04, 0.05), ink),
    B.box("carcass", (W, D, H - TOP - 0.10), (0.0, 0.0, 0.10 + (H - TOP - 0.10) / 2), cream),
    B.box("counter", (W, D + 0.02, TOP), (0.0, 0.0, H - TOP / 2), stone),
    B.box("door", (W - 0.03, 0.02, H - TOP - 0.16), (0.0, -D / 2 - 0.01, 0.10 + (H - TOP - 0.10) / 2), cream),
    B.box("handle", (0.02, 0.02, 0.14), (W / 2 - 0.08, -D / 2 - 0.03, H * 0.55), brass),
]
cabinet = B.join(parts, "bb_set_cabinet")
X.link_only(cabinet, coll)
path = X.output_path("bb_set_cabinet.fbx")
X.export_collection(coll, path)
X.report(coll, path)

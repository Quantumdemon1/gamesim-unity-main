"""A pool lounger: brass frame, coral cushion, backrest raised to 50 degrees.

0.65 x 1.9 m, origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_lounger.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_lounger")

brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
coral = X.material("cushion_coral", (0.95, 0.45, 0.40), roughness=0.8)

W, L = 0.65, 1.9
LEG, SEAT_Z = 0.30, 0.33

parts = [
    B.box("leg_fl", (0.05, 0.05, LEG), (W / 2 - 0.05, L / 2 - 0.08, LEG / 2), brass),
    B.box("leg_fr", (0.05, 0.05, LEG), (-W / 2 + 0.05, L / 2 - 0.08, LEG / 2), brass),
    B.box("leg_bl", (0.05, 0.05, LEG), (W / 2 - 0.05, -L / 2 + 0.08, LEG / 2), brass),
    B.box("leg_br", (0.05, 0.05, LEG), (-W / 2 + 0.05, -L / 2 + 0.08, LEG / 2), brass),
    B.box("frame", (W, L, 0.06), (0.0, 0.0, SEAT_Z), brass),
    B.box("cushion", (W - 0.06, L * 0.62, 0.08), (0.0, -L * 0.19 + 0.0, SEAT_Z + 0.07), coral),
    B.tilted_box("backrest", (W - 0.06, 0.08, L * 0.36), (0.0, L * 0.12, SEAT_Z + 0.03), -50.0, coral),
]
lounger = B.join(parts, "bb_set_lounger")
X.link_only(lounger, coll)

path = X.output_path("bb_set_lounger.fbx")
X.export_collection(coll, path)
X.report(coll, path)

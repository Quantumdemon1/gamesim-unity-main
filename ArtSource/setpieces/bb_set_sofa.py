"""A three-seat sofa: velvet over a brass-footed plinth, rolled arms, three seat cushions.

Stands in for the kit's loungeDesignSofa and loungeSofaCorner wherever a room has one. 2.2 m
long, 0.85 m deep, 0.78 m tall - the height the plan's sofa rows ask for, so it lands at its own
scale. The back is at +y (Unity +z); yaw 0 faces -z. Origin at the floor under the centre.
Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sofa.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_sofa")

velvet = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)
cushion = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.6)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

L, D, H = 2.2, 0.85, 0.78
SEAT = 0.42
parts = [
    B.box("plinth", (L - 0.10, D - 0.10, 0.06), (0.0, 0.0, 0.09), velvet),
    B.box("base", (L, D, SEAT - 0.12 - 0.06), (0.0, 0.0, 0.12 + (SEAT - 0.18) / 2), velvet),
    B.box("back", (L, 0.20, H - 0.12), (0.0, D / 2 - 0.10, 0.12 + (H - 0.12) / 2), velvet),
    B.box("arm_l", (0.18, D - 0.05, 0.28), (-L / 2 + 0.09, -0.02, SEAT + 0.08), velvet),
    B.box("arm_r", (0.18, D - 0.05, 0.28), (L / 2 - 0.09, -0.02, SEAT + 0.08), velvet),
]
for i, x in enumerate((-0.67, 0.0, 0.67)):
    parts.append(B.box("seat_%d" % i, (0.62, D - 0.28, 0.12), (x, -0.06, SEAT - 0.06 + 0.06), cushion))
    parts.append(B.box("back_cushion_%d" % i, (0.60, 0.14, 0.30), (x, D / 2 - 0.27, SEAT + 0.15), cushion))
for i, (x, y) in enumerate([(-L / 2 + 0.12, -D / 2 + 0.12), (L / 2 - 0.12, -D / 2 + 0.12),
                             (-L / 2 + 0.12, D / 2 - 0.12), (L / 2 - 0.12, D / 2 - 0.12)]):
    parts.append(B.cylinder("foot_%d" % i, 0.03, 0.09, (x, y, 0.0), brass, segments=10))

sofa = B.join(parts, "bb_set_sofa")
X.link_only(sofa, coll)
path = X.output_path("bb_set_sofa.fbx")
X.export_collection(coll, path)
X.report(coll, path)

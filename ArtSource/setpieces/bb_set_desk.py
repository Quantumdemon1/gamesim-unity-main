"""A desk: an oak top on a brass frame with a drawer pedestal on the right. 1.3 x 0.65 x 0.74 m.

Stands in for the kit's desk. Front at -y. Origin at the floor under the centre. Furniture,
so no collider.

    blender --background --python ArtSource/setpieces/bb_set_desk.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_desk")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

L, D, H = 1.3, 0.65, 0.74
parts = [
    B.box("top", (L, D, 0.035), (0.0, 0.0, H - 0.0175), oak),
    B.box("pedestal", (0.42, D - 0.06, H - 0.035 - 0.08), (L / 2 - 0.23, 0.0, 0.08 + (H - 0.115) / 2), ink),
    B.box("leg_a", (0.03, 0.03, H - 0.035), (-L / 2 + 0.05, -D / 2 + 0.05, (H - 0.035) / 2), brass),
    B.box("leg_b", (0.03, 0.03, H - 0.035), (-L / 2 + 0.05, D / 2 - 0.05, (H - 0.035) / 2), brass),
    B.box("stretcher", (0.03, D - 0.10, 0.03), (-L / 2 + 0.05, 0.0, 0.12), brass),
]
for i in range(3):
    z = 0.12 + i * 0.19
    parts.append(B.box("drawer_%d" % i, (0.36, 0.012, 0.15), (L / 2 - 0.23, -D / 2 + 0.03 - 0.006, z + 0.075), oak))
    parts.append(B.box("pull_%d" % i, (0.12, 0.012, 0.012), (L / 2 - 0.23, -D / 2 + 0.03 - 0.02, z + 0.075), brass))

desk = B.join(parts, "bb_set_desk")
X.link_only(desk, coll)
path = X.output_path("bb_set_desk.fbx")
X.export_collection(coll, path)
X.report(coll, path)

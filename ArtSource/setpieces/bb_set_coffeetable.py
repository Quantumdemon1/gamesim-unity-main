"""A coffee table: an oak slab on a brass frame with a lower shelf. 1.2 x 0.6 x 0.42 m.

Stands in for the kit's tableCoffee. Origin at the floor under the centre. Furniture, so no
collider.

    blender --background --python ArtSource/setpieces/bb_set_coffeetable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_coffeetable")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

L, D, H = 1.2, 0.6, 0.42
parts = [
    B.box("top", (L, D, 0.04), (0.0, 0.0, H - 0.02), oak),
    B.box("shelf", (L - 0.16, D - 0.16, 0.02), (0.0, 0.0, 0.14), ink),
    B.box("rail_l", (0.02, D - 0.10, 0.02), (-L / 2 + 0.05, 0.0, H - 0.06), brass),
    B.box("rail_r", (0.02, D - 0.10, 0.02), (L / 2 - 0.05, 0.0, H - 0.06), brass),
]
for i, (x, y) in enumerate([(-L / 2 + 0.05, -D / 2 + 0.05), (L / 2 - 0.05, -D / 2 + 0.05),
                             (-L / 2 + 0.05, D / 2 - 0.05), (L / 2 - 0.05, D / 2 - 0.05)]):
    parts.append(B.box("leg_%d" % i, (0.025, 0.025, H - 0.04), (x, y, (H - 0.04) / 2), brass))

table = B.join(parts, "bb_set_coffeetable")
X.link_only(table, coll)
path = X.output_path("bb_set_coffeetable.fbx")
X.export_collection(coll, path)
X.report(coll, path)

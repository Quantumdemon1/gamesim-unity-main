"""A side table with two drawers, oak on brass legs. 0.5 x 0.4 x 0.62 m.

Stands in for the kit's sideTableDrawers and cabinetBedDrawer; the rows scale the bedside ones
to their 0.52. Front at -y. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sidetable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_sidetable")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

W, D, H = 0.5, 0.4, 0.62
parts = [
    B.box("body", (W, D, H - 0.18), (0.0, 0.0, 0.18 + (H - 0.18) / 2), oak),
    B.box("top", (W + 0.03, D + 0.03, 0.025), (0.0, 0.0, H - 0.0125), ink),
    B.box("drawer_top", (W - 0.06, 0.015, 0.16), (0.0, -D / 2 - 0.0075, H - 0.11), oak),
    B.box("drawer_bottom", (W - 0.06, 0.015, 0.16), (0.0, -D / 2 - 0.0075, H - 0.30), oak),
    B.box("pull_top", (0.14, 0.015, 0.015), (0.0, -D / 2 - 0.02, H - 0.11), brass),
    B.box("pull_bottom", (0.14, 0.015, 0.015), (0.0, -D / 2 - 0.02, H - 0.30), brass),
]
for i, (x, y) in enumerate([(-W / 2 + 0.04, -D / 2 + 0.04), (W / 2 - 0.04, -D / 2 + 0.04),
                             (-W / 2 + 0.04, D / 2 - 0.04), (W / 2 - 0.04, D / 2 - 0.04)]):
    parts.append(B.cylinder("leg_%d" % i, 0.015, 0.18, (x, y, 0.0), brass, segments=8))

table = B.join(parts, "bb_set_sidetable")
X.link_only(table, coll)
path = X.output_path("bb_set_sidetable.fbx")
X.export_collection(coll, path)
X.report(coll, path)

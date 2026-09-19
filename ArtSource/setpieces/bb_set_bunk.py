"""A bunk bed: two single beds in one oak frame with a ladder, 1.45 m tall - under the wall.

Stands in for the kit's bedBunk. 0.95 x 2.0 m; the ladder is on the +x side at the foot. The
headboard end is at -y. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_bunk.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_bunk")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
linen = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.9)
throw_a = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.85)
throw_b = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.85)

W, L, H = 0.95, 2.0, 1.45
MAT_T = 0.14
parts = []
for i, (z, throw) in enumerate(((0.26, throw_a), (0.94, throw_b))):
    parts.append(B.box("frame_%d" % i, (W, L, 0.08), (0.0, 0.0, z - 0.04), oak))
    parts.append(B.box("mattress_%d" % i, (W - 0.06, L - 0.10, MAT_T), (0.0, 0.0, z + MAT_T / 2), linen))
    parts.append(B.box("throw_%d" % i, (W - 0.02, L * 0.5, 0.03), (0.0, L / 4, z + MAT_T + 0.015), throw))
    parts.append(B.box("pillow_%d" % i, (W - 0.32, 0.32, 0.08), (0.0, -L / 2 + 0.24, z + MAT_T + 0.04), linen))
    parts.append(B.box("rail_%d" % i, (0.04, L, 0.04), (-W / 2 + 0.02, 0.0, z + MAT_T + 0.10), oak))
for i, (x, y) in enumerate([(-W / 2 + 0.04, -L / 2 + 0.04), (W / 2 - 0.04, -L / 2 + 0.04),
                             (-W / 2 + 0.04, L / 2 - 0.04), (W / 2 - 0.04, L / 2 - 0.04)]):
    parts.append(B.box("post_%d" % i, (0.08, 0.08, H), (x, y, H / 2), oak))
parts.append(B.box("head_panel", (W, 0.04, 0.5), (0.0, -L / 2 + 0.02, 0.6), oak))
# The ladder, up the +x side at the foot.
for i in range(4):
    parts.append(B.box("rung_%d" % i, (0.04, 0.30, 0.03), (W / 2 + 0.02, L / 2 - 0.25, 0.30 + i * 0.22), oak))
parts.append(B.box("ladder_rail_a", (0.03, 0.03, 1.0), (W / 2 + 0.02, L / 2 - 0.10, 0.62), oak))
parts.append(B.box("ladder_rail_b", (0.03, 0.03, 1.0), (W / 2 + 0.02, L / 2 - 0.40, 0.62), oak))

bunk = B.join(parts, "bb_set_bunk")
X.link_only(bunk, coll)
path = X.output_path("bb_set_bunk.fbx")
X.export_collection(coll, path)
X.report(coll, path)

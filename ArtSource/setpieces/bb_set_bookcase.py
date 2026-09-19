"""An open bookcase: four shelves in an ink carcass with books already on them.

Stands in for the kit's bookcaseOpen. 0.8 m wide, 0.32 m deep, 1.45 m tall (the plan's rows
scale the shorter ones down). The books are part of the piece, so a room that had "books" rows
on its bookcase keeps them and gets more. Back at +y. Origin at the floor under the centre.
Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_bookcase.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_bookcase")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
spines = [X.material("book_teal", (0.10, 0.36, 0.40)), X.material("book_coral", (0.86, 0.38, 0.32)),
          X.material("book_brass", (0.78, 0.62, 0.28)), X.material("book_linen", (0.85, 0.80, 0.68))]

W, D, H = 0.8, 0.32, 1.45
T = 0.025
parts = [
    B.box("side_l", (T, D, H), (-W / 2 + T / 2, 0.0, H / 2), ink),
    B.box("side_r", (T, D, H), (W / 2 - T / 2, 0.0, H / 2), ink),
    B.box("back", (W, 0.012, H), (0.0, D / 2 - 0.006, H / 2), ink),
    B.box("top", (W, D, T), (0.0, 0.0, H - T / 2), ink),
]
shelf_z = [0.06, 0.40, 0.74, 1.08]
for i, z in enumerate(shelf_z):
    parts.append(B.box("shelf_%d" % i, (W - 2 * T, D - 0.02, T), (0.0, 0.0, z + T / 2), oak))
    # A run of books, leaning nowhere, in four widths.
    x = -W / 2 + T + 0.03
    k = 0
    while x < W / 2 - T - 0.08:
        w = (0.03, 0.045, 0.035, 0.05)[k % 4]
        h = (0.22, 0.26, 0.20, 0.24)[k % 4]
        parts.append(B.box("book_%d_%d" % (i, k), (w, D - 0.10, h), (x + w / 2, -0.02, z + T + h / 2), spines[(i + k) % 4]))
        x += w + 0.006
        k += 1
        if i == 1 and k == 9:
            break   # a gap on the second shelf, with a bookend
    if i == 1:
        parts.append(B.box("bookend", (0.03, 0.12, 0.12), (x + 0.02, -0.02, z + T + 0.06), oak))

case = B.join(parts, "bb_set_bookcase")
X.link_only(case, coll)
path = X.output_path("bb_set_bookcase.fbx")
X.export_collection(coll, path)
X.report(coll, path)

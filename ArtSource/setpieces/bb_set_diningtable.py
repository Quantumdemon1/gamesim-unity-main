"""The long dining table: where the house does most of its arguing.

A single plank top on two trestle legs with a stretcher between them, sized for sixteen chairs -
seven a side and one at each end. 4.8 x 1.1 m, 0.76 m high, origin at the floor under the centre.
Furniture, so no collider; the chairs are their own asset (bb_set_diningchair).

    blender --background --python ArtSource/setpieces/bb_set_diningtable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_diningtable")

oak = X.material("table_oak", (0.55, 0.36, 0.20), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

L, W, H, TOP = 4.8, 1.1, 0.76, 0.06
LEG_IN = 0.55   # trestles sit this far in from each end

parts = [
    B.box("top", (L, W, TOP), (0.0, 0.0, H - TOP / 2), oak),
    # Two trestles: a slab foot, an upright, and a bearer under the top.
    B.box("foot_a", (0.12, W - 0.30, 0.06), (-(L / 2 - LEG_IN), 0.0, 0.03), brass),
    B.box("foot_b", (0.12, W - 0.30, 0.06), ((L / 2 - LEG_IN), 0.0, 0.03), brass),
    B.box("post_a", (0.08, 0.08, H - TOP - 0.06), (-(L / 2 - LEG_IN), 0.0, 0.06 + (H - TOP - 0.06) / 2), brass),
    B.box("post_b", (0.08, 0.08, H - TOP - 0.06), ((L / 2 - LEG_IN), 0.0, 0.06 + (H - TOP - 0.06) / 2), brass),
    B.box("bearer_a", (0.10, W - 0.30, 0.05), (-(L / 2 - LEG_IN), 0.0, H - TOP - 0.025), brass),
    B.box("bearer_b", (0.10, W - 0.30, 0.05), ((L / 2 - LEG_IN), 0.0, H - TOP - 0.025), brass),
    B.box("stretcher", (L - 2 * LEG_IN, 0.06, 0.06), (0.0, 0.0, 0.20), brass),
]
table = B.join(parts, "bb_set_diningtable")
X.link_only(table, coll)

path = X.output_path("bb_set_diningtable.fbx")
X.export_collection(coll, path)
X.report(coll, path)

"""A round table: an oak top on a brass pedestal with a weighted foot. 1.5 m across, 0.78 m tall.

Stands in for the kit's tableRound, at the nomination room's centre with the dining chairs
around it. Origin at the floor under the pedestal. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_roundtable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_roundtable")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

R, H = 0.75, 0.78
parts = [
    B.cylinder("foot", 0.32, 0.04, (0.0, 0.0, 0.0), ink, segments=32),
    B.cylinder("pedestal", 0.07, H - 0.04 - 0.04, (0.0, 0.0, 0.04), brass, segments=16),
    B.cylinder("top_plate", 0.28, 0.02, (0.0, 0.0, H - 0.06), brass, segments=24),
    B.cylinder("top", R, 0.04, (0.0, 0.0, H - 0.04), oak, segments=48),
]
table = B.join(parts, "bb_set_roundtable")
X.link_only(table, coll)
path = X.output_path("bb_set_roundtable.fbx")
X.export_collection(coll, path)
X.report(coll, path)

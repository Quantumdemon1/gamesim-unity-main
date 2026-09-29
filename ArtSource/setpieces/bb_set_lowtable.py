"""The low round table inside the living room's U: black stone under a brass rim, on a brass drum.

MOCKUP-PASS-PLAN M20 and the eviction storyboard: a round table low enough to sit behind, 0.95 m
across and 0.45 m tall, its top a dark marble-black with a thin brass rim, standing on a short
brass drum over a black plinth. Its own script, because bb_set_roundtable is the nomination room's
table and changes on its own. Origin at the floor under the centre. Furniture, so no collider: the
placement pass fits its proxy on the furniture layer (it stands above the 0.40 m it takes).

    blender --background --python ArtSource/setpieces/bb_set_lowtable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_lowtable")

stone = X.material("marble_black", (0.035, 0.035, 0.045), roughness=0.25)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

R, H = 0.475, 0.45
TOP = 0.035
parts = [
    B.cylinder("plinth", 0.30, 0.03, (0.0, 0.0, 0.0), ink, segments=40),
    B.ring("drum", 0.26, 0.22, H - TOP - 0.03, (0.0, 0.0, 0.03), brass, segments=40),
    B.cylinder("drum_core", 0.22, H - TOP - 0.03, (0.0, 0.0, 0.03), ink, segments=40),
    B.cylinder("top", R - 0.012, TOP, (0.0, 0.0, H - TOP), stone, segments=64),
    B.ring("rim", R, R - 0.012, TOP + 0.004, (0.0, 0.0, H - TOP - 0.002), brass, segments=64),
]
table = B.join(parts, "bb_set_lowtable")
X.link_only(table, coll)
path = X.output_path("bb_set_lowtable.fbx")
X.export_collection(coll, path)
X.report(coll, path)

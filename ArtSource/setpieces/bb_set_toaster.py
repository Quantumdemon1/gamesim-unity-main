"""A toaster: a rounded-looking steel body with two slots and a lever. 0.28 x 0.16 x 0.22 m.

Stands in for the kit's toaster on the counter. Origin at the base under the centre. Furniture,
so no collider.

    blender --background --python ArtSource/setpieces/bb_set_toaster.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_toaster")

steel = X.material("steel_appliance", (0.72, 0.74, 0.77), metallic=0.65, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, D, H = 0.28, 0.16, 0.22
parts = [
    B.box("body", (W, D, H - 0.02), (0.0, 0.0, 0.02 + (H - 0.02) / 2), steel),
    B.box("feet", (W - 0.04, D - 0.04, 0.02), (0.0, 0.0, 0.01), ink),
    B.box("slot_a", (W - 0.06, 0.02, 0.005), (0.0, -0.03, H + 0.0025), ink),
    B.box("slot_b", (W - 0.06, 0.02, 0.005), (0.0, 0.03, H + 0.0025), ink),
    B.box("lever", (0.02, 0.03, 0.02), (W / 2 + 0.01, 0.0, H * 0.7), brass),
]
toaster = B.join(parts, "bb_set_toaster")
X.link_only(toaster, coll)
path = X.output_path("bb_set_toaster.fbx")
X.export_collection(coll, path)
X.report(coll, path)

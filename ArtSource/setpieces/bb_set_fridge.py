"""A fridge: the kitchen run's fridge on its own, two doors and long brass handles. 0.8 x 0.7 x 1.45 m.

Stands in for the kit's kitchenFridgeLarge. Doors at -y. Under the 1.5 m wall. Origin at the
floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_fridge.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_fridge")

steel = X.material("steel_appliance", (0.72, 0.74, 0.77), metallic=0.65, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, D, H = 0.8, 0.7, 1.45
parts = [
    B.box("body", (W, D, H - 0.06), (0.0, 0.0, 0.06 + (H - 0.06) / 2), steel),
    B.box("plinth", (W - 0.06, D - 0.06, 0.06), (0.0, 0.0, 0.03), ink),
    B.box("seam", (W - 0.04, 0.01, 0.01), (0.0, -D / 2 - 0.005, 0.95), ink),
    B.box("handle_top", (0.03, 0.03, 0.34), (W / 2 - 0.10, -D / 2 - 0.03, 1.20), brass),
    B.box("handle_bottom", (0.03, 0.03, 0.34), (W / 2 - 0.10, -D / 2 - 0.03, 0.62), brass),
]
fridge = B.join(parts, "bb_set_fridge")
X.link_only(fridge, coll)
path = X.output_path("bb_set_fridge.fbx")
X.export_collection(coll, path)
X.report(coll, path)

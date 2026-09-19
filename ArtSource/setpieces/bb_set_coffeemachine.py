"""A coffee machine: an ink body with a steel group head, a drip tray and a cup. 0.26 x 0.3 x 0.34 m.

Stands in for the kit's kitchenCoffeeMachine on the counter. Front at -y. Origin at the base
under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_coffeemachine.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_coffeemachine")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
steel = X.material("steel_appliance", (0.72, 0.74, 0.77), metallic=0.65, roughness=0.35)
white = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.3)

W, D, H = 0.26, 0.30, 0.34
parts = [
    B.box("base", (W, D, 0.03), (0.0, 0.0, 0.015), steel),
    B.box("tower", (W, D * 0.5, H - 0.03), (0.0, D * 0.25, 0.03 + (H - 0.03) / 2), ink),
    B.box("head", (0.10, 0.10, 0.06), (0.0, -0.05, H - 0.14), steel),
    B.box("tray", (W - 0.04, D * 0.45, 0.01), (0.0, -D * 0.27, 0.035), steel),
    B.cylinder("cup", 0.03, 0.06, (0.0, -D * 0.27, 0.04), white, segments=12),
    B.box("tank", (W - 0.06, 0.03, 0.10), (0.0, D / 2 + 0.015, H - 0.12), steel),
]
machine = B.join(parts, "bb_set_coffeemachine")
X.link_only(machine, coll)
path = X.output_path("bb_set_coffeemachine.fbx")
X.export_collection(coll, path)
X.report(coll, path)

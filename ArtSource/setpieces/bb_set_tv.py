"""A television: a slim screen in an ink frame on a foot, the screen glowing. 1.1 x 0.18 x 0.65 m.

Stands in for the kit's televisionModern where a screen stands on its own (the furnishing pass
fits it to a wall panel). The screen faces -y. Origin at the base under the foot. Furniture, so
no collider.

    blender --background --python ArtSource/setpieces/bb_set_tv.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_tv")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
screen = X.material("glow_screen", (0.20, 0.36, 0.55), roughness=0.2,
                    emission=(0.25, 0.45, 0.70), emission_strength=1.5)

W, H = 1.1, 0.65
parts = [
    B.box("foot", (0.40, 0.18, 0.02), (0.0, 0.0, 0.01), ink),
    B.box("neck", (0.10, 0.03, 0.08), (0.0, 0.0, 0.06), ink),
    B.box("frame", (W, 0.03, H - 0.10), (0.0, 0.0, 0.10 + (H - 0.10) / 2), ink),
    B.box("screen", (W - 0.06, 0.005, H - 0.16), (0.0, -0.0175, 0.10 + (H - 0.10) / 2), screen),
]
tv = B.join(parts, "bb_set_tv")
X.link_only(tv, coll)
path = X.output_path("bb_set_tv.fbx")
X.export_collection(coll, path)
X.report(coll, path)

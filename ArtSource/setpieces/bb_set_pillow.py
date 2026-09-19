"""A pillow: a soft linen block with a slight crown. 0.5 x 0.34 x 0.11 m.

Stands in for the kit's pillow and pillowBlue, on the HoH bed and wherever a row lifts one onto
a bed. Origin at its base under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_pillow.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_pillow")

linen = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.9)

parts = [
    B.box("pillow", (0.5, 0.34, 0.08), (0.0, 0.0, 0.04), linen),
    B.box("crown", (0.42, 0.26, 0.03), (0.0, 0.0, 0.095), linen),
]
pillow = B.join(parts, "bb_set_pillow")
X.link_only(pillow, coll)
path = X.output_path("bb_set_pillow.fbx")
X.export_collection(coll, path)
X.report(coll, path)

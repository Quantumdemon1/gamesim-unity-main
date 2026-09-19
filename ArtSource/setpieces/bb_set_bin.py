"""A pedal bin: brushed steel, a dark lid, a pedal at the foot.

Replaces the kit's trashcan in every room that has one. 0.30 m across, 0.55 m tall. Origin at
the floor under the drum. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_bin.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_bin")

steel = X.material("steel_appliance", (0.72, 0.74, 0.77), metallic=0.65, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

parts = [
    B.cylinder("drum", 0.15, 0.48, (0.0, 0.0, 0.02), steel, segments=20),
    B.cylinder("foot", 0.13, 0.02, (0.0, 0.0, 0.0), ink, segments=20),
    B.cylinder("lid", 0.155, 0.04, (0.0, 0.0, 0.50), ink, segments=20),
    B.cylinder("lid_knob", 0.03, 0.02, (0.0, 0.0, 0.54), steel, segments=12),
    B.box("pedal", (0.10, 0.06, 0.015), (0.0, -0.17, 0.03), ink),
]
bin_ = B.join(parts, "bb_set_bin")
X.link_only(bin_, coll)

path = X.output_path("bb_set_bin.fbx")
X.export_collection(coll, path)
X.report(coll, path)

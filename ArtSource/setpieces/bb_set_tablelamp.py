"""A table lamp: a squat brass body under a linen shade that glows. 0.45 m tall.

Stands in for the kit's lampSquareTable; the plan lifts it onto its side table. Origin at the
base. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_tablelamp.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_tablelamp")

brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
shade = X.material("glow_linen", (0.92, 0.86, 0.72), roughness=0.9,
                   emission=(1.0, 0.88, 0.66), emission_strength=1.2)

H = 0.45
parts = [
    B.cylinder("base", 0.07, 0.02, (0.0, 0.0, 0.0), brass, segments=16),
    B.cylinder("body", 0.045, 0.20, (0.0, 0.0, 0.02), brass, segments=16),
    B.cylinder("neck", 0.015, 0.05, (0.0, 0.0, 0.22), brass, segments=8),
    B.ring("shade", 0.13, 0.12, 0.18, (0.0, 0.0, H - 0.18), shade, segments=20),
    B.disc("shade_top", 0.12, (0.0, 0.0, H - 0.005), shade, segments=20),
]
lamp = B.join(parts, "bb_set_tablelamp")
X.link_only(lamp, coll)
path = X.output_path("bb_set_tablelamp.fbx")
X.export_collection(coll, path)
X.report(coll, path)

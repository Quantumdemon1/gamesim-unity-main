"""A floor lamp: a brass column on a weighted base, a linen drum shade that glows.

Stands in for the kit's lampSquareFloor and lampRoundFloor. 1.5 m tall; the rows scale it to
their own heights. The shade's material is named for its glow, so the importer lights it. Origin
at the floor under the base. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_floorlamp.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_floorlamp")

brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
shade = X.material("glow_linen", (0.92, 0.86, 0.72), roughness=0.9,
                   emission=(1.0, 0.88, 0.66), emission_strength=1.2)

H = 1.5
parts = [
    B.cylinder("base", 0.14, 0.03, (0.0, 0.0, 0.0), ink, segments=20),
    B.cylinder("column", 0.02, H - 0.36, (0.0, 0.0, 0.03), brass, segments=10),
    B.ring("shade", 0.18, 0.165, 0.32, (0.0, 0.0, H - 0.32), shade, segments=24),
    B.ring("shade_ring_top", 0.185, 0.16, 0.01, (0.0, 0.0, H - 0.01), brass, segments=24),
    B.ring("shade_ring_bottom", 0.185, 0.16, 0.01, (0.0, 0.0, H - 0.32), brass, segments=24),
]
lamp = B.join(parts, "bb_set_floorlamp")
X.link_only(lamp, coll)
path = X.output_path("bb_set_floorlamp.fbx")
X.export_collection(coll, path)
X.report(coll, path)

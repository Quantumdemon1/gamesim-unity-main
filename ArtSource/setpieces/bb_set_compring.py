"""The competition circle: three concentric gold rings laid flat on the yard.

They replace 144 primitive segments - three rings of 48 boxes - with one mesh of 96-sided
annuli, so the circle reads as a circle from the overhead camera instead of a polygon of
tiles. The radii, the 0.12 m band and the 5 cm lip are the primitives' own, so the yard's
layout is unchanged. Origin at the centre on the floor. A floor graphic, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_compring.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_compring")

# Neon gold: the emissive tone the house's own outlines use. The importer reads the emission.
gold = X.material("neon_gold", (0.92, 0.72, 0.22), metallic=0.6, roughness=0.3,
                  emission=(1.0, 0.76, 0.28), emission_strength=2.0)

BAND, LIP = 0.12, 0.05
parts = [
    B.ring("ring_2_0", 2.0 + BAND / 2, 2.0 - BAND / 2, LIP, (0.0, 0.0, 0.0), gold, segments=96),
    B.ring("ring_3_2", 3.2 + BAND / 2, 3.2 - BAND / 2, LIP, (0.0, 0.0, 0.0), gold, segments=96),
    B.ring("ring_4_4", 4.4 + BAND / 2, 4.4 - BAND / 2, LIP, (0.0, 0.0, 0.0), gold, segments=96),
]
rings = B.join(parts, "bb_set_compring")
X.link_only(rings, coll)

path = X.output_path("bb_set_compring.fbx")
X.export_collection(coll, path)
X.report(coll, path)

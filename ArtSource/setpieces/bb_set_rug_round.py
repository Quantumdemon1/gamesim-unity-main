"""A round rug: a teal disc with a coral ring, one metre across at export.

Stands in for the kit's rugRound; the plan's negative height lays it flat and sizes it. 1 cm
thick. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_rug_round.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_rug_round")

field = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.95)
border = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.95)

parts = [
    B.cylinder("field", 0.42, 0.01, (0.0, 0.0, 0.0), field, segments=48),
    B.ring("border", 0.5, 0.42, 0.01, (0.0, 0.0, 0.0), border, segments=48),
]
rug = B.join(parts, "bb_set_rug_round")
X.link_only(rug, coll)
path = X.output_path("bb_set_rug_round.fbx")
X.export_collection(coll, path)
X.report(coll, path)

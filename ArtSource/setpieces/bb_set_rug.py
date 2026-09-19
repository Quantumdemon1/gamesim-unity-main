"""A rug: a flat teal field with a coral border, one metre square at export.

Stands in for the kit's rugSquare and rugRectangle; the plan's negative heights lay it flat and
size it, so one export serves every rug. 1 cm thick. Origin at the floor under the centre.
Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_rug.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_rug")

field = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.95)
border = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.95)

S, T, BORDER = 1.0, 0.01, 0.08
parts = [
    B.box("field", (S - 2 * BORDER, S - 2 * BORDER, T), (0.0, 0.0, T / 2), field),
    B.box("border_n", (S, BORDER, T), (0.0, S / 2 - BORDER / 2, T / 2), border),
    B.box("border_s", (S, BORDER, T), (0.0, -S / 2 + BORDER / 2, T / 2), border),
    B.box("border_e", (BORDER, S - 2 * BORDER, T), (S / 2 - BORDER / 2, 0.0, T / 2), border),
    B.box("border_w", (BORDER, S - 2 * BORDER, T), (-S / 2 + BORDER / 2, 0.0, T / 2), border),
]
rug = B.join(parts, "bb_set_rug")
X.link_only(rug, coll)
path = X.output_path("bb_set_rug.fbx")
X.export_collection(coll, path)
X.report(coll, path)

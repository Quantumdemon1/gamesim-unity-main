"""A coat rack: a brass column on a weighted foot with four hooks at the top. 0.4 m across, 1.45 m tall.

Stands in for the kit's coatRackStanding. Origin at the floor under the column. Furniture, so no
collider.

    blender --background --python ArtSource/setpieces/bb_set_coatrack.py -- <output.fbx>
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_coatrack")

brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

H = 1.45
parts = [
    B.cylinder("foot", 0.18, 0.03, (0.0, 0.0, 0.0), ink, segments=20),
    B.cylinder("column", 0.02, H - 0.03, (0.0, 0.0, 0.03), brass, segments=10),
    B.cylinder("finial", 0.035, 0.03, (0.0, 0.0, H - 0.03), brass, segments=10),
]
for i in range(4):
    hook = B.box("hook_%d" % i, (0.03, 0.18, 0.02), (0.0, 0.11, 0.0), brass)
    tip = B.box("tip_%d" % i, (0.03, 0.02, 0.06), (0.0, 0.19, 0.02), brass)
    for part in (hook, tip):
        part.data.transform(Matrix.Rotation(math.radians(i * 90.0), 4, 'Z'))
        part.data.transform(Matrix.Translation(Vector((0.0, 0.0, H - 0.20))))
        parts.append(part)

rack = B.join(parts, "bb_set_coatrack")
X.link_only(rack, coll)
path = X.output_path("bb_set_coatrack.fbx")
X.export_collection(coll, path)
X.report(coll, path)

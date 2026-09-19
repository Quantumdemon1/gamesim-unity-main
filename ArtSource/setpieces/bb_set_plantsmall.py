"""A small plant: a squat pot with a tuft of narrow leaves. 0.5 m tall.

Stands in for the kit's plantSmall1, plantSmall2 and plantSmall3. Origin at the floor under the
pot. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_plantsmall.py -- <output.fbx>
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
coll = X.collection("bb_set_plantsmall")

pot_mat = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.6)
soil = X.material("soil", (0.16, 0.11, 0.07), roughness=0.95)
leaf = X.material("leaf_green", (0.14, 0.42, 0.20), roughness=0.7)

POT_H, POT_R = 0.16, 0.10
parts = [
    B.cylinder("pot", POT_R, POT_H, (0.0, 0.0, 0.0), pot_mat, segments=12),
    B.cylinder("soil", POT_R - 0.015, 0.015, (0.0, 0.0, POT_H), soil, segments=12),
]
for i in range(9):
    angle = i * 40.0
    tilt = 20.0 + (i % 4) * 10.0
    length = 0.24 + (i % 3) * 0.05
    blade = B.box("leaf_%d" % i, (0.03, length, 0.008), (0.0, length / 2, 0.0), leaf)
    blade.data.transform(Matrix.Rotation(math.radians(angle), 4, 'Z') @ Matrix.Rotation(math.radians(tilt), 4, 'X'))
    blade.data.transform(Matrix.Translation(Vector((0.0, 0.0, POT_H + 0.01))))
    parts.append(blade)

plant = B.join(parts, "bb_set_plantsmall")
X.link_only(plant, coll)
path = X.output_path("bb_set_plantsmall.fbx")
X.export_collection(coll, path)
X.report(coll, path)

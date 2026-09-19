"""A potted plant: an ink pot on a saucer, soil, and a crown of broad leaves. 0.9 m tall.

Stands in for the kit's pottedPlant; the rows scale it to their heights. The leaves are
tilted boxes fanned around the stem - low-poly foliage that reads as a plant from four metres
and costs nothing. Origin at the floor under the pot. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_plant.py -- <output.fbx>
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
coll = X.collection("bb_set_plant")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
soil = X.material("soil", (0.16, 0.11, 0.07), roughness=0.95)
leaf = X.material("leaf_green", (0.14, 0.42, 0.20), roughness=0.7)
stem = X.material("stem_green", (0.20, 0.32, 0.14), roughness=0.8)

POT_H, POT_R = 0.34, 0.17
parts = [
    B.cylinder("saucer", POT_R + 0.02, 0.02, (0.0, 0.0, 0.0), ink, segments=16),
    B.cylinder("pot", POT_R, POT_H, (0.0, 0.0, 0.02), ink, segments=16),
    B.cylinder("soil", POT_R - 0.02, 0.02, (0.0, 0.0, POT_H), soil, segments=16),
    B.cylinder("stem", 0.02, 0.30, (0.0, 0.0, POT_H + 0.02), stem, segments=8),
]
for i in range(7):
    angle = i * 360.0 / 7
    tilt = 35.0 + (i % 3) * 12.0
    length = 0.36 + (i % 2) * 0.08
    blade = B.box("leaf_%d" % i, (0.10, length, 0.012), (0.0, length / 2, 0.0), leaf)
    m = Matrix.Rotation(math.radians(angle), 4, 'Z') @ Matrix.Rotation(math.radians(tilt), 4, 'X')
    blade.data.transform(m)
    blade.data.transform(Matrix.Translation(Vector((0.0, 0.0, POT_H + 0.26))))
    parts.append(blade)
crown = B.box("leaf_top", (0.08, 0.22, 0.012), (0.0, 0.11, 0.0), leaf)
crown.data.transform(Matrix.Rotation(math.radians(80.0), 4, 'X'))
crown.data.transform(Matrix.Translation(Vector((0.0, 0.0, POT_H + 0.32))))
parts.append(crown)

plant = B.join(parts, "bb_set_plant")
X.link_only(plant, coll)
path = X.output_path("bb_set_plant.fbx")
X.export_collection(coll, path)
X.report(coll, path)

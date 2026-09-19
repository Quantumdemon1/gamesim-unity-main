"""A floor-standing speaker: an ink column with two brass-ringed drivers. 0.22 x 0.22 x 0.95 m.

Stands in for the kit's speaker. Front at -y. Origin at the floor under the centre. Furniture,
so no collider.

    blender --background --python ArtSource/setpieces/bb_set_speaker.py -- <output.fbx>
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
coll = X.collection("bb_set_speaker")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
cone = X.material("steel_cold", (0.45, 0.47, 0.50), metallic=0.75, roughness=0.45)

W, H = 0.22, 0.95
parts = [B.box("cabinet", (W, W, H - 0.02), (0.0, 0.0, 0.02 + (H - 0.02) / 2), ink),
         B.box("plinth", (W + 0.02, W + 0.02, 0.02), (0.0, 0.0, 0.01), brass)]
for name, z, r in (("woofer", 0.30, 0.075), ("tweeter", 0.72, 0.04)):
    ring = B.ring(name + "_ring", r + 0.01, r, 0.012, (0.0, 0.0, 0.0), brass, segments=20)
    disc = B.disc(name + "_cone", r, (0.0, 0.0, 0.006), cone, segments=20)
    for part in (ring, disc):
        part.data.transform(Matrix.Rotation(math.radians(90.0), 4, 'X'))   # face -y
        part.data.transform(Matrix.Translation(Vector((0.0, -W / 2 - 0.001, z))))
        parts.append(part)

speaker = B.join(parts, "bb_set_speaker")
X.link_only(speaker, coll)
path = X.output_path("bb_set_speaker.fbx")
X.export_collection(coll, path)
X.report(coll, path)

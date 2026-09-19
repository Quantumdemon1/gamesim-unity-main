"""A microwave: a steel box with a dark door and a brass handle. 0.5 x 0.38 x 0.32 m.

Stands in for the kit's kitchenMicrowave on the kitchen run's counter. Door at -y. Origin at the
base under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_microwave.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_microwave")

steel = X.material("steel_appliance", (0.72, 0.74, 0.77), metallic=0.65, roughness=0.35)
glass = X.material("oven_glass", (0.05, 0.06, 0.08), roughness=0.15)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, D, H = 0.5, 0.38, 0.32
parts = [
    B.box("body", (W, D, H), (0.0, 0.0, H / 2), steel),
    B.box("door", (W * 0.66, 0.01, H - 0.06), (-W * 0.12, -D / 2 - 0.005, H / 2), glass),
    B.box("handle", (0.02, 0.02, H - 0.10), (W * 0.20, -D / 2 - 0.02, H / 2), brass),
    B.box("panel", (W * 0.22, 0.01, H - 0.06), (W * 0.36, -D / 2 - 0.005, H / 2), steel),
    B.cylinder("dial", 0.02, 0.01, (0.0, 0.0, 0.0), brass, segments=10),
]
import math  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
dial = parts[-1]
dial.data.transform(Matrix.Rotation(math.radians(90.0), 4, 'X'))
dial.data.transform(Matrix.Translation(Vector((W * 0.36, -D / 2 - 0.01, H * 0.6))))

microwave = B.join(parts, "bb_set_microwave")
X.link_only(microwave, coll)
path = X.output_path("bb_set_microwave.fbx")
X.export_collection(coll, path)
X.report(coll, path)

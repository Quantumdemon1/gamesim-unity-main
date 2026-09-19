"""A toilet: cistern, bowl, seat and lid, in white with a brass flush. 0.40 x 0.70 x 0.72 m.

Stands in for the kit's toilet. The cistern is at +y (against the wall). Origin at the floor
under the bowl. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_toilet.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_toilet")

white = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.3)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, L, H = 0.40, 0.70, 0.72
parts = [
    B.box("cistern", (W, 0.18, 0.34), (0.0, L / 2 - 0.09, H - 0.17), white),
    B.box("cistern_lid", (W + 0.02, 0.20, 0.02), (0.0, L / 2 - 0.09, H - 0.01), white),
    B.cylinder("flush", 0.02, 0.03, (W / 2 - 0.06, L / 2 - 0.09, H), brass, segments=8),
    B.box("base", (0.24, 0.30, 0.36), (0.0, 0.02, 0.18), white),
    B.cylinder("bowl", 0.19, 0.06, (0.0, -0.06, 0.36), white, segments=20),
    B.cylinder("seat", 0.20, 0.02, (0.0, -0.06, 0.42), ink, segments=20),
    B.box("lid", (0.38, 0.30, 0.02), (0.0, 0.08, 0.44), ink),
]
toilet = B.join(parts, "bb_set_toilet")
# The cistern sits at the back, so centre the piece on its origin (the plan places by bounds).
from mathutils import Matrix, Vector  # noqa: E402
toilet.data.transform(Matrix.Translation(Vector((0.0, -0.05, 0.0))))
X.link_only(toilet, coll)
path = X.output_path("bb_set_toilet.fbx")
X.export_collection(coll, path)
X.report(coll, path)

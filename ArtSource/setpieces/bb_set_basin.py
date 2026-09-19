"""A pedestal basin: a white basin on a column with a brass tap. 0.55 x 0.45 x 0.85 m.

Stands in for the kit's bathroomSink. The back is at +y. Origin at the floor under the
pedestal. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_basin.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_basin")

white = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.3)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)

W, D, H = 0.55, 0.45, 0.85
parts = [
    B.cylinder("pedestal", 0.09, H - 0.16, (0.0, 0.05, 0.0), white, segments=16),
    B.box("basin", (W, D, 0.16), (0.0, 0.0, H - 0.16 + 0.08), white),
    B.box("bowl_shadow", (W - 0.10, D - 0.14, 0.01), (0.0, -0.02, H - 0.005 + 0.001), ink),
    B.box("backsplash", (W, 0.03, 0.12), (0.0, D / 2 - 0.015, H + 0.06), white),
    B.cylinder("tap", 0.015, 0.14, (0.0, D / 2 - 0.08, H), brass, segments=10),
    B.box("spout", (0.025, 0.12, 0.025), (0.0, D / 2 - 0.14, H + 0.12), brass),
]
basin = B.join(parts, "bb_set_basin")
X.link_only(basin, coll)
path = X.output_path("bb_set_basin.fbx")
X.export_collection(coll, path)
X.report(coll, path)

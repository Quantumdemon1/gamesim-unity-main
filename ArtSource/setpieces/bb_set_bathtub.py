"""A bathtub: a linen-white tub in an ink surround with a brass tap. 1.7 x 0.75 x 0.60 m.

Stands in for the kit's bathtub in the HoH ensuite. The tap end is at -y. Origin at the floor
under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_bathtub.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_bathtub")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
white = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.3)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
water = X.material("water", (0.30, 0.60, 0.75), roughness=0.1, alpha=0.5)

W, L, H = 0.75, 1.7, 0.60
RIM, T = 0.06, 0.05
parts = [
    B.box("surround", (W, L, H - RIM), (0.0, 0.0, (H - RIM) / 2), ink),
    # The rim as four strips, and the tub's inner walls and floor below them.
    B.box("rim_a", (W, T, RIM), (0.0, L / 2 - T / 2, H - RIM / 2), white),
    B.box("rim_b", (W, T, RIM), (0.0, -L / 2 + T / 2, H - RIM / 2), white),
    B.box("rim_c", (T, L - 2 * T, RIM), (W / 2 - T / 2, 0.0, H - RIM / 2), white),
    B.box("rim_d", (T, L - 2 * T, RIM), (-W / 2 + T / 2, 0.0, H - RIM / 2), white),
    B.box("tub_floor", (W - 2 * T, L - 2 * T, 0.02), (0.0, 0.0, H - RIM - 0.38 + 0.01), white),
    B.box("water", (W - 2 * T - 0.01, L - 2 * T - 0.01, 0.01), (0.0, 0.0, H - RIM - 0.16), water),
    B.cylinder("tap", 0.018, 0.16, (0.0, -L / 2 + 0.10, H), brass, segments=10),
    B.box("spout", (0.03, 0.14, 0.03), (0.0, -L / 2 + 0.16, H + 0.14), brass),
]
tub = B.join(parts, "bb_set_bathtub")
X.link_only(tub, coll)
path = X.output_path("bb_set_bathtub.fbx")
X.export_collection(coll, path)
X.report(coll, path)

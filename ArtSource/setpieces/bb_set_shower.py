"""A shower: a square tray, two glass screens on the open sides, a brass column and head.

Stands in for the kit's showerRound in the HoH ensuite. 0.9 x 0.9 m, 1.05 m tall, under the
wing's 1.1 m wall. The tiled back corner is at +x, +y; the glass faces the room. Origin at the
floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_shower.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_shower")

white = X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.3)
tile = X.material("pool_tile", (0.55, 0.72, 0.78), roughness=0.25)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
glass = X.material("shower_glass", (0.75, 0.85, 0.9), roughness=0.05, alpha=0.3)

S, H = 0.9, 1.05
parts = [
    B.box("tray", (S, S, 0.06), (0.0, 0.0, 0.03), white),
    B.box("wall_back", (S, 0.03, H), (0.0, S / 2 - 0.015, H / 2), tile),
    B.box("wall_side", (0.03, S, H), (S / 2 - 0.015, 0.0, H / 2), tile),
    B.box("glass_front", (S - 0.03, 0.01, H - 0.06), (-0.015, -S / 2 + 0.005, 0.06 + (H - 0.06) / 2), glass),
    B.box("glass_side", (0.01, S - 0.03, H - 0.06), (-S / 2 + 0.005, 0.015, 0.06 + (H - 0.06) / 2), glass),
    B.box("glass_rail_front", (S - 0.03, 0.02, 0.02), (-0.015, -S / 2 + 0.01, H - 0.01), brass),
    B.box("glass_rail_side", (0.02, S - 0.03, 0.02), (-S / 2 + 0.01, 0.015, H - 0.01), brass),
    B.cylinder("column", 0.015, H - 0.20, (S / 2 - 0.10, S / 2 - 0.10, 0.06), brass, segments=8),
    B.box("arm", (0.20, 0.03, 0.03), (S / 2 - 0.20, S / 2 - 0.10, H - 0.14), brass),
    B.cylinder("head", 0.07, 0.02, (S / 2 - 0.30, S / 2 - 0.10, H - 0.16), brass, segments=16),
]
shower = B.join(parts, "bb_set_shower")
X.link_only(shower, coll)
path = X.output_path("bb_set_shower.fbx")
X.export_collection(coll, path)
X.report(coll, path)

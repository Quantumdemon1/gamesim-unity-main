"""A dining chair for the long table: brass frame, coral seat, a slightly raked back.

0.46 x 0.50 m footprint, 0.92 m tall, origin at the floor under the seat's centre; the back is on
the -y side, so a chair placed with yaw 0 faces +y (towards +z in Unity). Furniture, no collider.

    blender --background --python ArtSource/setpieces/bb_set_diningchair.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_diningchair")

brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
coral = X.material("cushion_coral", (0.95, 0.45, 0.40), roughness=0.8)

W, D, SEAT = 0.46, 0.46, 0.46
LEG = 0.035

parts = [
    B.box("leg_fl", (LEG, LEG, SEAT), (W / 2 - LEG / 2, D / 2 - LEG / 2, SEAT / 2), brass),
    B.box("leg_fr", (LEG, LEG, SEAT), (-W / 2 + LEG / 2, D / 2 - LEG / 2, SEAT / 2), brass),
    B.box("leg_bl", (LEG, LEG, SEAT), (W / 2 - LEG / 2, -D / 2 + LEG / 2, SEAT / 2), brass),
    B.box("leg_br", (LEG, LEG, SEAT), (-W / 2 + LEG / 2, -D / 2 + LEG / 2, SEAT / 2), brass),
    B.box("frame", (W, D, 0.03), (0.0, 0.0, SEAT - 0.015), brass),
    B.box("seat", (W - 0.02, D - 0.02, 0.06), (0.0, 0.0, SEAT + 0.03), coral),
    # The back rises from the rear edge of the seat and leans away 8 degrees.
    B.tilted_box("back", (W - 0.06, 0.04, 0.44), (0.0, -D / 2 + 0.02, SEAT + 0.06), -8.0, coral),
    B.tilted_box("back_post_l", (LEG, LEG, 0.46), (W / 2 - LEG / 2 - 0.01, -D / 2 + LEG / 2, SEAT), -8.0, brass),
    B.tilted_box("back_post_r", (LEG, LEG, 0.46), (-W / 2 + LEG / 2 + 0.01, -D / 2 + LEG / 2, SEAT), -8.0, brass),
]
chair = B.join(parts, "bb_set_diningchair")
X.link_only(chair, coll)

path = X.output_path("bb_set_diningchair.fbx")
X.export_collection(coll, path)
X.report(coll, path)

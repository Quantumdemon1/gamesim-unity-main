"""The red wingback: the nominees' chair at evictions, the storyboards' red seat.

CEREMONY-CUTSCENES-PLAN 8.2.3: 0.9 m wide, 0.9 m deep, 1.25 m tall, red velvet, with wings at
the top of the back and rolled arms, on dark tapered feet with brass caps. The seat is at 0.45 m.
The back is at +y (Unity +z); yaw 0 faces -z. Origin at the floor under the centre. Furniture,
so no collider: the placement pass fits its proxy on the furniture layer.

    blender --background --python ArtSource/setpieces/bb_set_wingback.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_wingback")

velvet = X.material("velvet_red", (0.52, 0.03, 0.05), roughness=0.7)
piping = X.material("velvet_red_dark", (0.30, 0.02, 0.03), roughness=0.75)
wood = X.material("wood_black", (0.05, 0.04, 0.04), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, D, H = 0.90, 0.90, 1.25
SEAT = 0.45
LEG = 0.14
BACK = 0.20          # the back's thickness
BODY = SEAT - 0.12   # the upholstered base under the cushion

parts = [
    # The base, from the feet to under the cushion.
    B.soft_box("base", (W - 0.04, D - 0.06, BODY - LEG), (0.0, -0.01, LEG + (BODY - LEG) / 2), velvet, radius=0.03),
    B.box("base_band", (W - 0.02, D - 0.04, 0.03), (0.0, -0.01, LEG + 0.015), piping),
    # The back, full width and height, and its inner cushion.
    B.soft_box("back", (W, BACK, H - LEG), (0.0, D / 2 - BACK / 2, LEG + (H - LEG) / 2), velvet, radius=0.07),
    B.soft_box("back_cushion", (W - 0.40, 0.12, H - SEAT - 0.20), (0.0, D / 2 - BACK - 0.06, SEAT + 0.12 + (H - SEAT - 0.20) / 2), velvet, radius=0.05),
    # The seat cushion between the arms.
    B.soft_box("seat", (W - 0.36, D - BACK - 0.08, 0.13), (0.0, -BACK / 2 + 0.02, SEAT - 0.065 + 0.03), velvet, radius=0.05),
]
# Channel tufting: the storyboards' red chairs have vertical seams down the inner back, drawn here
# as dark piping just proud of the back cushion's face, with a band across its top.
face_y = D / 2 - BACK - 0.06 - 0.06 - 0.004
channel_height = H - SEAT - 0.24
for i, x in enumerate((-0.15, -0.05, 0.05, 0.15)):
    parts.append(B.box("channel_%d" % i, (0.012, 0.01, channel_height), (x, face_y, SEAT + 0.14 + channel_height / 2), piping))
parts.append(B.box("channel_band", (W - 0.40, 0.012, 0.03), (0.0, face_y, H - 0.10), piping))
# The wings: deep at the top of the back, narrowing toward the arms.
for side, x in (("l", -W / 2 + 0.08), ("r", W / 2 - 0.08)):
    parts.append(B.soft_box("wing_%s" % side, (0.16, 0.42, 0.50), (x, D / 2 - BACK - 0.21 + 0.10, H - 0.25 - 0.02), velvet, radius=0.06))
    parts.append(B.soft_box("wing_low_%s" % side, (0.16, 0.28, 0.16), (x, D / 2 - BACK - 0.14 + 0.10, SEAT + 0.30), velvet, radius=0.05))
    # The arm, rolled: a rounded box.
    parts.append(B.soft_box("arm_%s" % side, (0.18, D - BACK - 0.10, 0.22), (x, -BACK / 2 - 0.02, SEAT + 0.09), velvet, radius=0.08))
# Tapered feet with brass caps.
for i, (x, y) in enumerate([(-W / 2 + 0.10, -D / 2 + 0.10), (W / 2 - 0.10, -D / 2 + 0.10),
                             (-W / 2 + 0.10, D / 2 - 0.10), (W / 2 - 0.10, D / 2 - 0.10)]):
    parts.append(B.cylinder("foot_%d" % i, 0.028, LEG - 0.03, (x, y, 0.03), wood, segments=10))
    parts.append(B.cylinder("cap_%d" % i, 0.024, 0.03, (x, y, 0.0), brass, segments=10))

chair = B.join(parts, "bb_set_wingback")
X.link_only(chair, coll)
path = X.output_path("bb_set_wingback.fbx")
X.export_collection(coll, path)
X.report(coll, path)

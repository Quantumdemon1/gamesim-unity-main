"""A four-seat couch in cream: the base of the living room's U at evictions.

CEREMONY-CUTSCENES-PLAN 8.2.3: straight cream couches at 0.76 m a seat, because the seats'
approaches must stand 0.7 m apart and the old sofa's 0.67 m pitch breaks that. Four seats, so
3.44 m long (0.20 m arms), 0.92 m deep, 0.80 m tall, the seat at 0.43 m. The seats' centres are
at x = -1.14, -0.38, 0.38, 1.14. The back is at +y (Unity +z); yaw 0 faces -z. Origin at the
floor under the centre. Furniture, so no collider: the placement pass fits its proxy.

    blender --background --python ArtSource/setpieces/bb_set_lounge4.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

NAME, SEATS = "bb_set_lounge4", 4

X.fresh_scene()
coll = X.collection(NAME)

# The Furniture Mega Pack's cream sofas as the reference (the owner's, 2026-09-29): plain cream,
# everything upholstered rounded - thick arms, plump back cushions, deep seat cushions - on short
# dark feet, no trim.
boucle = X.material("boucle_cream", (0.80, 0.76, 0.68), roughness=0.9)
cushion = X.material("cushion_cream", (0.88, 0.85, 0.78), roughness=0.85)
feet = X.material("wood_black", (0.05, 0.04, 0.04), roughness=0.5)

PITCH, ARM = 0.76, 0.20
L, D, H = SEATS * PITCH + 2 * ARM, 0.92, 0.80
SEAT, LEG, BACK = 0.43, 0.08, 0.22

parts = [
    B.soft_box("base", (L, D, SEAT - 0.14 - LEG), (0.0, 0.0, LEG + (SEAT - 0.14 - LEG) / 2), boucle, radius=0.03),
    B.soft_box("back", (L, BACK, H - LEG), (0.0, D / 2 - BACK / 2, LEG + (H - LEG) / 2), boucle, radius=0.07),
    B.soft_box("arm_l", (ARM, D, 0.62 - LEG), (-L / 2 + ARM / 2, 0.0, LEG + (0.62 - LEG) / 2), boucle, radius=0.08),
    B.soft_box("arm_r", (ARM, D, 0.62 - LEG), (L / 2 - ARM / 2, 0.0, LEG + (0.62 - LEG) / 2), boucle, radius=0.08),
]
for i in range(SEATS):
    x = (i - (SEATS - 1) / 2) * PITCH
    parts.append(B.soft_box("seat_%d" % i, (PITCH - 0.02, D - BACK - 0.02, 0.15), (x, -BACK / 2 + 0.01, SEAT - 0.075), cushion, radius=0.05))
    parts.append(B.soft_box("back_cushion_%d" % i, (PITCH - 0.03, 0.20, 0.38), (x, D / 2 - BACK - 0.09, SEAT + 0.19), cushion, radius=0.08))
for i, (x, y) in enumerate([(-L / 2 + 0.10, -D / 2 + 0.10), (L / 2 - 0.10, -D / 2 + 0.10),
                             (-L / 2 + 0.10, D / 2 - 0.10), (L / 2 - 0.10, D / 2 - 0.10),
                             (0.0, -D / 2 + 0.10), (0.0, D / 2 - 0.10)]):
    parts.append(B.box("foot_%d" % i, (0.06, 0.06, LEG), (x, y, LEG / 2), feet))

couch = B.join(parts, NAME)
X.link_only(couch, coll)
path = X.output_path(NAME + ".fbx")
X.export_collection(coll, path)
X.report(coll, path)

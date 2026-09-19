"""The diary-room chair: the one piece the camera sees close during reflections.

A brass plinth, a deep velvet seat with rolled arms, and a tall raked back with a crown - the
throne every version of the format has. 0.95 m across, 1.35 m tall, under the private room's
1.5 m wall. Origin at the floor under the seat's centre; the back is on the -y side, so yaw 0
faces +y (Unity +z). Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_diarychair.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_diarychair")

velvet = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
piping = X.material("coping_stone", (0.85, 0.82, 0.75), roughness=0.7)

W, D = 0.95, 0.90
SEAT_Z, SEAT_T = 0.42, 0.14

parts = [
    B.cylinder("plinth", 0.55, 0.06, (0.0, 0.0, 0.0), brass, segments=24),
    B.box("base", (W - 0.10, D - 0.10, SEAT_Z - 0.06 - SEAT_T), (0.0, 0.0, 0.06 + (SEAT_Z - 0.06 - SEAT_T) / 2), velvet),
    B.box("seat", (W - 0.04, D - 0.06, SEAT_T), (0.0, 0.0, SEAT_Z - SEAT_T / 2), velvet),
    # Rolled arms, a little proud of the seat.
    B.box("arm_l", (0.14, D - 0.20, 0.26), (W / 2 - 0.07, 0.03, SEAT_Z + 0.13), velvet),
    B.box("arm_r", (0.14, D - 0.20, 0.26), (-W / 2 + 0.07, 0.03, SEAT_Z + 0.13), velvet),
    B.box("arm_cap_l", (0.16, D - 0.18, 0.03), (W / 2 - 0.07, 0.03, SEAT_Z + 0.275), piping),
    B.box("arm_cap_r", (0.16, D - 0.18, 0.03), (-W / 2 + 0.07, 0.03, SEAT_Z + 0.275), piping),
    # The tall back, raked ten degrees, and its crown.
    B.tilted_box("back", (W - 0.04, 0.16, 0.86), (0.0, -D / 2 + 0.08, SEAT_Z - 0.02), -10.0, velvet),
    B.tilted_box("crown", (W + 0.04, 0.20, 0.08), (0.0, -D / 2 + 0.08, SEAT_Z + 0.84), -10.0, brass),
]
chair = B.join(parts, "bb_set_diarychair")
X.link_only(chair, coll)

path = X.output_path("bb_set_diarychair.fbx")
X.export_collection(coll, path)
X.report(coll, path)

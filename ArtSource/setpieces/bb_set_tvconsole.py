"""A television on its console: a low oak cabinet with two doors, a slim screen on a foot.

Stands in for the kit's cabinetTelevision; the plan's televisionModern rows are dropped, as the
screen is part of this piece. 1.4 m wide, 0.45 m deep, 1.0 m to the screen's top (0.45 to the
cabinet's). The screen faces -y. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_tvconsole.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_tvconsole")

oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
screen = X.material("glow_screen", (0.20, 0.36, 0.55), roughness=0.2,
                    emission=(0.25, 0.45, 0.70), emission_strength=1.5)

W, D, H = 1.4, 0.45, 0.45
parts = [
    B.box("cabinet", (W, D, H - 0.10), (0.0, 0.0, 0.10 + (H - 0.10) / 2), oak),
    B.box("top", (W + 0.02, D + 0.02, 0.03), (0.0, 0.0, H - 0.015), ink),
    B.box("door_l", (W / 2 - 0.05, 0.015, H - 0.18), (-W / 4, -D / 2 - 0.0075, 0.10 + (H - 0.10) / 2), oak),
    B.box("door_r", (W / 2 - 0.05, 0.015, H - 0.18), (W / 4, -D / 2 - 0.0075, 0.10 + (H - 0.10) / 2), oak),
    B.box("pull_l", (0.02, 0.015, 0.10), (-0.04, -D / 2 - 0.02, H * 0.55), brass),
    B.box("pull_r", (0.02, 0.015, 0.10), (0.04, -D / 2 - 0.02, H * 0.55), brass),
    B.box("tv_foot", (0.40, 0.18, 0.02), (0.0, 0.02, H + 0.01), ink),
    B.box("tv_neck", (0.10, 0.03, 0.08), (0.0, 0.02, H + 0.06), ink),
    B.box("tv_frame", (1.1, 0.03, 0.55), (0.0, 0.02, H + 0.10 + 0.275), ink),
    B.box("tv_screen", (1.04, 0.005, 0.49), (0.0, 0.02 - 0.0175, H + 0.10 + 0.275), screen),
]
for i, (x, y) in enumerate([(-W / 2 + 0.06, -D / 2 + 0.06), (W / 2 - 0.06, -D / 2 + 0.06),
                             (-W / 2 + 0.06, D / 2 - 0.06), (W / 2 - 0.06, D / 2 - 0.06)]):
    parts.append(B.cylinder("leg_%d" % i, 0.02, 0.10, (x, y, 0.0), brass, segments=8))

console = B.join(parts, "bb_set_tvconsole")
X.link_only(console, coll)
path = X.output_path("bb_set_tvconsole.fbx")
X.export_collection(coll, path)
X.report(coll, path)

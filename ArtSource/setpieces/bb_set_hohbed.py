"""The Head of Household's bed: the reward room should look like one.

A dark platform, a tall mattress, a velvet duvet turned back, two pillows and a velvet headboard
with a brass crown - kept to 1.0 m so it stays under the south wing's 1.1 m cutaway wall.
2.1 x 1.9 m, origin at the floor under the centre; the headboard is on the -y side, so yaw 0 puts
the head at -z in Unity. Furniture, no collider.

    blender --background --python ArtSource/setpieces/bb_set_hohbed.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_hohbed")

oak = X.material("deck_wood", (0.45, 0.30, 0.18), roughness=0.55)
linen = X.material("linen_white", (0.92, 0.90, 0.85), roughness=0.9)
velvet = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

W, L = 1.9, 2.1          # across, head to foot
PLAT_H, MATT_H = 0.25, 0.22
HEAD_Y = -L / 2 + 0.05   # the headboard's centre line, at the head (-y)

parts = [
    B.box("platform", (W, L, PLAT_H), (0.0, 0.0, PLAT_H / 2), oak),
    B.box("mattress", (W - 0.14, L - 0.14, MATT_H), (0.0, 0.0, PLAT_H + MATT_H / 2), linen),
    # The duvet covers the foot two-thirds and is turned back at the head.
    B.box("duvet", (W - 0.10, (L - 0.14) * 0.66, 0.10), (0.0, (L - 0.14) * 0.17, PLAT_H + MATT_H + 0.05), velvet),
    B.box("turnback", (W - 0.10, 0.22, 0.06), (0.0, (L - 0.14) * 0.17 - (L - 0.14) * 0.33 + 0.11, PLAT_H + MATT_H + 0.13), linen),
    B.box("pillow_l", (0.70, 0.45, 0.14), (W / 4 - 0.02, HEAD_Y + 0.38, PLAT_H + MATT_H + 0.07), linen),
    B.box("pillow_r", (0.70, 0.45, 0.14), (-W / 4 + 0.02, HEAD_Y + 0.38, PLAT_H + MATT_H + 0.07), linen),
    # Headboard: velvet panel between two brass posts, crowned.
    B.box("headboard", (W - 0.16, 0.10, 0.72), (0.0, HEAD_Y, PLAT_H + 0.36), velvet),
    B.box("post_l", (0.08, 0.08, 0.98), (W / 2 - 0.04, HEAD_Y, 0.49), brass),
    B.box("post_r", (0.08, 0.08, 0.98), (-W / 2 + 0.04, HEAD_Y, 0.49), brass),
    B.box("crown", (W, 0.12, 0.05), (0.0, HEAD_Y, 0.975), brass),
]
bed = B.join(parts, "bb_set_hohbed")
X.link_only(bed, coll)

path = X.output_path("bb_set_hohbed.fbx")
X.export_collection(coll, path)
X.report(coll, path)

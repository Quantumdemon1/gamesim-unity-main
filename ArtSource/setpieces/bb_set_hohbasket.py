"""The HoH basket: the week's reward, on the suite's coffee table.

A wicker basket - an octagonal drum on a foot with a handle arc - with the goodies showing
above the rim: two bottles, a box, a bag. 0.5 m across and 0.55 m tall with the handle, so it
reads from the overhead camera as a basket rather than a mug. Origin at its own base, so the
plan lifts it onto the table. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_hohbasket.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_hohbasket")

wicker = X.material("wicker", (0.68, 0.50, 0.27), roughness=0.85)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
bottle = X.material("bottle_green", (0.12, 0.38, 0.22), roughness=0.25)
label = X.material("coping_stone", (0.85, 0.82, 0.75), roughness=0.7)
gift = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)
ribbon = X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.6)

R, H_BASKET = 0.24, 0.24
parts = [
    B.cylinder("foot", 0.20, 0.03, (0.0, 0.0, 0.0), wicker, segments=8),
    B.ring("drum", R, R - 0.02, H_BASKET, (0.0, 0.0, 0.03), wicker, segments=8),
    B.disc("drum_floor", R - 0.02, (0.0, 0.0, 0.03), wicker, segments=8),
    B.ring("rim", R + 0.02, R - 0.02, 0.03, (0.0, 0.0, 0.03 + H_BASKET), brass, segments=8),
    # The handle: two posts up from the rim and a bar across.
    B.box("handle_l", (0.03, 0.03, 0.24), (-R + 0.02, 0.0, 0.03 + H_BASKET + 0.12), wicker),
    B.box("handle_r", (0.03, 0.03, 0.24), (R - 0.02, 0.0, 0.03 + H_BASKET + 0.12), wicker),
    B.box("handle_top", (2 * R - 0.01, 0.03, 0.03), (0.0, 0.0, 0.03 + H_BASKET + 0.24), wicker),
    # What is in it.
    B.cylinder("bottle_a", 0.035, 0.30, (-0.08, 0.06, 0.06), bottle, segments=12),
    B.cylinder("bottle_a_neck", 0.015, 0.08, (-0.08, 0.06, 0.36), bottle, segments=12),
    B.box("bottle_a_label", (0.075, 0.03, 0.08), (-0.08, 0.06 - 0.02, 0.20), label),
    B.cylinder("bottle_b", 0.035, 0.28, (0.05, -0.09, 0.06), bottle, segments=12),
    B.cylinder("bottle_b_neck", 0.015, 0.08, (0.05, -0.09, 0.34), bottle, segments=12),
    B.box("gift_box", (0.16, 0.12, 0.12), (0.08, 0.07, 0.24), gift),
    B.box("gift_ribbon", (0.17, 0.03, 0.13), (0.08, 0.07, 0.245), ribbon),
    B.box("bag", (0.10, 0.14, 0.16), (-0.09, -0.08, 0.20), ribbon),
]
basket = B.join(parts, "bb_set_hohbasket")
X.link_only(basket, coll)

path = X.output_path("bb_set_hohbasket.fbx")
X.export_collection(coll, path)
X.report(coll, path)

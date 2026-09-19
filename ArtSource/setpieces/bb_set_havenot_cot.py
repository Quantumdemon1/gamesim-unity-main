"""A have-not cot: the punishment bed at the cold end of the game room.

A steel tube frame - four legs, two side rails, a head rail - with a thin pale mattress sagging
between them and no pillow. The palette is deliberately cold and the surfaces hard; this is the
one piece in the house meant to look like nowhere anyone wants to sleep. Set dressing only: the
simulation has no have-not mechanic. 0.9 x 1.9 m, 0.5 m tall at the head rail. Origin at the
floor under the centre; the head rail is at -y (Unity -z). Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_havenot_cot.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_havenot_cot")

steel = X.material("steel_cold", (0.45, 0.47, 0.50), metallic=0.75, roughness=0.45)
mattress = X.material("mattress_cold", (0.55, 0.62, 0.70), roughness=0.9)

W, L = 0.9, 1.9
LEG, RAIL_Z = 0.035, 0.30
parts = [
    B.box("leg_a", (LEG, LEG, RAIL_Z), (W / 2 - LEG / 2, L / 2 - LEG / 2, RAIL_Z / 2), steel),
    B.box("leg_b", (LEG, LEG, RAIL_Z), (-W / 2 + LEG / 2, L / 2 - LEG / 2, RAIL_Z / 2), steel),
    B.box("leg_c", (LEG, LEG, RAIL_Z), (W / 2 - LEG / 2, -L / 2 + LEG / 2, RAIL_Z / 2), steel),
    B.box("leg_d", (LEG, LEG, RAIL_Z), (-W / 2 + LEG / 2, -L / 2 + LEG / 2, RAIL_Z / 2), steel),
    B.box("rail_l", (LEG, L, LEG), (W / 2 - LEG / 2, 0.0, RAIL_Z - LEG / 2), steel),
    B.box("rail_r", (LEG, L, LEG), (-W / 2 + LEG / 2, 0.0, RAIL_Z - LEG / 2), steel),
    B.box("rail_foot", (W, LEG, LEG), (0.0, L / 2 - LEG / 2, RAIL_Z - LEG / 2), steel),
    B.box("rail_head", (W, LEG, LEG), (0.0, -L / 2 + LEG / 2, RAIL_Z - LEG / 2), steel),
    # The head rail stands up: two posts and a bar.
    B.box("head_post_a", (LEG, LEG, 0.22), (W / 2 - LEG / 2, -L / 2 + LEG / 2, RAIL_Z + 0.11), steel),
    B.box("head_post_b", (LEG, LEG, 0.22), (-W / 2 + LEG / 2, -L / 2 + LEG / 2, RAIL_Z + 0.11), steel),
    B.box("head_bar", (W, LEG, LEG), (0.0, -L / 2 + LEG / 2, RAIL_Z + 0.22 - LEG / 2), steel),
    # Slats, then the thin mattress lying on them, a little narrower than the frame.
    B.box("slat_a", (W - 2 * LEG, 0.06, 0.02), (0.0, -0.6, RAIL_Z - LEG - 0.01), steel),
    B.box("slat_b", (W - 2 * LEG, 0.06, 0.02), (0.0, 0.0, RAIL_Z - LEG - 0.01), steel),
    B.box("slat_c", (W - 2 * LEG, 0.06, 0.02), (0.0, 0.6, RAIL_Z - LEG - 0.01), steel),
    B.box("mattress", (W - 2 * LEG - 0.04, L - 2 * LEG - 0.04, 0.07), (0.0, 0.0, RAIL_Z - LEG + 0.035), mattress),
]
cot = B.join(parts, "bb_set_havenot_cot")
X.link_only(cot, coll)

path = X.output_path("bb_set_havenot_cot.fbx")
X.export_collection(coll, path)
X.report(coll, path)

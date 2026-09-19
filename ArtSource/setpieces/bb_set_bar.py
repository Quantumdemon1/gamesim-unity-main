"""The game room's bar: an ink counter with a brass top edge and a foot rail. 1.8 x 0.6 x 1.05 m.

Stands in for the kit's kitchenBar. The serving side is -y; the stools go there. Origin at the
floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_bar.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_bar")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
oak = X.material("table_oak", (0.58, 0.42, 0.24), roughness=0.5)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
velvet = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)

L, D, H = 1.8, 0.6, 1.05
parts = [
    B.box("body", (L, D - 0.10, H - 0.05), (0.0, 0.05, (H - 0.05) / 2), ink),
    B.box("front_panel", (L - 0.04, 0.03, H - 0.25), (0.0, -D / 2 + 0.065, 0.10 + (H - 0.25) / 2), velvet),
    B.box("top", (L + 0.06, D, 0.05), (0.0, 0.0, H - 0.025), oak),
    B.box("top_edge", (L + 0.06, 0.02, 0.02), (0.0, -D / 2 + 0.01, H - 0.01), brass),
    B.box("foot_rail", (L - 0.10, 0.03, 0.03), (0.0, -D / 2 - 0.08, 0.16), brass),
    B.box("rail_bracket_a", (0.03, 0.12, 0.03), (-L / 2 + 0.15, -D / 2 - 0.02, 0.16), brass),
    B.box("rail_bracket_b", (0.03, 0.12, 0.03), (L / 2 - 0.15, -D / 2 - 0.02, 0.16), brass),
    B.box("shelf", (L - 0.10, D - 0.20, 0.02), (0.0, 0.10, 0.55), oak),
]
bar = B.join(parts, "bb_set_bar")
X.link_only(bar, coll)
path = X.output_path("bb_set_bar.fbx")
X.export_collection(coll, path)
X.report(coll, path)

"""One leaf of the opening's front door (the art brief's A4; decision D9, an original scripted door).

1.10 m wide, 2.60 m tall and 0.08 m thick, the same on both faces, centred on its origin with its
foot on the floor. Built as a door is: a thinner core, and stiles and rails at the full 0.08 m
framing two panels - the upper 0.70 by 0.95 m about 1.875 m, the lower 0.70 by 0.90 m about
0.75 m - each a field raised off the core with softened edges but sunk below the stiles' faces,
so nothing stands proud of the leaf. A gold strip runs across the middle rail at 1.30 m, 1 mm
proud. Three materials, which OpeningDoorSet maps to its own: bb_mat_door_leaf (core, stiles,
rails), bb_mat_door_panel (the fields) and bb_mat_neon_gold (the strip). Under 3k triangles.
Runtime set dressing: no collider.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_frontdoor_leaf.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_frontdoor_leaf")
body = X.material("door_leaf", (0.30, 0.05, 0.08), roughness=0.45)
panel = X.material("door_panel", (0.40, 0.08, 0.11), roughness=0.4)
gold = X.material("neon_gold", (0.95, 0.72, 0.25), emission=(0.95, 0.72, 0.25), emission_strength=1.0)

T, W, H = 0.08, 1.10, 2.60
CORE = 0.05
PANEL_W = 0.70
STILE = (W - PANEL_W) / 2                       # 0.20 m either side
UPPER = (2.35 - 0.95, 2.35)                     # 1.40 .. 2.35, about 1.875
LOWER = (0.30, 0.30 + 0.90)                     # 0.30 .. 1.20, about 0.75
parts = [
    B.box("core", (CORE, W, H), (0.0, 0.0, H / 2), body),
    B.box("stile", (T, STILE, H), (0.0, -(PANEL_W / 2 + STILE / 2), H / 2), body),
    B.box("stile", (T, STILE, H), (0.0, PANEL_W / 2 + STILE / 2, H / 2), body),
    B.box("top rail", (T, PANEL_W, H - UPPER[1]), (0.0, 0.0, (UPPER[1] + H) / 2), body),
    B.box("middle rail", (T, PANEL_W, UPPER[0] - LOWER[1]), (0.0, 0.0, (LOWER[1] + UPPER[0]) / 2), body),
    B.box("bottom rail", (T, PANEL_W, LOWER[0]), (0.0, 0.0, LOWER[0] / 2), body),
]
for lo, hi in (UPPER, LOWER):
    # The field: raised off the core, sunk below the stiles - a margin all round it is the bevel's shadow.
    parts.append(B.soft_box("field", (T - 0.014, PANEL_W - 0.08, (hi - lo) - 0.08), (0.0, 0.0, (lo + hi) / 2), panel,
                            radius=0.012, segments=2))
parts.append(B.box("rail strip", (T + 0.002, PANEL_W, 0.04), (0.0, 0.0, 1.30), gold))
leaf = B.join(parts, "bb_set_frontdoor_leaf")
X.link_only(leaf, coll)
vs = leaf.data.vertices
print("DOORLEAF %d triangles, x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (
    sum(len(p.vertices) - 2 for p in leaf.data.polygons), min(v.co.x for v in vs), max(v.co.x for v in vs),
    min(v.co.y for v in vs), max(v.co.y for v in vs), min(v.co.z for v in vs), max(v.co.z for v in vs)))
path = X.output_path("bb_set_frontdoor_leaf.fbx")
X.export_collection(coll, path)
X.report(coll, path)

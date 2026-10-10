"""The opening's front door frame (the art brief's A4): jambs, lintel and crown round the doorway.

2.40 m wide and 2.80 m tall overall, its origin on the floor at the middle of the doorway and the
facade's centre plane: two jambs 0.24 m deep and 0.10 m wide standing at +-1.15 m (so the opening
between them is 2.20 m), the lintel closing the doorway's top from 2.60 to 2.70 m, and the crown
from 2.70 to 2.80 m across the frame's full width. Stepped Deco fashion: each jamb and the crown
carry a narrower fascia standing 0.02 m proud on both faces. Two materials, which OpeningDoorSet
maps to its own: bb_mat_neon_gold (jambs, crown, fascias) and bb_mat_door_facade (the lintel).
Runtime set dressing: no collider.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_frontdoor_frame.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_frontdoor_frame")
gold = X.material("neon_gold", (0.95, 0.72, 0.25), emission=(0.95, 0.72, 0.25), emission_strength=1.0)
facade = X.material("door_facade", (0.12, 0.12, 0.16), roughness=0.6)

parts = []
for side in (-1, 1):
    y = side * 1.15
    parts.append(B.box("jamb", (0.24, 0.10, 2.70), (0.0, y, 1.35), gold))
    # The fascia: the jamb's outer half, standing proud of both faces.
    parts.append(B.box("jamb fascia", (0.28, 0.05, 2.62), (0.0, side * 1.175, 1.31), gold))
parts.append(B.box("lintel", (0.20, 2.20, 0.10), (0.0, 0.0, 2.65), facade))
parts.append(B.box("crown", (0.24, 2.40, 0.10), (0.0, 0.0, 2.75), gold))
parts.append(B.box("crown fascia", (0.28, 2.30, 0.04), (0.0, 0.0, 2.77), gold))
frame = B.join(parts, "bb_set_frontdoor_frame")
X.link_only(frame, coll)
vs = frame.data.vertices
print("DOORFRAME %d triangles, x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (
    sum(len(p.vertices) - 2 for p in frame.data.polygons), min(v.co.x for v in vs), max(v.co.x for v in vs),
    min(v.co.y for v in vs), max(v.co.y for v in vs), min(v.co.z for v in vs), max(v.co.z for v in vs)))
path = X.output_path("bb_set_frontdoor_frame.fbx")
X.export_collection(coll, path)
X.report(coll, path)

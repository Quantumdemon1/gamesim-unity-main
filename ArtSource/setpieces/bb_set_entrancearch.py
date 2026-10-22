"""The entrance arch over the yard's way in, which is also the HoH competition's gate (the art brief's A3).

Two posts 0.26 m square at x = -2.3 and +2.3 (the box arch's footprint and centre line, the
SESSION-HANDOFF gate), each stepping back Art-Deco fashion at 2.2, 2.6 and 2.9 m, under a lintel
5.04 by 0.31 by 0.26 m centred at 3.1 m, with a stepped crown rising 0.12 m over its middle 1.2 m.
Neon gold (bb_mat_neon_gold glows by its name). Origin on the floor at the middle of the opening.
Set dressing: no collider.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_entrancearch.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_entrancearch")
gold = X.material("neon_gold", (0.95, 0.72, 0.25), emission=(0.95, 0.72, 0.25), emission_strength=1.0)

POST_X = 2.3
LINTEL_W, LINTEL_H, LINTEL_D, LINTEL_Z = 5.04, 0.31, 0.26, 3.1
bottom = LINTEL_Z - LINTEL_H / 2
top = LINTEL_Z + LINTEL_H / 2

parts = []
for side in (-1, 1):
    x = side * POST_X
    # The post, stepping back at each setback, and a thin reveal band at each step.
    steps = [(0.0, 2.2, 0.26), (2.2, 2.6, 0.22), (2.6, 2.9, 0.18), (2.9, bottom, 0.15)]
    for z0, z1, w in steps:
        parts.append(B.box("post", (w, w, z1 - z0), (x, 0.0, (z0 + z1) / 2)))
        if z0 > 0.0:
            parts.append(B.box("band", (w + 0.03, w + 0.03, 0.03), (x, 0.0, z0 + 0.015)))
    parts.append(B.box("plinth", (0.32, 0.32, 0.12), (x, 0.0, 0.06)))
parts.append(B.box("lintel", (LINTEL_W, LINTEL_D, LINTEL_H), (0.0, 0.0, LINTEL_Z)))
# The crown: two steps over the middle 1.2 m, 0.06 m each.
parts.append(B.box("crown", (1.2, LINTEL_D - 0.04, 0.06), (0.0, 0.0, top + 0.03)))
parts.append(B.box("crown", (0.8, LINTEL_D - 0.08, 0.06), (0.0, 0.0, top + 0.09)))
for part in parts:
    part.data.materials.append(gold)
piece = B.join(parts, "bb_set_entrancearch")
X.link_only(piece, coll)
vs = piece.data.vertices
print("ARCH %d triangles, x %.3f..%.3f, z %.3f..%.3f" % (sum(len(p.vertices) - 2 for p in piece.data.polygons),
                                                       min(v.co.x for v in vs), max(v.co.x for v in vs),
                                                       min(v.co.z for v in vs), max(v.co.z for v in vs)))
path = X.output_path("bb_set_entrancearch.fbx")
X.export_collection(coll, path)
X.report(coll, path)

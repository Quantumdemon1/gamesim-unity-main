"""The front door's lever handle, on both faces of a leaf (the art brief's A4).

A pair of levers through the leaf: on each face a 0.05 m rose 0.01 m thick, a 0.018 m neck and a
0.14 m lever of 0.018 m round, standing no more than 0.065 m proud of the face (the leaf is
0.08 m thick, so the pair spans 0.208 m). Mirror-symmetric through the leaf's centre plane, so it
is centred on its origin like every set piece, with its foot (the roses' bottom edge) on z = 0;
OpeningDoorSet places it on the leaf. The levers point along y, across the leaf. One material,
bb_mat_neon_gold. Runtime set dressing: no collider.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_frontdoor_handle.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_frontdoor_handle")
gold = X.material("neon_gold", (0.95, 0.72, 0.25), emission=(0.95, 0.72, 0.25), emission_strength=1.0)

FACE = 0.04            # half the leaf's thickness
ROSE_R, ROSE_T = 0.025, 0.010
NECK_R, NECK_L = 0.009, 0.045
LEVER = 0.14
Z = ROSE_R             # the roses' centre: their bottom edge on the floor of the piece
parts = []
for side in (-1, 1):
    x0 = side * FACE
    x1 = side * (FACE + ROSE_T)
    x2 = side * (FACE + ROSE_T + NECK_L)
    parts.append(B.tube_along("rose", [(x0, 0.0, Z), (x1, 0.0, Z)], ROSE_R, 16, gold))
    parts.append(B.tube_along("neck", [(x1, 0.0, Z), (x2, 0.0, Z)], NECK_R, 10, gold))
    parts.append(B.tube_along("lever", [(x2, -NECK_R, Z), (x2, LEVER / 2, Z), (x2, LEVER - NECK_R, Z)], NECK_R, 10, gold))
# Centre the pair in y: the levers run from the roses' axis to one side.
handle = B.join(parts, "bb_set_frontdoor_handle")
ys = [v.co.y for v in handle.data.vertices]
shift = -(min(ys) + max(ys)) / 2
for v in handle.data.vertices:
    v.co.y += shift
X.link_only(handle, coll)
vs = handle.data.vertices
print("DOORHANDLE %d triangles, x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f, proud %.3f, rose axis at y %.3f" % (
    sum(len(p.vertices) - 2 for p in handle.data.polygons), min(v.co.x for v in vs), max(v.co.x for v in vs),
    min(v.co.y for v in vs), max(v.co.y for v in vs), min(v.co.z for v in vs), max(v.co.z for v in vs),
    max(v.co.x for v in vs) - FACE, shift))
path = X.output_path("bb_set_frontdoor_handle.fbx")
X.export_collection(coll, path)
X.report(coll, path)

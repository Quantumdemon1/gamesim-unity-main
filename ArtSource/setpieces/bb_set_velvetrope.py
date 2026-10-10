"""A red velvet rope between two entrance stanchions (the art brief's A3; decision D8, red velvet).

A 1.52 m run of 0.04 m rope hanging in a catenary 0.12 m deep, twelve sides round and
twenty-four lengths along, with a 0.04 m brass snap hook at each end. The lowest vertex is on
z = 0 and the origin under the middle of the span, so the house places it with a lift of 0.72 m:
its ends then meet the stanchions' eyes at 0.86 m. The rope runs along x. Set dressing: no
collider.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_velvetrope.py -- <output.fbx>
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_velvetrope")
velvet = X.material("velvet_red", (0.42, 0.03, 0.06), roughness=0.9)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

SPAN, SAG, R = 1.52, 0.12, 0.02
HOOK = 0.04
ROPE = SPAN - 2 * HOOK * 0.5          # the rope proper, the hooks' halves beyond it at each end
HALF = ROPE / 2.0

# The catenary a cosh(x / a) - a through the ends with the sag asked for: solve for a.
lo, hi = 0.05, 50.0
for _ in range(80):
    a = (lo + hi) / 2.0
    if a * (math.cosh(HALF / a) - 1.0) > SAG:
        lo = a
    else:
        hi = a
a = (lo + hi) / 2.0
points = []
for i in range(25):
    x = -HALF + ROPE * i / 24
    points.append((x, 0.0, R + a * (math.cosh(x / a) - 1.0)))
end_z = points[-1][2]
parts = [B.tube_along("rope", points, R, 12, velvet)]
# The snap hooks: a brass sleeve over each end and a ring beyond it that clips to the eye.
for side in (-1, 1):
    x0 = side * HALF
    sleeve = B.tube_along("sleeve", [(x0 - side * 0.012, 0.0, end_z), (x0 + side * 0.006, 0.0, end_z)], R + 0.004, 12, brass)
    ring = B.torus("hook", 0.009, 0.0035, (x0 + side * (0.006 + 0.009), 0.0, end_z), axis='Y', mat=brass,
                   major_segments=12, minor_segments=6)
    parts += [sleeve, ring]
piece = B.join(parts, "bb_set_velvetrope")
X.link_only(piece, coll)
vs = piece.data.vertices
print("VELVETROPE %d triangles, x %.3f..%.3f, z %.3f..%.3f, sag %.3f" % (
    sum(len(p.vertices) - 2 for p in piece.data.polygons), min(v.co.x for v in vs), max(v.co.x for v in vs),
    min(v.co.z for v in vs), max(v.co.z for v in vs), end_z - R))
path = X.output_path("bb_set_velvetrope.fbx")
X.export_collection(coll, path)
X.report(coll, path)

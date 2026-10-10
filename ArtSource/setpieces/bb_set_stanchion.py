"""A brass queue stanchion for the entrance's red velvet rope (the art brief's A3).

1.02 m tall, turned: a 0.32 m base 0.04 m thick with a 0.01 m bevel, a dome rising to a 0.10 m
neck at 0.08 m, a 0.05 m post from 0.08 to 0.92 m with 0.065 m collars at 0.10 and 0.90 m, and a
0.09 m finial ball centred at 0.97 m. Two ring eyes at 0.86 m face along x, where the ropes hook
on. Origin on the floor under the post. Set dressing: no collider (a pole this thin is "too small"
for the house's collision, HouseLayersTests), and under 1.5k triangles.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_stanchion.py -- <output.fbx>
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_stanchion")
# The house's own brass (bb_mat_frame_brass is the extracted, tuned material beside the set pieces).
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)

POST = 0.025
profile = [
    (0.0, 0.0), (0.15, 0.0), (0.16, 0.01), (0.16, 0.03), (0.15, 0.04),
    # The dome from the base up to the neck.
    (0.13, 0.048), (0.10, 0.060), (0.075, 0.070), (0.05, 0.080),
    # The post and its lower collar.
    (POST, 0.082), (POST, 0.094), (0.0325, 0.096), (0.0325, 0.104), (POST, 0.106),
    # Up to the upper collar.
    (POST, 0.894), (0.0325, 0.896), (0.0325, 0.904), (POST, 0.906), (POST, 0.928),
]
# The finial: a ball of 0.045 m radius about 0.97 m, from where it meets the post to its top.
for k in range(1, 9):
    theta = math.radians(35 + (180 - 35) * k / 8)
    profile.append((0.045 * math.sin(theta), 0.97 - 0.045 * math.cos(theta)))
profile[-1] = (0.0, 0.97 + 0.045)

parts = [B.lathe("post", profile, 16, brass)]
for side in (-1, 1):
    parts.append(B.torus("eye", 0.012, 0.004, (side * (POST + 0.012), 0.0, 0.86), axis='Y', mat=brass,
                         major_segments=12, minor_segments=6))
piece = B.join(parts, "bb_set_stanchion")
X.link_only(piece, coll)
print("STANCHION %d triangles, height %.3f" % (sum(len(p.vertices) - 2 for p in piece.data.polygons),
                                                max(v.co.z for v in piece.data.vertices)))
path = X.output_path("bb_set_stanchion.fbx")
X.export_collection(coll, path)
X.report(coll, path)

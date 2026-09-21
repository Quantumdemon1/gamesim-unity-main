"""A competition lane: the pair of lit lines that divide the yard into a course.

mockup-05's strongest graphic is not a prop at all - it is the light on the ground. Three lanes
run away from the camera, each edged in its own colour, and they are what turns a patio into a
course. They also read from directly overhead, which the props do not: at the competition camera's
height a podium is a dark rectangle and a lane is a stripe of colour.

Sized from the yard rather than from the picture: the competition floor is 28 x 10 m spanning
z 10 to 20, and the three podiums stand 6 m apart on z 17. So a lane is 4.8 m across and 9 m long,
which leaves 1.2 m of dark deck between neighbours - the gap the mockup shows - and stops half a
metre short of each end. It lies 2 cm proud of the deck so it catches the edge light rather than
z-fighting the floor. Authored white and named for its glow; the placement
step tints each of the three to its lane. Origin on the floor at the lane's own centre, between the strips,
because every prop here is centred on its origin so a plan can place it by its middle - a rule the
import tests enforce and one this piece was written against the first time round. A floor graphic,
so no collider.

    blender --background --python ArtSource/setpieces/bb_set_comp_lane.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_comp_lane")

neon = X.material("neon_lane", (0.90, 0.94, 1.00), roughness=0.25,
                  emission=(1.0, 1.0, 1.0), emission_strength=3.0)

WIDTH = 4.80          # centre to centre of the two strips
LENGTH = 9.0          # the run, from the start line to the back wall
STRIP = 0.07          # how wide each line is
RISE = 0.02           # proud of the deck, so it is lit rather than coplanar
HALF = WIDTH / 2

parts = [
    B.box("edge_l", (STRIP, LENGTH, RISE), (-HALF, 0.0, RISE / 2), neon),
    B.box("edge_r", (STRIP, LENGTH, RISE), (HALF, 0.0, RISE / 2), neon),
    # The start line closes the near end, so a lane reads as a lane and not as two loose lines.
    B.box("start", (WIDTH + STRIP, STRIP, RISE), (0.0, -LENGTH / 2 + STRIP / 2, RISE / 2), neon),
]

lane = B.join(parts, "bb_set_comp_lane")
X.link_only(lane, coll)
path = X.output_path("bb_set_comp_lane.fbx")
X.export_collection(coll, path)
X.report(coll, path)

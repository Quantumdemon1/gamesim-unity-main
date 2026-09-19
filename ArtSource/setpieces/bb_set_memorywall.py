"""The memory wall: sixteen lit frames on a dark backing, hung on the living room's west wall.

The frames are geometry; the portraits are the RenderTextures MemoryWall paints at runtime into
a quad the builder adds in front of each frame. Every frame is its own object - the runtime tints
each one's emission by the houseguest's status - so the export is an empty root with seventeen
children: the backing and frames f00-f15, two rows of eight in name order, column 0 at -y
(Unity -z, the south end). The root origin is the floor directly beneath the wall's centre; the
front faces +x, the way the builder hangs it just proud of the wall's inner face. Nothing here
stands on the floor, so the checklist's floor rule is met by the root being an empty, and the
Unity-side test exempts the piece by name.

    blender --background --python ArtSource/setpieces/bb_set_memorywall.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

import bpy  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_memorywall")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
gold = X.material("neon_gold", (0.92, 0.72, 0.22), metallic=0.6, roughness=0.3,
                  emission=(1.0, 0.76, 0.28), emission_strength=2.0)

# The grid. MemoryWall re-centres the frames a season actually uses along these rows at runtime,
# so the pitch and the row heights are what the builder and the runtime both read back from the
# placed frames - they are not repeated anywhere in C#.
ROWS, COLS = 2, 8
FRAME, GAP, BORDER = 0.53, 0.053, 0.045
MOUNT = 0.75                      # the centre of a 1.5 m cutaway wall
PITCH = FRAME + GAP
SPAN_Y = COLS * FRAME + (COLS - 1) * GAP     # along the wall (Unity z)
SPAN_Z = ROWS * FRAME + (ROWS - 1) * GAP     # up (Unity y)
BACK_T, FRAME_T = 0.04, 0.03

root = bpy.data.objects.new("bb_set_memorywall", None)
X.link_only(root, coll)

backing = B.box("bb_set_memorywall_backing", (BACK_T, SPAN_Y + 2 * GAP, SPAN_Z + 2 * GAP),
                (-BACK_T / 2 - 0.01, 0.0, MOUNT), ink)
X.link_only(B.child(backing, root), coll)

index = 0
for row in range(ROWS):
    for col in range(COLS):
        y = (col - (COLS - 1) / 2.0) * PITCH
        z = MOUNT + ((ROWS - 1) / 2.0 - row) * PITCH
        outer = FRAME + 2 * BORDER
        # Four strips make the ring: the runtime's portrait quad covers the middle.
        strips = [
            B.box("top", (FRAME_T, outer, BORDER), (FRAME_T / 2, y, z + FRAME / 2 + BORDER / 2), gold),
            B.box("bottom", (FRAME_T, outer, BORDER), (FRAME_T / 2, y, z - FRAME / 2 - BORDER / 2), gold),
            B.box("left", (FRAME_T, BORDER, FRAME), (FRAME_T / 2, y - FRAME / 2 - BORDER / 2, z), gold),
            B.box("right", (FRAME_T, BORDER, FRAME), (FRAME_T / 2, y + FRAME / 2 + BORDER / 2, z), gold),
        ]
        frame = B.join(strips, "bb_set_memorywall_f%02d" % index)
        X.link_only(B.child(frame, root), coll)
        index += 1

path = X.output_path("bb_set_memorywall.fbx")
X.export_collection(coll, path)
X.report(coll, path)

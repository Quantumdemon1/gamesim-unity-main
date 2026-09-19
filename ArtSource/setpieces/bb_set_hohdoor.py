"""The HoH door: the one with the key.

Dresses the 2.8 m gap in the south wing's first divider, where the nomination room opens into
the HoH suite: a brass threshold across the gap, a jamb at each end, and the door itself - a
dark leaf with the show's gold key window - standing open into the suite, hinged at the north
jamb and swung ninety degrees so it never reads as blocking the way (the divider's collider is
what the NavMesh knows; this is visual). A gold HoH plaque sits on the south jamb. The wing's
walls are 1.1 m, so the whole piece is 1.1 m: a door that tops its own wall would hang in the sky
of the cutaway.

Origin at the floor at the gap's centre; the gap runs along +/-y (Unity z); the suite is at -x,
the nomination room at +x. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_hohdoor.py -- <output.fbx>
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_hohdoor")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
gold = X.material("neon_gold", (0.92, 0.72, 0.22), metallic=0.6, roughness=0.3,
                  emission=(1.0, 0.76, 0.28), emission_strength=2.0)
velvet = X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75)

GAP, H, JAMB, T = 2.8, 1.1, 0.16, 0.14
LEAF_W, LEAF_T = 0.9, 0.05

parts = [
    B.box("threshold", (0.30, GAP + 2 * JAMB, 0.03), (0.0, 0.0, 0.015), brass),
    B.box("jamb_n", (T, JAMB, H), (0.0, GAP / 2 + JAMB / 2, H / 2), ink),
    B.box("jamb_s", (T, JAMB, H), (0.0, -GAP / 2 - JAMB / 2, H / 2), ink),
    B.box("jamb_cap_n", (T + 0.04, JAMB + 0.04, 0.04), (0.0, GAP / 2 + JAMB / 2, H - 0.02), brass),
    B.box("jamb_cap_s", (T + 0.04, JAMB + 0.04, 0.04), (0.0, -GAP / 2 - JAMB / 2, H - 0.02), brass),
    # The plaque on the south jamb's suite-facing side.
    B.box("plaque", (0.02, 0.10, 0.16), (-T / 2 - 0.01, -GAP / 2 - JAMB / 2, 0.78), gold),
]

# The leaf: built closed along +y from the north jamb, then swung open into the suite. It is one
# object, so tilted_box's hinge rule does the turn: the hinge is the north jamb's inner edge.
hinge_y = GAP / 2
swing = math.radians(90.0)
leaf_parts = [
    B.box("leaf", (LEAF_T, LEAF_W, H - 0.05), (0.0, 0.0, (H - 0.05) / 2), ink),
    B.box("leaf_rail_top", (LEAF_T + 0.02, LEAF_W, 0.05), (0.0, 0.0, H - 0.075), brass),
    B.box("leaf_rail_bottom", (LEAF_T + 0.02, LEAF_W, 0.05), (0.0, 0.0, 0.075), brass),
    # The key window: a gold bow ring and a shank, proud of the leaf on the suite side.
    B.ring("key_bow", 0.11, 0.06, 0.02, (0.0, 0.0, 0.0), gold, segments=32),
    B.box("key_shank", (0.02, 0.06, 0.26), (-LEAF_T / 2 - 0.01, 0.0, 0.43), gold),
    B.box("key_bit", (0.02, 0.10, 0.06), (-LEAF_T / 2 - 0.01, 0.03, 0.33), gold),
    B.box("key_bit_2", (0.02, 0.08, 0.05), (-LEAF_T / 2 - 0.01, 0.02, 0.42), gold),
]
# The bow ring is built flat; stand it in the leaf's plane (rotate about y) and lift it.
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

bow = leaf_parts[3]
bow.data.transform(Matrix.Rotation(math.radians(90.0), 4, 'Y'))          # its axis along x
bow.data.transform(Matrix.Translation(Vector((-LEAF_T / 2 - 0.02, 0.0, 0.70))))  # on the suite face
leaf = B.join(leaf_parts, "leaf_closed")
# Shift the leaf so its hinge edge sits on the north jamb, then swing it open into the suite.
leaf.data.transform(Matrix.Translation(Vector((0.0, hinge_y - LEAF_W / 2, 0.0))))
about = Matrix.Translation(Vector((0.0, hinge_y, 0.0))) @ Matrix.Rotation(-swing, 4, 'Z') @ Matrix.Translation(Vector((0.0, -hinge_y, 0.0)))
leaf.data.transform(about)
parts.append(leaf)

door = B.join(parts, "bb_set_hohdoor")
X.link_only(door, coll)

path = X.output_path("bb_set_hohdoor.fbx")
X.export_collection(coll, path)
X.report(coll, path)

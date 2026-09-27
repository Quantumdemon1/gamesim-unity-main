"""The ceremony screen: the lit board the house turns to face, on its own low stage.

mockup-10 is the nomination ceremony, and its centre is not a card over the picture - it is a
screen in the room, in a neon frame, with the whole cast sitting on the sofas watching it. That is
how the mockups solve a problem the build solves with a 97.5% scrim: they do not dim the house to
make the beat readable, they give the house something to look at. A room with a focal point can be
lit; a room without one has to be hidden.

Free-standing, and that is deliberate. This house's walls are 1.1 m cutaways so the overview camera
can see down into the rooms, so there is nothing to hang a 1.5 m screen on. Like the yard's
backdrop it stands on the floor instead: a 4.0 x 1.6 m stage 0.25 m high, two posts, and the board
between them with its sill at 1.0 m - above a seated head and below the camera's line.

The board is emissive and named for its glow, so it reads as switched on in a dark room without a
light on it. Its frame is authored white and named for its neon, so the placement step can tint it;
mockup-10's is red. Origin at the centre of the stage, on the floor, face toward -y (Unity -z).
Set dressing, so no collider: the room's own walk area is unchanged and the NavMesh was baked
without it.

    blender --background --python ArtSource/setpieces/bb_set_ceremonyscreen.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_ceremonyscreen")

ink = X.material("ink_stage", (0.05, 0.05, 0.07), metallic=0.25, roughness=0.6)
steel = X.material("frame_post", (0.20, 0.21, 0.24), metallic=0.7, roughness=0.4)
neon = X.material("neon_frame", (0.92, 0.94, 1.00), roughness=0.25,
                  emission=(1.0, 1.0, 1.0), emission_strength=3.0)
screen = X.material("glow_screen", (0.09, 0.16, 0.30), roughness=0.35,
                    emission=(0.16, 0.34, 0.68), emission_strength=1.6)

STAGE_W, STAGE_D, STAGE_H = 4.0, 1.6, 0.25
BOARD_W, BOARD_H = 2.60, 1.50
SILL = 1.00                 # the board's bottom edge above the floor
TUBE = 0.10                 # the neon frame's square section
POST = 0.12
FACE = -STAGE_D / 2 + 0.22  # how far forward of the stage's middle the board sits

parts = [
    B.box("stage", (STAGE_W, STAGE_D, STAGE_H), (0.0, 0.0, STAGE_H / 2), ink),
    # A lit reveal under the stage's lip, the same trick the podium and the stack use to lift a
    # dark prop off a dark floor.
    B.box("stage_glow", (STAGE_W - 0.12, STAGE_D - 0.12, 0.03), (0.0, 0.0, STAGE_H - 0.04), neon),

    # The two posts the board hangs between, standing on the stage.
    B.box("post_l", (POST, POST, SILL + BOARD_H + TUBE - STAGE_H),
          (-(BOARD_W / 2 + TUBE), FACE, STAGE_H + (SILL + BOARD_H + TUBE - STAGE_H) / 2), steel),
    B.box("post_r", (POST, POST, SILL + BOARD_H + TUBE - STAGE_H),
          (BOARD_W / 2 + TUBE, FACE, STAGE_H + (SILL + BOARD_H + TUBE - STAGE_H) / 2), steel),

    # The board itself, set back a little so the frame stands proud of it.
    B.box("board", (BOARD_W, 0.08, BOARD_H), (0.0, FACE + 0.04, SILL + BOARD_H / 2), screen),

    # The neon frame around it: two uprights and two rails.
    B.box("frame_l", (TUBE, TUBE, BOARD_H + TUBE * 2),
          (-(BOARD_W / 2 + TUBE / 2), FACE - 0.03, SILL + BOARD_H / 2), neon),
    B.box("frame_r", (TUBE, TUBE, BOARD_H + TUBE * 2),
          (BOARD_W / 2 + TUBE / 2, FACE - 0.03, SILL + BOARD_H / 2), neon),
    B.box("frame_top", (BOARD_W + TUBE * 2, TUBE, TUBE),
          (0.0, FACE - 0.03, SILL + BOARD_H + TUBE / 2), neon),
    B.box("frame_bottom", (BOARD_W + TUBE * 2, TUBE, TUBE),
          (0.0, FACE - 0.03, SILL - TUBE / 2), neon),
]

unit = B.join(parts, "bb_set_ceremonyscreen")
X.link_only(unit, coll)
path = X.output_path("bb_set_ceremonyscreen.fbx")
X.export_collection(coll, path)
X.report(coll, path)

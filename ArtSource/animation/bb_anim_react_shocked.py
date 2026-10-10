"""React_shocked: a startle, both hands over the mouth (left over right), held, then to the chest.

78 frames (2.6 s) at 30 fps; the controller leaves it at 0.95 (f74). From the art brief's R2:
- f0: the base.
- f5, startle: Spine1, Spine2 and Neck flex -3, Head -5; both shoulders raise +10; both arms
  flex +25, abd +8; elbows bend +40; hands flex -10; fingers curl 8.
- f14, hands over the mouth: Spine2 and Neck flex +2, Head +5; the left palm 7.5 cm in front of
  the mouth and 5 mm to the left, the right 6 cm in front and 1.5 cm lower, palms to the face;
  hands flex -5; fingers 25/20/15; thumbs 10.
- f14-f54, held: breathing (1.2 s, depth 1.5) and the head shaking +5/-5 every 8 frames.
- f62: both hands to the chest, 3 cm in front of the sternum, the left 3 cm above the right and
  laid over it; Head flex +3; fingers 15.
- f72: the base, breathing to f78.

    blender --background --factory-startup --python ArtSource/animation/bb_anim_react_shocked.py -- [out.fbx]
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tools"))
import bb_anim  # noqa: E402
from bb_anim import F, L, U, both, merge  # noqa: E402

rig = bb_anim.load()
base = rig.base()
hang = rig.hang()
m = rig.m
take = bb_anim.Take(rig, "React_shocked", 78, exit_at=74)

take.key(0, base)
# The base holds its hands behind its back: they let go and come out beside the hips on the way,
# so they go round the body to the front rather than through it (and back the same way at f72).
take.leave_base(0, 5)

startle = rig.turn(hang, merge(
    {"Spine1": {"flex": -3}, "Spine2": {"flex": -3}, "Neck": {"flex": -3}, "Head": {"flex": -5}},
    both("Shoulder", {"raise": 10}), both("Arm", {"flex": 25, "abd": 8}), both("ForeArm", {"bend": 40}),
    both("Hand", {"flex": -10}), {"LeftFingers": (8, 8, 8), "RightFingers": (8, 8, 8)}))
take.key(5, startle)
print("SANITY f5 left (flex, abd, bend)", rig.arm_angles(startle, "Left"), "right", rig.arm_angles(startle, "Right"),
      "hang", rig.arm_angles(hang, "Left"), "base", rig.arm_angles(base, "Left"))

mouth = rig.turn(hang, merge(
    {"Spine2": {"flex": 2}, "Neck": {"flex": 2}, "Head": {"flex": 5}}, both("Hand", {"flex": -5}),
    {"LeftFingers": (25, 20, 15), "RightFingers": (25, 20, 15), "LeftThumb": (10, 10, 10), "RightThumb": (10, 10, 10)}))
lm = rig.landmarks(mouth)
toward_face = -lm["Fh"]
mouth = rig.reach(mouth, "Left", rig.face(lm, "M", 0.075, left=0.005),
                  pole=(-U + F * 0.5 - L * 0.2).normalized(), palm=toward_face)
mouth = rig.reach(mouth, "Right", rig.face(lm, "M", 0.06, up=-0.015),
                  pole=(-U + F * 0.5 + L * 0.2).normalized(), palm=toward_face)
print("SANITY f14 left (flex, abd, bend)", rig.arm_angles(mouth, "Left"), "right", rig.arm_angles(mouth, "Right"))
take.key(14, mouth)
take.key(54, mouth)
take.hold(14, 54)
take.breathe(14, 54, 1.2, 1.5)
take.curve("Head", "yaw", [(14, 0.0), (22, 5.0), (30, -5.0), (38, 5.0), (46, -5.0), (54, 0.0)])

chest = rig.turn(hang, merge({"Head": {"flex": 3}}, {"LeftFingers": (15, 15, 15), "RightFingers": (15, 15, 15)}))
lm = rig.landmarks(chest)
chest = rig.reach(chest, "Right", lm["S"] + F * m(0.03) - U * m(0.015), pole=(-U + L * 0.3).normalized(), palm=-F)
chest = rig.reach(chest, "Left", lm["S"] + F * m(0.05) + U * m(0.015), pole=(-U - L * 0.3).normalized(), palm=-F)
print("SANITY f62 left", rig.arm_angles(chest, "Left"), "right", rig.arm_angles(chest, "Right"))
take.key(62, chest)

take.return_to_base(62, 72)
take.micro(6, 72)
take.breathe(72, 78, 4.0, 1.0)

act = take.bake()
bb_anim.finish(rig, act, take, "bb_anim_React_shocked.fbx")

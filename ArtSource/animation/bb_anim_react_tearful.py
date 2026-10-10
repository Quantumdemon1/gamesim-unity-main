"""React_tearful: the head bows, one hand to the eyes and the other arm across the waist, three
sobs and two wipes, the hand to the mouth, then home.

120 frames (4.0 s) at 30 fps; the controller leaves it at 0.95 (f114). From the art brief's R2:
- f12: Neck flex +8, Head +10; both shoulders fwd +8, raise -3; Spine1 and Spine2 +3 (the arms
  let go of the base's clasp on the way and hang).
- f20: the right palm 10 cm in front of the eyes, towards them, hand flex +10, fingers 20; the
  left forearm across the waist (its palm at the Spine joint + F 0.17 - L 0.06 + U 0.02), fingers 30.
- Sobs from f34, f52 and f70, each peaking 6 frames on (shoulders raise +5, Spine2 +3, Head +2)
  and gone 14 frames after it starts.
- Wipes at f40 and f76: the right palm 9 cm in front of the eyes moves from 3 cm right (f37/f73)
  to 3 cm left (f43/f79) and back to the f20 place (f46/f82).
- f96: the right palm 7 cm in front of the mouth.
- f104: the arms home to the base's clasp, the head still bowed; f106-f112 the neck and head come
  up; f112 the base, breathing to f120.
- Breathing over f20-f96 (2.0 s, depth 1.2) under the sobs.

    blender --background --factory-startup --python ArtSource/animation/bb_anim_react_tearful.py -- [out.fbx]
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tools"))
import bb_anim  # noqa: E402
from bb_anim import F, L, U, both, merge  # noqa: E402

rig = bb_anim.load()
base = rig.base()
m = rig.m
take = bb_anim.Take(rig, "React_tearful", 120, exit_at=114)

bow = merge({"Neck": {"flex": 8}, "Head": {"flex": 10}, "Spine1": {"flex": 3}, "Spine2": {"flex": 3}},
            both("Shoulder", {"fwd": 8, "raise": -3}))

take.key(0, base)
take.leave_base(0, 12, bow)
bowed = rig.hang_with(bow)
take.key(12, bowed)


def weeping(eyes_target, hand_flex=10):
    """The f20 shape with the right palm at a given point: right to the eyes, left across the waist."""
    pose = rig.turn(bowed, {"RightHand": {"flex": hand_flex}, "RightFingers": (20, 20, 20), "LeftFingers": (30, 30, 30)})
    lm = rig.landmarks(pose)
    pose = rig.reach(pose, "Right", eyes_target(lm), pole=(-U + F * 0.3 - L * 0.4).normalized(), palm=-lm["Fh"])
    waist = lm["Spine"] + F * m(0.17) - L * m(0.06) + U * m(0.02)
    pose = rig.reach(pose, "Left", waist, pole=(-U * 0.6 + L).normalized(), palm=-F)
    return pose


at_eyes = weeping(lambda lm: rig.face(lm, "E", 0.10))
print("SANITY f20 right (flex, abd, bend)", rig.arm_angles(at_eyes, "Right"), "left", rig.arm_angles(at_eyes, "Left"))
take.key(20, at_eyes)
take.hold(20, 37)
for start in (37, 73):
    take.key(start, weeping(lambda lm: rig.face(lm, "E", 0.09, left=-0.03)))
    take.key(start + 6, weeping(lambda lm: rig.face(lm, "E", 0.09, left=0.03)))
    take.key(start + 9, at_eyes)
take.hold(46, 73)
take.key(96, weeping(lambda lm: rig.face(lm, "M", 0.07)))

# The arms go home to the clasp while the head is still down; then the head comes up.
take.return_to_base(96, 104, bow)
take.key(106, rig.turn(base, bow))
take.key(112, base)

for start in (34, 52, 70):
    sob = bb_anim.bump(start, start + 6, start + 14, 1.0)
    take.layer(lambda f, sob=sob: {"LeftShoulder": {"raise": 5 * sob(f)}, "RightShoulder": {"raise": 5 * sob(f)},
                                   "Spine2": {"flex": 3 * sob(f)}, "Head": {"flex": 2 * sob(f)}})
take.breathe(20, 96, 2.0, 1.2)
take.micro(12, 106)
take.breathe(112, 120, 4.0, 1.0)

act = take.bake()
bb_anim.finish(rig, act, take, "bb_anim_React_tearful.fbx")

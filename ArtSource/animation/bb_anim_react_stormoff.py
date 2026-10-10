"""React_stormoff: turning away, a flick of the hand, a chop across the body, the head shaking,
then home - the beat a body plays before it walks out of the room.

48 frames (1.6 s) at 30 fps; the controller leaves it at 0.95 (f45), and it gives way to the walk
as soon as the body is sent somewhere. From the art brief's R2:
- f6, turning away: Neck yaw -10, Head -20 (30 degrees to the body's left); Spine2 yaw -8,
  Spine1 -3.6 (the brief's -4 puts the chest on rule 5's 12-degree limit and the breath over
  it); the arms let go of the base's clasp on the way and hang.
- f10, flick: right arm flex +60, abd +30, rot -30; bend +80; hand flex -25 (palm out); fingers 10.
- f16 -> f21, a 5-frame chop across the body to the left hip: the right palm at LeftUpLeg +
  F 0.22 + U 0.10, at least 0.12 m off the hips.
- The head shakes: Head yaw -28 at f22, -36 at f25, -22 at f28, -28 at f31.
- f34: right flex +5, bend +15. f40: Spine1 and Spine2 yaw -2, Head -10. f44: the base.

    blender --background --factory-startup --python ArtSource/animation/bb_anim_react_stormoff.py -- [out.fbx]
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tools"))
import bb_anim  # noqa: E402
from bb_anim import F, L, U, both, merge  # noqa: E402

rig = bb_anim.load()
base = rig.base()
m = rig.m
take = bb_anim.Take(rig, "React_stormoff", 48, exit_at=45)

# The brief's chest turn (Spine2 -8, Spine1 -4) is exactly rule 5's 12 degrees, and the breath
# under it measures 12.05; Spine1 turns 3.6 so the chest holds at about 11.6.
away = {"Spine1": {"yaw": -3.6}, "Spine2": {"yaw": -8}, "Neck": {"yaw": -10}}

take.key(0, base)
take.leave_base(0, 6)
take.key(6, rig.hang_with(away))
flick = rig.turn(rig.hang_with(away), {"RightArm": {"flex": 60, "abd": 30, "rot": -30}, "RightForeArm": {"bend": 80},
                                       "RightHand": {"flex": -25}, "RightFingers": (10, 10, 10)})
take.key(10, flick)
take.key(16, flick)
chop = rig.turn(rig.hang_with(away), {"RightFingers": (10, 10, 10)})
lm = rig.landmarks(chop)
chop = rig.reach(chop, "Right", lm["LeftUpLeg"] + F * m(0.22) + U * m(0.10), pole=(-L + U * 0.2 - F * 0.2).normalized(), palm=-U)
print("SANITY f21 right (flex, abd, bend)", rig.arm_angles(chop, "Right"))
take.key(21, chop)
take.key(34, rig.hang_with(away, {"RightArm": {"flex": 5}, "RightForeArm": {"bend": 15}}))
take.key(40, rig.hang_with({"Spine1": {"yaw": -2}, "Spine2": {"yaw": -2}}))
take.return_to_base(40, 44)

# The head's turn rides on top of the keys: away with the chest, a shake, then home.
take.curve("Head", "yaw", [(0, 0.0), (6, -20.0), (21, -20.0), (22, -28.0), (25, -36.0), (28, -22.0), (31, -28.0),
                           (34, -24.0), (40, -10.0), (44, 0.0)])
take.micro(6, 44)
take.breathe(6, 44, 1.2, 1.0)
take.breathe(44, 48, 4.0, 1.0)

act = take.bake()
mats = rig.fk(rig.sample(act, 21))
hips_gap = bb_anim.seg_dist(rig.palm(mats, "Right"), rig.head(mats, "LeftUpLeg"), rig.head(mats, "RightUpLeg")) * rig.unit
print("SANITY f21 right palm %.3f m from the hip line" % hips_gap)
if hips_gap < 0.12:
    raise AssertionError("the chop's hand is %.3f m off the hips, under 0.12" % hips_gap)
bb_anim.finish(rig, act, take, "bb_anim_React_stormoff.fbx")

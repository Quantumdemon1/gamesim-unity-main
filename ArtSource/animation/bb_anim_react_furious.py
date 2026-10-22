"""React_furious: a lean in with fists, arms thrown out palms up, two jabs of a pointing finger
with the other fist on the hip, hands to the temples, a wind-up and a chop, a hard breath, home.

90 frames (3.0 s) at 30 fps; the controller leaves it at 0.95 (f85). The director turns the body
to the person it is furious with before the clip starts; nothing here moves the hips or the feet.
From the art brief's R2:
- f8, lean in: Spine1 and Spine2 flex +4, Neck -4; fists (fingers 70/90/60, thumbs 35); both arms
  flex +10, elbows bend +30.
- f16, arms thrown out palms up: both arms flex +30, abd +35, rot -45; bend +20; hands flex -15;
  fingers 5.
- f22: fists again, arms flex +10, bend +60.
- f30, jab: right arm flex +75, abd +5, bend +15, the index straight and the other fingers
  80/90/60, thumb 40; Spine2 yaw +6; the left fist at the hip (LeftUpLeg + L 0.14 + U 0.06 +
  F 0.03). f36: right flex +45, bend +70. f42 as f30, f48 as f36.
- f54, hands to the temples: each palm 3 cm out from its temple and 4 cm forward, fingers 15;
  Neck -4, Head -4, Spine2 -2, no yaw.
- f62, wind-up: right arm flex +110, abd +10, bend +70, fist.
- f66, chop (4 frames): right flex +25, bend +35, the fist at least 0.15 m in front of the
  belly; Spine2 +5.
- f68-f80: a hard breath (1.0 s, depth 2.5); Head yaw +7 at f72, -7 at f76, +4 at f80.
- f84: the base, to f90.

    blender --background --factory-startup --python ArtSource/animation/bb_anim_react_furious.py -- [out.fbx]
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tools"))
import bb_anim  # noqa: E402
from bb_anim import F, L, U, both, merge  # noqa: E402

rig = bb_anim.load()
base = rig.base()
m = rig.m
take = bb_anim.Take(rig, "React_furious", 90, exit_at=85)

FIST = (70, 90, 60)
lean = {"Spine1": {"flex": 4}, "Spine2": {"flex": 4}, "Neck": {"flex": -4}}
fists = {"LeftFingers": FIST, "RightFingers": FIST, "LeftThumb": (35, 35, 35), "RightThumb": (35, 35, 35)}

take.key(0, base)
take.leave_base(0, 8, lean)
take.key(8, rig.turn(rig.hang_with(lean), merge(both("Arm", {"flex": 10}), both("ForeArm", {"bend": 30}), fists)))

thrown = rig.turn(rig.hang_with(lean), merge(
    both("Arm", {"flex": 30, "abd": 35, "rot": -45}), both("ForeArm", {"bend": 20}), both("Hand", {"flex": -15}),
    {"LeftFingers": (5, 5, 5), "RightFingers": (5, 5, 5)}))
take.key(16, thrown)
take.key(22, rig.turn(rig.hang_with(lean), merge(both("Arm", {"flex": 10}), both("ForeArm", {"bend": 60}), fists)))


def jab(extended):
    upper = merge(lean, {"Spine2": {"yaw": 6}})
    pose = rig.hang_with(upper)
    if extended:
        right = {"RightArm": {"flex": 75, "abd": 5}, "RightForeArm": {"bend": 15}}
    else:
        right = {"RightArm": {"flex": 45}, "RightForeArm": {"bend": 70}}
    pose = rig.turn(pose, merge(right, {"RightFingers": {"Index": (0, 0, 0), "Middle": FIST, "Ring": FIST, "Pinky": FIST},
                                        "RightThumb": (40, 40, 40), "LeftFingers": FIST, "LeftThumb": (35, 35, 35)}))
    lm = rig.landmarks(pose)
    hip = lm["LeftUpLeg"] + L * m(0.14) + U * m(0.06) + F * m(0.03)
    return rig.reach(pose, "Left", hip, pole=(L - F * 0.6 + U * 0.2).normalized(), palm=-L)


take.key(30, jab(True))
take.key(36, jab(False))
take.key(42, jab(True))
take.key(48, jab(False))
print("SANITY f30 right (flex, abd, bend)", rig.arm_angles(jab(True), "Right"), "left", rig.arm_angles(jab(True), "Left"))

temples_upper = merge({"Spine1": {"flex": 4}}, {"Spine2": {"flex": -2}, "Neck": {"flex": -4}, "Head": {"flex": -4}})
# The thumbs open away from the palms: curled with the fingers, the thumb of a palm laid to the
# temple points into the head (0.066 m from its centre on the first try).
temples = rig.turn(rig.hang_with(temples_upper), {"LeftFingers": (15, 15, 15), "RightFingers": (15, 15, 15),
                                                  "LeftThumb": (-20, -5, 0), "RightThumb": (-20, -5, 0)})
lm = rig.landmarks(temples)
# Elbows forward and out, a little low, so the forearms stand up to the temples rather than lying
# over the head (the first try, elbows out and up, laid the fingers across the crown).
# The brief puts each palm 3 cm out from its temple; at 3 cm a thumb tip comes 0.083 m from the
# head's centre, under rule 7's 0.10, so the palms stand 5 cm out.
OUT = 0.05
temples = rig.reach(temples, "Left", lm["TempleL"] + lm["Lh"] * m(OUT) + lm["Fh"] * m(0.04),
                    pole=(F + L * 0.6 - U * 0.3).normalized(), palm=-lm["Lh"])
temples = rig.reach(temples, "Right", lm["TempleR"] - lm["Lh"] * m(OUT) + lm["Fh"] * m(0.04),
                    pole=(F - L * 0.6 - U * 0.3).normalized(), palm=lm["Lh"])
print("SANITY f54 left", rig.arm_angles(temples, "Left"), "right", rig.arm_angles(temples, "Right"))
take.key(54, temples)

low_fist = merge(both("ForeArm", {"bend": 30}), {"LeftFingers": FIST, "LeftThumb": (35, 35, 35)})
windup = rig.turn(rig.hang_with(temples_upper), merge(low_fist, {"RightArm": {"flex": 110, "abd": 10}, "RightForeArm": {"bend": 70 - 30},
                                                                 "RightFingers": FIST, "RightThumb": (35, 35, 35)}))
take.key(62, windup)
chop_upper = merge(temples_upper, {"Spine2": {"flex": 7}})  # Spine2 +5 from the base: -2 + 7
chop = rig.turn(rig.hang_with(chop_upper), merge(low_fist, {"RightArm": {"flex": 25}, "RightForeArm": {"bend": 35 - 30},
                                                            "RightFingers": FIST, "RightThumb": (35, 35, 35)}))
mats = rig.fk(chop)
lm = rig.landmarks(chop)
ahead = (rig.palm(mats, "Right") - lm["Spine1"]).dot(F) * rig.unit
print("SANITY f66 right", rig.arm_angles(chop, "Right"), "fist ahead of Spine1 by %.3f m" % ahead)
take.key(66, chop)
take.key(77, chop)
take.hold(66, 77)
take.return_to_base(77, 84)

take.breathe(68, 80, 1.0, 2.5)
take.curve("Head", "yaw", [(66, 0.0), (72, 7.0), (76, -7.0), (80, 4.0), (84, 0.0)])
take.micro(6, 84)
take.breathe(84, 90, 4.0, 1.0)

act = take.bake()
bb_anim.finish(rig, act, take, "bb_anim_React_furious.fbx")

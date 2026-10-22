"""React_embrace: arms open, one arm over the partner's shoulder and one under, held with a sway
and two pats, released, home. Both bodies play this same clip, facing, D_STAR apart root to root
(the director scales that by the pair's mean humanScale and eases the two together).

114 frames (3.8 s) at 30 fps; the controller leaves it at 0.97 (f110), and it gives way to the
walk as soon as either body is sent somewhere. From the art brief's R2:
- f9, arms open: both arms flex +60, abd +20, rot +10; bend +30; fingers 10; Spine2 +4, Head +3.
- f21, wrap: the left palm high behind the partner (root + F (d* + 0.10) + L 0.10 + U (Spine2
  height + 0.12)), hand flex +10, fingers 20; the right palm low (root + F (d* + 0.12) - L 0.08
  + U Spine1 height), hand +10, fingers 15; left shoulder raise +8, fwd +10, right fwd +10; Neck
  yaw +15, Head yaw +20, Head side +10; Spine2 +4.
- f21-f84, held: a sway (Spine side +2 at f30, -2 at f48, +2 at f66, 0 at f84), two pats (the
  right hand flex +25 and back at f50-f54 and f62-f66), breathing (4.0 s, depth 1.0).
- f96, release: the f9 arms, the head straight, Spine2 +2. f108: the base.
- The interlock, checked here on two copies of the take D_STAR apart, each stepped SIDE_STEP to
  its own right, the second turned to face the first, at f40: the left palm at least 0.15 m above the right; the heads at least 0.20 m
  apart sideways, on opposite sides; no hand within 0.05 m of a partner's hand; the Spine2
  joints at least 0.15 m apart; no hand point inside the partner's torso capsule.
  The heads need the body to lean into the hug for that: the brief's Head side +10 alone puts
  the two heads 0.04 m apart, so the spine and neck lean to the right as well (LEAN below).

    blender --background --factory-startup --python ArtSource/animation/bb_anim_react_embrace.py -- [out.fbx]
"""
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tools"))
import bb_anim  # noqa: E402
from bb_anim import F, L, U, both, merge  # noqa: E402

# The pair's distance, root to root (EmbracePair.PairDistance at runtime). The brief's 0.46 left
# the back targets at arm's length unless the bodies leaned in far enough to carry their centre of
# mass 5 cm off the root; at 0.40 the at-ease stances' toes are still 0.16 m apart.
D_STAR = 0.40
# The body leans to its right so the two heads pass each other: 3/3/3 and the neck 6 put the
# Mixamo heads 0.24 m apart sideways, and on two UMA bodies only 0.094 (their muscle ranges are
# narrower), with the heads touching in the capture; so more.
LEAN = {"Spine": {"side": 4}, "Spine1": {"side": 4}, "Spine2": {"side": 4}, "Neck": {"side": 10}}
# At d* the brief's Spine2 +4 leaves the chests 0.2 m apart and the arms straight at full reach
# (the low elbow bent 2 degrees): an A-frame hug. The upper bodies lean in to meet.
INTO = {"Spine": {"flex": 3}, "Spine1": {"flex": 3}, "Spine2": {"flex": 3}}
# And the hips counterbalance both leans, as a body does: leaning in and over carried the centre
# of mass 5.4 cm forward of the root in Unity (the EditMode test allows 3 cm either way), and
# 3.4 cm with the hips 1.5 cm back.
COUNTER_BACK, COUNTER_LEFT = 0.025, 0.015
# Each body also stands this far to its own right of the line between the two roots
# (EmbracePair.SideStep at runtime), so the heads pass each other: the lean alone, retargeted onto
# two UMA bodies, put their head joints only 0.092 m apart across the pair's line. Off the take, so
# the take's own centre of mass stays over its root.
SIDE_STEP = 0.035
# The high hand's height above the partner's Spine2 joint (the brief's 0.12).
HIGH_UP = 0.17

rig = bb_anim.load()
base = rig.base()
m = rig.m
take = bb_anim.Take(rig, "React_embrace", 114, exit_at=110)

open_upper = {"Spine2": {"flex": 4}, "Head": {"flex": 3}}
# The brief opens both arms alike (flex 60, abd 20). Facing at d*, one body's left arm and the
# other's right are on the same side at the same height, and they pass through each other on the
# way in and out (the pair check measured the forearms 0.07 m inside each other at f94). So the
# arms open as they will wrap: the left higher and both wider, the high arm passing over the
# partner's low one, which opens low (flex 30) because the side steps bring it nearer.
open_arms = merge({"LeftArm": {"flex": 70, "abd": 60, "rot": 10}, "RightArm": {"flex": 30, "abd": 60, "rot": 10}},
                  both("ForeArm", {"bend": 30}), {"LeftFingers": (10, 10, 10), "RightFingers": (10, 10, 10)})

take.key(0, base)
take.leave_base(0, 9, open_upper)
take.key(9, rig.hang_with(open_upper, open_arms))

wrap_upper = merge({"Spine2": {"flex": 4}, "Neck": {"yaw": 15}, "Head": {"yaw": 20, "side": 10},
                    "LeftShoulder": {"raise": 8, "fwd": 10}, "RightShoulder": {"fwd": 10}}, LEAN, INTO)
wrap = rig.hang_with(wrap_upper, merge(open_arms, {"LeftHand": {"flex": 10}, "RightHand": {"flex": 10},
                                                   "LeftFingers": (20, 20, 20), "RightFingers": (15, 15, 15)}))
wrap = rig.shift_hips(wrap, forward=-COUNTER_BACK, left=COUNTER_LEFT)
lm = rig.landmarks(wrap)


def partner_point(p):
    """A point of this pose as the partner holds it: turned half round U, D_STAR ahead, and two
    side steps to this body's left (each body steps to its own right)."""
    return Vector((-p.x, p.y, -p.z)) + F * m(D_STAR) + L * m(2 * SIDE_STEP)


# The brief's targets, measured from the partner's own spine as it leans in: 10 and 12 cm behind
# its Spine2 and Spine1 joints. Upright, these are exactly root + F (d* + 0.10) + L 0.10 + U
# (Spine2 + 0.12) and root + F (d* + 0.12) - L 0.08 + U Spine1.
high = partner_point(lm["Spine2"]) + F * m(0.10) + L * m(0.10) + U * m(HIGH_UP)
low = partner_point(lm["Spine1"]) + F * m(0.12) - L * m(0.08)
# The palms lie on the partner's back, so they face back towards the one who hugs.
wrap = rig.reach(wrap, "Left", high, pole=(L + U * 0.4 - F * 0.1).normalized(), palm=-F)
wrap = rig.reach(wrap, "Right", low, pole=(-L - U * 0.6).normalized(), palm=-F)
print("SANITY f21 left (flex, abd, bend)", rig.arm_angles(wrap, "Left"), "right", rig.arm_angles(wrap, "Right"))
take.key(21, wrap)
take.key(84, wrap)
take.hold(21, 84)
# The release opens the arms wider and less far forward than they opened on the way in: the side
# steps put one body's right forearm and the other's left on the same side, and opened forward they
# crossed on the way down.
release_arms = merge({"LeftArm": {"flex": 45, "abd": 70, "rot": 10}, "RightArm": {"flex": 15, "abd": 70, "rot": 10}},
                     both("ForeArm", {"bend": 30}), {"LeftFingers": (10, 10, 10), "RightFingers": (10, 10, 10)})
take.key(96, rig.hang_with({"Spine2": {"flex": 2}}, release_arms))
# Home without dropping the open arms to the sides first, as the other takes do: this close, two
# partners' forearms cross on the way down. Straight out to the side, then round to the clasp.
take.via(101, rig.aside())
take.via(104, rig.release())
take.key(108, base)

take.curve("Spine", "side", [(21, 0.0), (30, 2.0), (48, -2.0), (66, 2.0), (84, 0.0)])
pats = [bb_anim.bump(50, 52, 54, 15.0), bb_anim.bump(62, 64, 66, 15.0)]
take.layer(lambda f: {"RightHand": {"flex": sum(p(f) for p in pats)}})
take.breathe(21, 84, 4.0, 1.0)
take.micro(9, 96)
take.breathe(108, 114, 4.0, 1.0)

act = take.bake()


def interlock(frame):
    """The two-body checks at a frame: the partner is this pose turned half round U and set
    D_STAR ahead along F and two side steps along L."""
    pose = rig.sample(act, frame)
    mats = rig.fk(pose)
    ahead = F * m(D_STAR)
    partner = partner_point

    u = rig.unit
    lmA = rig.landmarks(pose)
    hands = {s: rig.palm(mats, s) for s in ("Left", "Right")}
    points = {}
    for s in ("Left", "Right"):
        for label, bone in (("palm", None), ("middle tip", "HandMiddle4"), ("index tip", "HandIndex4"),
                            ("thumb tip", "HandThumb4"), ("wrist", "Hand")):
            points[s + " " + label] = hands[s] if bone is None else rig.head(mats, s + bone)
    out = {}
    out["left_over_right_m"] = (hands["Left"] - hands["Right"]).dot(U) * u
    cA = lmA["C"]
    cB = partner(cA)
    out["heads_side_m"] = abs((cA - cB).dot(L)) * u
    out["heads_opposite"] = (cA.dot(L) - ahead.dot(L) * 0.0) * (cB.dot(L)) < 0.0
    out["heads_apart_m"] = (cA - cB).length * u
    out["hand_hand_m"] = min((hands[a] - partner(hands[b])).length * u for a in hands for b in hands)
    s2 = rig.head(mats, "Spine2")
    out["spine2_apart_m"] = (s2 - partner(s2)).length * u
    torso_a, torso_b = partner(rig.head(mats, "Hips")), partner(rig.head(mats, "Neck"))
    gap, where = 99.0, None
    for label, p in points.items():
        g = bb_anim.seg_dist(p, torso_a, torso_b) * u - 0.10
        if g < gap:
            gap, where = g, label
    out["torso_gap_m"] = gap
    out["torso_gap_at"] = where
    # Arms against the partner's arms (capsules of 0.045 and 0.037 m: 0.075 apart at the least),
    # and hands against the partner's head (a 0.10 m ball about its centre).
    arm_gap, arm_where = 99.0, None
    for a in ("Left", "Right"):
        for b in ("Left", "Right"):
            for sa in (("Arm", "ForeArm"), ("ForeArm", "Hand")):
                for sb in (("Arm", "ForeArm"), ("ForeArm", "Hand")):
                    d = bb_anim.seg_seg_dist(rig.head(mats, a + sa[0]), rig.head(mats, a + sa[1]),
                                             partner(rig.head(mats, b + sb[0])), partner(rig.head(mats, b + sb[1]))) * u - 0.075
                    if d < arm_gap:
                        arm_gap, arm_where = d, "%s %s / partner %s %s" % (a, sa[0], b, sb[0])
    out["arm_gap_m"] = arm_gap
    out["arm_gap_at"] = arm_where
    out["hand_head_m"] = min((p - cB).length * u for p in points.values()) - 0.10
    return out


fails = []
worst_arm = (99.0, None)
worst_torso = (99.0, None)
worst_head = (99.0, None)
for frame in range(0, 115):
    r = interlock(frame)
    if r["arm_gap_m"] < worst_arm[0]:
        worst_arm = (r["arm_gap_m"], "f%d %s" % (frame, r["arm_gap_at"]))
    if r["torso_gap_m"] < worst_torso[0]:
        worst_torso = (r["torso_gap_m"], "f%d %s" % (frame, r["torso_gap_at"]))
    if r["hand_head_m"] < worst_head[0]:
        worst_head = (r["hand_head_m"], "f%d" % frame)
print("PAIR worst arm gap %.3f (%s), worst hand-in-torso gap %.3f (%s), worst hand-to-head gap %.3f (%s)"
      % (worst_arm[0], worst_arm[1], worst_torso[0], worst_torso[1], worst_head[0], worst_head[1]))
# The arm capsules are the skeleton's guess at a sleeve's thickness, and in an embrace the arms
# rest on each other: 5 mm of the two radii pressed is contact. A pass-through measured -0.07.
if worst_arm[0] < -0.005:
    fails.append("arms pass through the partner's arms (%s)" % worst_arm[1])
if worst_torso[0] < 0.0:
    fails.append("a hand inside the partner's torso (%s)" % worst_torso[1])
if worst_head[0] < 0.0:
    fails.append("a hand inside the partner's head (%s)" % worst_head[1])
for frame in (30, 40, 60, 80):
    r = interlock(frame)
    print("INTERLOCK f%d %s" % (frame, " ".join("%s=%s" % (k, ("%.3f" % v) if isinstance(v, float) else v) for k, v in r.items())))
    if frame == 40:
        if r["left_over_right_m"] < 0.15:
            fails.append("left palm only %.3f m above the right" % r["left_over_right_m"])
        if r["heads_side_m"] < 0.20 or not r["heads_opposite"]:
            fails.append("heads %.3f m apart sideways" % r["heads_side_m"])
        if r["hand_hand_m"] < 0.05:
            fails.append("a hand %.3f m from a partner's hand" % r["hand_hand_m"])
        if r["spine2_apart_m"] < 0.15:
            fails.append("Spine2 joints %.3f m apart" % r["spine2_apart_m"])
if fails:
    print("INTERLOCK FAILED", "; ".join(fails))
bb_anim.finish(rig, act, take, "bb_anim_React_embrace.fbx")
if fails:
    raise AssertionError("React_embrace interlock: " + "; ".join(fails))

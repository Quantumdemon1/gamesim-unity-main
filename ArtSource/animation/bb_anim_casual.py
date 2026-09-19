"""The loops and reactions the shipped bodies never had, authored on their own rig.

MASTER-PLAN §4.5 called this the gap that reads loudest. Every take here is a base pose sampled
from a shipped one-shot - the end of SitDown for the seated takes, the start of Idle for the
standing ones - plus small motions keyed to return to it, so a loop closes and a reaction lands
back where the controller expects the body to be.

    SitIdle_loop     3 s   breathing in the chair: the torso and head, a degree or two
    SitTalk_loop     2 s   seated, talking: nods and a hand that lifts
    Talk_loop        2 s   standing, talking: nods, a shoulder, the right forearm
    Listen_loop      3 s   standing, listening: a slower sway, the head slightly turned
    React_nominated  1.2 s the head drops and the shoulders come in, then most of it eases back
    React_saved      1.2 s the arms lift, the head comes up
    React_evicted    1.4 s the torso folds forward, the head with it
    React_won        1.5 s both arms up, the torso back

Exported as one FBX of takes, armature only, to Art/Authored/Animation/Generic, where the
importer takes it as Generic and loops the *_loop takes.

    blender --background --python ArtSource/animation/bb_anim_casual.py -- <output.fbx>
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_anim as A  # noqa: E402

rig = A.load()
seated = rig.sample("SitDown", -1)
standing = rig.sample("Idle", 1)
takes = []


def loop(name, seconds, base, keys):
    """A closed loop: base at both ends, the listed (fraction, offsets) between."""
    frames = int(seconds * A.FPS)
    act = rig.action(name, frames)
    rig.key(act, 1, base)
    for fraction, offsets in keys:
        rig.key(act, 1 + int(fraction * (frames - 1)), A.nudge(base, offsets))
    rig.key(act, frames, base)
    takes.append(act)


def oneshot(name, seconds, base, keys, rest=None):
    """A reaction: base at the start, the listed beats, and a rest pose at the end."""
    frames = int(seconds * A.FPS)
    act = rig.action(name, frames)
    rig.key(act, 1, base)
    for fraction, offsets in keys:
        rig.key(act, 1 + int(fraction * (frames - 1)), A.nudge(base, offsets))
    rig.key(act, frames, A.nudge(base, rest or {}))
    takes.append(act)


loop("SitIdle_loop", 3.0, seated, [
    (0.5, {"Torso": (2.0, 0.0, 0.0), "Head": (-1.5, 0.0, 1.0), "Abdomen": (1.0, 0.0, 0.0)}),
])
loop("SitTalk_loop", 2.0, seated, [
    (0.2, {"Head": (4.0, 0.0, 0.0), "UpperArm.R": (0.0, 0.0, -14.0), "LowerArm.R": (-20.0, 0.0, 0.0)}),
    (0.45, {"Head": (-3.0, 2.0, 0.0), "UpperArm.R": (0.0, 0.0, -8.0), "LowerArm.R": (-30.0, 0.0, 0.0), "Torso": (1.5, 0.0, 0.0)}),
    (0.7, {"Head": (3.0, -2.0, 0.0), "UpperArm.R": (0.0, 0.0, -12.0), "LowerArm.R": (-16.0, 0.0, 0.0)}),
])
loop("Talk_loop", 2.0, standing, [
    (0.2, {"Head": (4.0, 0.0, 0.0), "Shoulder.R": (0.0, 0.0, -3.0), "UpperArm.R": (0.0, 0.0, -16.0), "LowerArm.R": (-25.0, 0.0, 0.0)}),
    (0.45, {"Head": (-3.0, 2.5, 0.0), "UpperArm.R": (0.0, 0.0, -10.0), "LowerArm.R": (-35.0, 0.0, 0.0), "Torso": (1.5, 0.0, 0.0)}),
    (0.7, {"Head": (3.0, -2.5, 0.0), "Shoulder.R": (0.0, 0.0, -2.0), "UpperArm.R": (0.0, 0.0, -14.0), "LowerArm.R": (-20.0, 0.0, 0.0)}),
])
loop("Listen_loop", 3.0, standing, [
    (0.33, {"Torso": (1.2, 0.0, 1.0), "Head": (2.0, 6.0, 0.0)}),
    (0.66, {"Torso": (1.2, 0.0, -1.0), "Head": (-1.0, 4.0, 0.0), "Abdomen": (0.8, 0.0, 0.0)}),
])
oneshot("React_nominated", 1.2, standing, [
    (0.35, {"Head": (16.0, 0.0, 0.0), "Torso": (6.0, 0.0, 0.0), "Shoulder.L": (0.0, 0.0, 6.0), "Shoulder.R": (0.0, 0.0, -6.0)}),
    (0.7, {"Head": (12.0, -6.0, 0.0), "Torso": (5.0, 0.0, 0.0), "Shoulder.L": (0.0, 0.0, 5.0), "Shoulder.R": (0.0, 0.0, -5.0)}),
], rest={"Head": (6.0, 0.0, 0.0), "Torso": (2.0, 0.0, 0.0)})
oneshot("React_saved", 1.2, standing, [
    (0.3, {"UpperArm.L": (0.0, 0.0, 70.0), "UpperArm.R": (0.0, 0.0, -70.0), "LowerArm.L": (-40.0, 0.0, 0.0), "LowerArm.R": (-40.0, 0.0, 0.0), "Head": (-10.0, 0.0, 0.0), "Torso": (-4.0, 0.0, 0.0)}),
    (0.6, {"UpperArm.L": (0.0, 0.0, 55.0), "UpperArm.R": (0.0, 0.0, -55.0), "LowerArm.L": (-30.0, 0.0, 0.0), "LowerArm.R": (-30.0, 0.0, 0.0), "Head": (-8.0, 0.0, 0.0), "Torso": (-3.0, 0.0, 0.0)}),
])
oneshot("React_evicted", 1.4, standing, [
    (0.4, {"Torso": (14.0, 0.0, 0.0), "Abdomen": (6.0, 0.0, 0.0), "Head": (12.0, 0.0, 0.0), "Shoulder.L": (0.0, 0.0, 8.0), "Shoulder.R": (0.0, 0.0, -8.0)}),
    (0.75, {"Torso": (12.0, 0.0, 0.0), "Abdomen": (5.0, 0.0, 0.0), "Head": (14.0, 5.0, 0.0), "Shoulder.L": (0.0, 0.0, 7.0), "Shoulder.R": (0.0, 0.0, -7.0)}),
], rest={"Torso": (6.0, 0.0, 0.0), "Head": (8.0, 0.0, 0.0)})
oneshot("React_won", 1.5, standing, [
    (0.25, {"UpperArm.L": (0.0, 0.0, 95.0), "UpperArm.R": (0.0, 0.0, -95.0), "LowerArm.L": (-20.0, 0.0, 0.0), "LowerArm.R": (-20.0, 0.0, 0.0), "Head": (-14.0, 0.0, 0.0), "Torso": (-8.0, 0.0, 0.0)}),
    (0.55, {"UpperArm.L": (0.0, 0.0, 85.0), "UpperArm.R": (0.0, 0.0, -85.0), "LowerArm.L": (-30.0, 0.0, 0.0), "LowerArm.R": (-30.0, 0.0, 0.0), "Head": (-12.0, 4.0, 0.0), "Torso": (-6.0, 0.0, 0.0)}),
    (0.8, {"UpperArm.L": (0.0, 0.0, 60.0), "UpperArm.R": (0.0, 0.0, -60.0), "Head": (-6.0, 0.0, 0.0), "Torso": (-3.0, 0.0, 0.0)}),
])

path = A.output_path("bb_anim_casual.fbx")
A.export(rig, takes, path)

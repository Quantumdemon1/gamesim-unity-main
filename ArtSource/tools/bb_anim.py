"""Authoring Humanoid takes for the UMA cast in Blender, headless, on the Mixamo skeleton.

Every Humanoid take that ships (Assets/Gamesim/Art/Authored/Animation/Humanoid) is a skinless
Mixamo rig: 65 bones under one armature, 30 fps, one take a file, named by the file
(bb_anim_<Take>.fbx carries <Take>). Unity imports each as a Humanoid clip and retargets it onto
whatever body UMA builds. This module loads that rig with a take, lets a script pose it in
anatomical terms - a bend of the spine, a raise of the arm, a hand brought to a point in front of
the face - keys the result frame by frame, checks it against the rules every story take follows,
and exports the armature alone, which Unity imports as one more take on the same skeleton.

    import bb_anim
    rig = bb_anim.load()                          # PoseAtEase, the still every take starts from
    base = rig.base()                             # its first frame, faced forward
    take = bb_anim.Take(rig, "React_shocked", 78, exit_at=74)
    take.key(0, base); take.key(5, rig.turn(rig.hang(base), {...}))
    act = take.bake()
    rig.check(act, take)                          # raises if a rule fails
    bb_anim.export(rig, act, bb_anim.output_path("bb_anim_React_shocked.fbx"))

Conventions (ArtSource/animation/bb_anim_react_*.py and the art brief's R2):

- The armature's own space is the body frame once base() has faced it forward: F (forward) is +Z,
  U (up) is +Y and L (the body's left) is +X, in the file's units (centimetres; Rig.m converts).
- Spine, Spine1, Spine2, Neck, Head: flex + bends forward, side + leans to the body's right,
  yaw + turns the face to the body's right. Shoulder: raise + shrugs, fwd + protracts. All are
  deltas from base(), about the body axes through the joint, parent before child.
- Arm: flex + raises forward from hanging (90 is horizontal), abd + raises out to the side (-
  crosses the body), rot + rotates internally. ForeArm: bend + bends the elbow, twist + pronates.
  Hand: flex + toward the palm. These are deltas from hanging - Rig.hang(), the base with its arms
  at its sides, palms in - because PoseAtEase holds its hands behind its back, and the brief's
  arm angles are written from hanging. Abd, rot and twist mirror on the right, so equal left and
  right values are symmetric.
- Fingers: curl per segment, absolute from straight (a fist is 70/90/60), for Index, Middle, Ring
  and Pinky 1/2/3; Thumb1-3 separately.
- reach(): where a pose uses it, the target point is authoritative and the angles are a sanity
  range only. A landmark plus F x (0.55h + d) means d in front of that landmark's face surface.

Keying is by posing and keyframe_insert, never by editing fcurves: Blender 5 actions are layered.
Every frame is keyed, so nothing depends on Blender's interpolation.

Gate 0 (2026-10-10): ArtSource/tools/bb_anim_roundtrip.py exported bb_anim_TalkB_loop.fbx untouched
and re-imported it; a scratch Unity probe on the UMA acceptance copy imported the result beside the
shipped take. Configuration C (apply_scale_options FBX_SCALE_NONE) passes:
- Blender: 65 bones both ways, action 0..118 at 30 fps, every pose bone's world matrix at frames
  0, 13, ... 117 within 0.056 degrees and 0.0016 mm; the armature's world matrix unchanged.
- Unity: one clip (Humanoid, looping), length 3.933334 s like TalkB's, lastFrame 118; the same 52
  human-bone pairs; 66 skeleton nodes with Hips directly under the file root at y 1.041959
  (TalkB: 1.0419588); every muscle curve at ten times within 0.000007; RootT.y 0.946615 against
  the shipped 0.946616; RootT.x/z 0; RootQ 0 degrees; IK goals within 0.000033.
- Gate 0b, base() held 4 s breathing (period 4 s, depth 1): every muscle at t 0 within 0.000002
  of bb_anim_PoseAtEase_loop.anim, Head and Neck Turn 0, RootT.y 0, Chest Front-Back ranging
  0.0149 over the clip. Its raw RootQ reads 180 degrees where the authored loop reads 0 - the
  Mixamo heading the stills carry (PoseAtEase.fbx reads 178.03 raw) - and played through a
  PlayableGraph, where Body Orientation applies, the two face within 0.000004 degrees.
- Configurations A (FBX_SCALE_ALL) and B (FBX_SCALE_UNITS) match every curve too but write the
  skeleton in centimetres (Hips at 104.1959), so they fail.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
HUMANOID = os.path.join(REPO, "Assets", "Gamesim", "Art", "Authored", "Animation", "Humanoid")
SOURCE = os.path.join(HUMANOID, "bb_anim_PoseAtEase.fbx")
FPS = 30
PREFIX = "mixamorig:"

# apply_scale_options by Gate 0's configuration letter.
SCALE_OPTIONS = {"A": 'FBX_SCALE_ALL', "B": 'FBX_SCALE_UNITS', "C": 'FBX_SCALE_NONE'}
CONFIG = "C"  # Gate 0: the only configuration whose skeleton imports in metres

F = Vector((0.0, 0.0, 1.0))
U = Vector((0.0, 1.0, 0.0))
L = Vector((1.0, 0.0, 0.0))

MIDLINE = ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head")
FINGERS = ("Index", "Middle", "Ring", "Pinky")
LEGS = ("UpLeg", "Leg", "Foot", "ToeBase", "Toe_End")
SIDES = ("Left", "Right")


def full(name):
    return name if name.startswith(PREFIX) else PREFIX + name


def short(name):
    return name[len(PREFIX):] if name.startswith(PREFIX) else name


def rot(axis, degrees):
    return Quaternion(Vector(axis).normalized(), math.radians(degrees))


def angle_between(q1, q2):
    """The angle in degrees between two orientations."""
    d = abs(q1.normalized().dot(q2.normalized()))
    return math.degrees(2.0 * math.acos(min(1.0, d)))


def yaw_of(v):
    """The heading of a direction about U, in degrees, + towards the body's left (+X)."""
    return math.degrees(math.atan2(v.x, v.z))


def ease(t):
    """Ease in and out, 0..1 to 0..1."""
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def copy_pose(pose):
    return {n: (loc.copy(), q.copy()) for n, (loc, q) in pose.items()}


def import_fbx(path):
    """Imports one FBX at 30 fps with its animation from frame 0; returns (armature, action)."""
    before = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(path), use_anim=True, anim_offset=0.0,
                             ignore_leaf_bones=False, automatic_bone_orientation=False,
                             primary_bone_axis='Y', secondary_bone_axis='X')
    new = [o for o in bpy.data.objects if o not in before]
    armature = next(o for o in new if o.type == 'ARMATURE')
    for mesh in [o for o in new if o.type == 'MESH']:
        bpy.data.objects.remove(mesh, do_unlink=True)
    acts = [a for a in bpy.data.actions if a not in before_actions]
    action = armature.animation_data.action if armature.animation_data else (acts[0] if acts else None)
    return armature, action


def fresh_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    return scene


def load(source=SOURCE):
    """A fresh 30 fps scene holding the Mixamo armature with the source's take, keyed by its stem."""
    fresh_scene()
    armature, action = import_fbx(source)
    stem = os.path.splitext(os.path.basename(source))[0]
    return Rig(armature, {stem: action})


class Rig:
    def __init__(self, armature, takes):
        self.armature = armature
        self.takes = dict(takes)
        if armature.animation_data is None:
            armature.animation_data_create()
        self.unit = armature.matrix_world.to_scale()[0]  # metres per armature unit
        bones = armature.data.bones
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}
        self.rest = {b.name: b.matrix_local.copy() for b in bones}
        self.rel = {}
        for b in bones:
            p = self.parent[b.name]
            self.rel[b.name] = (self.rest[p].inverted() @ self.rest[b.name]) if p else self.rest[b.name].copy()
        self.order = []
        seen = set()
        def visit(n):
            if n in seen:
                return
            p = self.parent[n]
            if p:
                visit(p)
            seen.add(n)
            self.order.append(n)
        for b in bones:
            visit(b.name)
        self.index = {n: i for i, n in enumerate(self.order)}
        self._axes = None
        self._base = None
        self._hang = None

    # ----- units and takes ------------------------------------------------------------------
    def m(self, metres):
        """Metres in armature units."""
        return metres / self.unit

    def add_take(self, path):
        """Imports another file's take onto this rig, keyed by the file's stem; its armature goes."""
        armature, action = import_fbx(path)
        bpy.data.objects.remove(armature, do_unlink=True)
        stem = os.path.splitext(os.path.basename(path))[0]
        self.takes[stem] = action
        return action

    def assign(self, action):
        ad = self.armature.animation_data
        ad.action = action
        if hasattr(ad, "action_slot") and ad.action_slot is None and len(action.slots) > 0:
            ad.action_slot = action.slots[0]

    def sample(self, take, frame):
        """A take's pose at a frame (negative counts from the end) as {bone: (loc, quat)}."""
        act = self.takes[take] if isinstance(take, str) else take
        start, end = act.frame_range
        if frame < 0:
            frame = int(end) + 1 + frame
        self.assign(act)
        bpy.context.scene.frame_set(int(frame))
        bpy.context.view_layer.update()
        pose = {}
        for pb in self.armature.pose.bones:
            loc, q, _ = pb.matrix_basis.decompose()
            pose[pb.name] = (loc.copy(), q.copy())
        return pose

    # ----- kinematics -----------------------------------------------------------------------
    def fk(self, pose):
        """Every bone's armature-space matrix for a pose, parents before children."""
        out = {}
        for n in self.order:
            loc, q = pose[n]
            basis = Matrix.Translation(loc) @ q.to_matrix().to_4x4()
            p = self.parent[n]
            out[n] = ((out[p] @ self.rel[n]) if p else self.rel[n]) @ basis
        return out

    def head(self, mats, name):
        return mats[full(name)].translation.copy()

    def palm(self, mats, side):
        return (self.head(mats, side + "Hand") + self.head(mats, side + "HandMiddle1")) * 0.5

    def rotate_world(self, pose, name, q_world, mats=None):
        """Turns a bone by an armature-space rotation about its own head; children follow."""
        name = full(name)
        mats = mats if mats is not None else self.fk(pose)
        p = self.parent[name]
        a = ((mats[p] @ self.rel[name]) if p else self.rel[name]).to_quaternion()
        loc, q = pose[name]
        pose[name] = (loc, (a.inverted() @ q_world @ a @ q).normalized())
        return pose

    def rotate_local(self, pose, name, axis_local, degrees):
        name = full(name)
        loc, q = pose[name]
        pose[name] = (loc, (q @ rot(axis_local, degrees)).normalized())
        return pose

    def world_dir(self, mats, name, local):
        return (mats[full(name)].to_3x3() @ Vector(local)).normalized()

    def forward_of(self, mats, name):
        """The direction that was F at rest, carried by the bone's pose."""
        name = full(name)
        return (mats[name].to_3x3() @ self.rest[name].to_3x3().inverted() @ F).normalized()

    # ----- the body's own axes (measured once on the base) ------------------------------------
    def axes(self):
        """Per-joint hinge axes in each bone's local frame, with signs, measured on hang()."""
        if self._axes is not None:
            return self._axes
        hang = self.hang()
        mats = self.fk(hang)
        ax = {}
        for side in SIDES:
            # The elbow: the rest T-pose (palms down) bends the elbow about U, so the hinge is U
            # carried by the upper arm; flexion carries the hand forward from hanging.
            arm = full(side + "Arm")
            hinge_local = self.rest[arm].to_3x3().inverted() @ U
            ax[side + "Elbow"] = (hinge_local, self._sign_world(hang, side + "Arm", side + "ForeArm", hinge_local,
                                                               lambda m: self.head(m, side + "Hand").dot(F)))
            # The wrist and the fingers flex about the line across the knuckles, towards the palm.
            across = (self.head(mats, side + "HandIndex1") - self.head(mats, side + "HandPinky1")).normalized()
            along = (self.head(mats, side + "HandMiddle1") - self.head(mats, side + "Hand")).normalized()
            palm = along.cross(across) if side == "Left" else across.cross(along)
            ax[side + "PalmNormal"] = palm
            for bone, tip in [(side + "Hand", side + "HandMiddle1")] + \
                    [(side + "Hand" + f + str(i), side + "Hand" + f + str(i + 1)) for f in FINGERS for i in (1, 2, 3)]:
                local = mats[full(bone)].to_3x3().inverted() @ across
                ax[bone] = (local, self._sign_local(hang, bone, local, lambda m, t=tip: self.head(m, t).dot(palm)))
            # The thumb: whichever local axis carries its tip towards the middle of the palm.
            target = self.palm(mats, side) + palm * self.m(0.03)
            for i in (1, 2, 3):
                bone = side + "HandThumb" + str(i)
                best = None
                for local in (Vector((1, 0, 0)), Vector((0, 0, 1))):
                    for sign in (1.0, -1.0):
                        trial = self.rotate_local(copy_pose(hang), bone, local, 15.0 * sign)
                        d = (self.head(self.fk(trial), side + "HandThumb4") - target).length
                        if best is None or d < best[0]:
                            best = (d, local, sign)
                ax[bone] = (best[1], best[2])
        self._axes = ax
        return ax

    def _sign_world(self, pose, parent_bone, bone, hinge_local, score):
        mats = self.fk(pose)
        axis = (mats[full(parent_bone)].to_3x3() @ hinge_local).normalized()
        trial = self.rotate_world(copy_pose(pose), bone, rot(axis, 10.0))
        return 1.0 if score(self.fk(trial)) > score(mats) else -1.0

    def _sign_local(self, pose, bone, local, score):
        trial = self.rotate_local(copy_pose(pose), bone, local, 10.0)
        return 1.0 if score(self.fk(trial)) > score(self.fk(pose)) else -1.0

    # ----- the base and hanging poses -------------------------------------------------------
    def base(self):
        """PoseAtEase's first frame, faced forward: the hips' heading and horizontal place zeroed,
        and the neck and head turned to face the way Spine2 does (keepTurn 0)."""
        if self._base is not None:
            return copy_pose(self._base)
        take = next(k for k in self.takes if k.endswith("PoseAtEase"))
        pose = self.sample(take, 0)
        hips = full("Hips")
        mats = self.fk(pose)
        body = self.body_heading(mats)
        self.rotate_world(pose, hips, rot(U, -body), mats)
        mats = self.fk(pose)
        h = mats[hips].translation
        loc, q = pose[hips]
        a = self.rel[hips].to_3x3()
        pose[hips] = (loc - a.inverted() @ Vector((h.x, 0.0, h.z)), q)
        for name, parent in (("Neck", "Spine2"), ("Head", "Neck")):
            mats = self.fk(pose)
            turn = yaw_of(self.forward_of(mats, name)) - yaw_of(self.forward_of(mats, parent))
            self.rotate_world(pose, name, rot(U, -turn), mats)
        self._base = copy_pose(pose)
        return copy_pose(pose)

    def body_heading(self, mats):
        """The body's heading about U in degrees: the mean of the hip line's and the shoulder
        line's normals, as Unity's body orientation reads it."""
        hip = self.head(mats, "LeftUpLeg") - self.head(mats, "RightUpLeg")
        sho = self.head(mats, "LeftArm") - self.head(mats, "RightArm")
        line = hip.normalized() + sho.normalized()
        fwd = Vector((-line.z, 0.0, line.x))  # L rotated a quarter turn about U is F
        return yaw_of(fwd)

    def hang(self, pose=None):
        """A pose with its arms hanging at its sides, palms in, elbows a little bent, fingers
        relaxed: the reference the arm angles are written from."""
        if pose is None and self._hang is not None:
            return copy_pose(self._hang)
        if pose is not None and self._hang is None:
            self.hang()  # the default first: it measures the joint axes every hang curls with
        out = copy_pose(pose if pose is not None else self.base())
        for side in SIDES:
            s = 1.0 if side == "Left" else -1.0
            arm, fore, hand = full(side + "Arm"), full(side + "ForeArm"), full(side + "Hand")
            # Straight from rest (the T-pose, palms down), then down to the side, 7 degrees out.
            for n in (fore, hand):
                out[n] = (out[n][0], Quaternion())
            mats = self.fk(out)
            p = self.parent[arm]
            a = (mats[p] @ self.rel[arm]).to_quaternion()
            target = rot(F, -s * 83.0) @ self.rest[arm].to_quaternion()
            out[arm] = (out[arm][0], (a.inverted() @ target).normalized())
            # The elbow a little bent, forward.
            mats = self.fk(out)
            hinge = (mats[arm].to_3x3() @ (self.rest[arm].to_3x3().inverted() @ U)).normalized()
            for sign in (1.0, -1.0):
                trial = self.rotate_world(copy_pose(out), fore, rot(hinge, 12.0 * sign))
                if self.head(self.fk(trial), side + "Hand").dot(F) > self.head(mats, side + "Hand").dot(F):
                    out = trial
                    break
            for f in FINGERS:
                for i in (1, 2, 3):
                    n = full(side + "Hand" + f + str(i))
                    out[n] = (out[n][0], Quaternion())
            for i in (1, 2, 3):
                n = full(side + "HandThumb" + str(i))
                out[n] = (out[n][0], Quaternion())
        if pose is None:
            self._hang = copy_pose(out)  # straight-fingered, while the axes are measured on it
        ax = self.axes()
        for side in SIDES:
            for f in FINGERS:
                for i, c in zip((1, 2, 3), (12.0, 16.0, 10.0)):
                    self.curl(out, side + "Hand" + f + str(i), c, ax)
            for i, c in zip((1, 2, 3), (8.0, 10.0, 8.0)):
                self.curl(out, side + "HandThumb" + str(i), c, ax)
        if pose is None:
            self._hang = copy_pose(out)
        return copy_pose(out)

    def release(self, upper=None):
        """The base's hands let go behind the back: the elbows straighten and the arms open out a
        little, keeping the clasp's roll, so the hands drop behind and beside the hips. The first
        way-point out of the base and the last one back into it. upper, a turn() spec for the
        spine, neck, head or shoulders, is applied first, so a bowed body stays bowed."""
        out = self.turn(self.base(), upper) if upper else self.base()
        out = self.turn(out, both("Arm", {"abd": 18.0, "flex": -8.0}))
        return self.set_bend(self.set_bend(out, "Left", 10.0), "Right", 10.0)

    def aside(self, upper=None):
        """The arms held out from the sides, a little behind, in the hanging roll: the second
        way-point, where the roll changes with the hands well clear of the thighs. Measured on
        the scan in the art lane: base -> release -> aside -> anything in front keeps every hand
        point 0.088 m or more outside the torso, pelvis and thigh capsules."""
        out = self.hang(self.turn(self.base(), upper)) if upper else self.hang()
        return self.turn(out, both("Arm", {"abd": 20.0, "flex": -10.0}))

    def hang_with(self, upper=None, arms=None):
        """hang() under an upper-body spec, with optional arm deltas on top."""
        out = self.hang(self.turn(self.base(), upper)) if upper else self.hang()
        return self.turn(out, arms) if arms else out

    def shift_hips(self, pose, forward=0.0, left=0.0):
        """The hips (and the body with them) moved along the floor by metres: a counterbalance, so a
        bow or a reach does not carry the body's centre of mass away from its root."""
        out = copy_pose(pose)
        hips = full("Hips")
        loc, q = out[hips]
        delta = F * self.m(forward) + L * self.m(left)
        out[hips] = (loc + self.rel[hips].to_3x3().inverted() @ delta, q)
        return out

    def curl(self, pose, bone, degrees, ax=None):
        """Sets a finger segment's curl, absolute from straight."""
        ax = ax or self.axes()
        local, sign = ax[short(full(bone))]
        n = full(bone)
        pose[n] = (pose[n][0], rot(local, degrees * sign))
        return pose

    # ----- anatomical deltas ----------------------------------------------------------------
    def turn(self, pose, spec):
        """Applies anatomical deltas, parent before child. spec maps a bone to its parameters;
        "LeftFingers"/"RightFingers" take (c1, c2, c3) or {finger: (c1, c2, c3)}; "LeftThumb"/
        "RightThumb" take (c1, c2, c3)."""
        out = copy_pose(pose)
        ax = self.axes()
        items = []
        for key, value in spec.items():
            if key.endswith("Fingers") or key.endswith("Thumb"):
                items.append((10 ** 6, key, value))
            else:
                items.append((self.index[full(key)], key, value))
        for _, key, value in sorted(items, key=lambda t: (t[0], t[1])):
            if key.endswith("Fingers"):
                side = key[:-len("Fingers")]
                per = value if isinstance(value, dict) else {f: value for f in FINGERS}
                for f, segs in per.items():
                    for i, c in zip((1, 2, 3), segs):
                        self.curl(out, side + "Hand" + f + str(i), c, ax)
                continue
            if key.endswith("Thumb"):
                side = key[:-len("Thumb")]
                for i, c in zip((1, 2, 3), value):
                    self.curl(out, side + "HandThumb" + str(i), c, ax)
                continue
            self._turn_one(out, key, value, ax)
        return out

    def _turn_one(self, pose, key, p, ax):
        name = short(full(key))
        side = "Left" if name.startswith("Left") else ("Right" if name.startswith("Right") else "")
        s = 1.0 if side != "Right" else -1.0
        part = name[len(side):]
        mats = self.fk(pose)
        if part in MIDLINE:
            q = rot(L, p.get("flex", 0.0)) @ rot(F, p.get("side", 0.0)) @ rot(U, -p.get("yaw", 0.0))
            self.rotate_world(pose, name, q, mats)
        elif part == "Shoulder":
            q = rot(F, s * p.get("raise", 0.0)) @ rot(U, -s * p.get("fwd", 0.0))
            self.rotate_world(pose, name, q, mats)
        elif part == "Arm":
            along = (self.head(mats, side + "ForeArm") - self.head(mats, name)).normalized()
            q = rot(L, -p.get("flex", 0.0)) @ rot(F, s * p.get("abd", 0.0)) @ rot(along, s * p.get("rot", 0.0))
            self.rotate_world(pose, name, q, mats)
        elif part == "ForeArm":
            if p.get("bend", 0.0):
                local, sign = ax[side + "Elbow"]
                hinge = (mats[full(side + "Arm")].to_3x3() @ local).normalized()
                self.rotate_world(pose, name, rot(hinge, sign * p["bend"]), mats)
            if p.get("twist", 0.0):
                mats = self.fk(pose)
                along = (self.head(mats, side + "Hand") - self.head(mats, name)).normalized()
                self.rotate_world(pose, name, rot(along, s * p["twist"]), mats)
        elif part == "Hand":
            local, sign = ax[name]
            self.rotate_local(pose, name, local, sign * p.get("flex", 0.0))
        else:
            raise ValueError("no anatomical parameters for " + key)

    def set_bend(self, pose, side, degrees):
        """Sets the elbow to a bend measured from the hanging pose's straight line."""
        return self.turn(pose, {side + "ForeArm": {"bend": degrees - self.bend_of(pose, side)}})

    def bend_of(self, pose, side):
        mats = self.fk(pose)
        upper = (self.head(mats, side + "ForeArm") - self.head(mats, side + "Arm")).normalized()
        lower = (self.head(mats, side + "Hand") - self.head(mats, side + "ForeArm")).normalized()
        return math.degrees(upper.angle(lower))

    # ----- reaching -------------------------------------------------------------------------
    def palm_normal(self, mats, side):
        across = (self.head(mats, side + "HandIndex1") - self.head(mats, side + "HandPinky1")).normalized()
        along = (self.head(mats, side + "HandMiddle1") - self.head(mats, side + "Hand")).normalized()
        n = along.cross(across) if side == "Left" else across.cross(along)
        return n.normalized()

    def reach(self, pose, side, target, pole=None, twist=None, palm=None, max_twist=100.0):
        """An analytic two-bone fit: the palm centre (the midpoint of Hand and HandMiddle1) is
        brought to target (armature space) by the elbow's bend and a swing of the upper arm, with
        the elbow turned towards pole (a direction; default down and out). twist, if given,
        pronates the forearm first; palm, if given, turns the forearm about its own length (by at
        most max_twist degrees) so the palm faces that way as nearly as it can, and fits again."""
        out = self._reach(pose, side, target, pole, twist)
        if palm is None:
            return out
        applied = 0.0
        for _ in range(3):
            mats = self.fk(out)
            elbow, wrist = self.head(mats, side + "ForeArm"), self.head(mats, side + "Hand")
            axis = (wrist - elbow).normalized()
            n = self.palm_normal(mats, side)
            n_p = n - axis * n.dot(axis)
            d_p = palm - axis * palm.dot(axis)
            if n_p.length < 1e-6 or d_p.length < 1e-6:
                break
            ang = math.degrees(n_p.normalized().angle(d_p.normalized()))
            if n_p.cross(d_p).dot(axis) < 0.0:
                ang = -ang
            ang = max(-max_twist - applied, min(max_twist - applied, ang))
            applied += ang
            self.rotate_world(out, side + "ForeArm", rot(axis, ang), mats)
            out = self._reach(out, side, target, pole, None)
        self.last_twist = applied
        return out

    def _reach(self, pose, side, target, pole=None, twist=None):
        out = copy_pose(pose)
        s = 1.0 if side == "Left" else -1.0
        if pole is None:
            pole = (-U + L * (0.5 * s) - F * 0.2).normalized()
        if twist:
            out = self.turn(out, {side + "ForeArm": {"twist": twist}})
        mats = self.fk(out)
        shoulder = self.head(mats, side + "Arm")
        want = (target - shoulder).length
        local, sign = self.axes()[side + "Elbow"]

        def dist_at(bend):
            trial = self._bend_by(copy_pose(out), side, bend, local, sign)
            return (self.palm(self.fk(trial), side) - shoulder).length, trial

        # Search the bend that puts the palm at the wanted distance from the shoulder, from no
        # bend past straight to a full fold (the distance falls as the elbow closes).
        current = self.bend_of(out, side)
        best = None
        lo_b = -current + 2.0
        hi_b = 150.0 - current
        steps = 60
        prev = None
        for k in range(steps + 1):
            b = lo_b + (hi_b - lo_b) * k / steps
            d, _ = dist_at(b)
            if prev is not None and (prev[1] - want) * (d - want) <= 0.0:
                a0, a1 = prev[0], b
                for _ in range(30):
                    mid = (a0 + a1) * 0.5
                    dm, _ = dist_at(mid)
                    if (dist_at(a0)[0] - want) * (dm - want) <= 0.0:
                        a1 = mid
                    else:
                        a0 = mid
                best = (a0 + a1) * 0.5
                break
            prev = (b, d)
        if best is None:
            # Out of reach either way: the nearest end.
            best = lo_b if abs(dist_at(lo_b)[0] - want) < abs(dist_at(hi_b)[0] - want) else hi_b
        _, out = dist_at(best)
        # Swing the upper arm so the palm lies on the line to the target...
        mats = self.fk(out)
        cur = (self.palm(mats, side) - shoulder).normalized()
        goal = (target - shoulder).normalized()
        self.rotate_world(out, side + "Arm", cur.rotation_difference(goal), mats)
        # ...then turn the whole arm about that line until the elbow points to the pole.
        mats = self.fk(out)
        elbow = self.head(mats, side + "ForeArm") - shoulder
        e_perp = elbow - goal * elbow.dot(goal)
        p_perp = pole - goal * pole.dot(goal)
        if e_perp.length > 1e-6 and p_perp.length > 1e-6:
            angle = e_perp.normalized().angle(p_perp.normalized())
            if e_perp.normalized().cross(p_perp.normalized()).dot(goal) < 0.0:
                angle = -angle
            self.rotate_world(out, side + "Arm", Quaternion(goal, angle), mats)
        return out

    def _bend_by(self, pose, side, degrees, local, sign):
        mats = self.fk(pose)
        hinge = (mats[full(side + "Arm")].to_3x3() @ local).normalized()
        return self.rotate_world(pose, side + "ForeArm", rot(hinge, sign * degrees), mats)

    # ----- landmarks ------------------------------------------------------------------------
    def landmarks(self, pose):
        """Points on the body, armature space: H the Head joint, h the head's height, E the eyes,
        M the mouth, the temples, S the sternum, C the head's centre, plus Spine1/Spine2 heights.
        Each face landmark is the surface point; X + F x (0.55h + d) in the brief is X_at(d)."""
        mats = self.fk(pose)
        H = self.head(mats, "Head")
        h = (self.head(mats, "HeadTop_End") - H).length
        sp2 = self.head(mats, "Spine2")
        # The face's points ride the head: its own up and forward, as the head is turned from the
        # base, so a bowed head's eyes are where the bow put them.
        base = self._base if self._base is not None else self.base()
        turn = mats[full("Head")].to_quaternion() @ self.fk(base)[full("Head")].to_quaternion().inverted()
        Uh, Fh, Lh = turn @ U, turn @ F, turn @ L
        lm = {
            "H": H, "h": h, "Uh": Uh, "Fh": Fh, "Lh": Lh,
            "E": H + Uh * (0.45 * h) + Fh * (0.55 * h),
            "M": H + Uh * (0.10 * h) + Fh * (0.55 * h),
            "TempleL": H + Uh * (0.55 * h) + Lh * (0.5 * h),
            "TempleR": H + Uh * (0.55 * h) - Lh * (0.5 * h),
            "S": sp2 + F * self.m(0.14) + U * self.m(0.08),
            "C": H + Uh * (0.5 * h),
            "Spine": self.head(mats, "Spine"),
            "Spine1": self.head(mats, "Spine1"),
            "Spine2": sp2,
            "LeftUpLeg": self.head(mats, "LeftUpLeg"),
            "RightUpLeg": self.head(mats, "RightUpLeg"),
        }
        return lm

    def face(self, lm, key, ahead=0.0, up=0.0, left=0.0):
        """A face landmark moved in the head's own frame, in metres: face(lm, "M", 0.075) is the
        brief's M + F x (0.55h + 0.075), a point 7.5 cm in front of the mouth."""
        return lm[key] + lm["Fh"] * self.m(ahead) + lm["Uh"] * self.m(up) + lm["Lh"] * self.m(left)

    def arm_angles(self, pose, side):
        """The upper arm's flex and abduction from hanging and the elbow's bend, in degrees, for
        reading a reach against the brief's sanity ranges."""
        mats = self.fk(pose)
        s = 1.0 if side == "Left" else -1.0
        d = (self.head(mats, side + "ForeArm") - self.head(mats, side + "Arm")).normalized()
        flex = math.degrees(math.atan2(d.dot(F), -d.dot(U)))
        abd = math.degrees(math.atan2(s * d.dot(L), -d.dot(U)))
        return round(flex, 1), round(abd, 1), round(self.bend_of(pose, side), 1)

    # Capsules a hand must stay out of: (from, to, radius in metres). A slim build, so a hand that
    # clears these clears the bodies UMA builds only when they are slim too; the margin is not big.
    BODY = (("Hips", "Spine2", 0.10), ("LeftUpLeg", "RightUpLeg", 0.075),
            ("LeftUpLeg", "LeftLeg", 0.06), ("RightUpLeg", "RightLeg", 0.06))

    def body_clearance(self, mats):
        """The smallest gap, in metres, between a hand point and the body's capsules."""
        best, where = 99.0, None
        for side in SIDES:
            points = {"palm": self.palm(mats, side), "wrist": self.head(mats, side + "Hand"),
                      "middle tip": self.head(mats, side + "HandMiddle4"), "index tip": self.head(mats, side + "HandIndex4")}
            for label, point in points.items():
                for a, b, r in self.BODY:
                    gap = seg_dist(point, self.head(mats, a), self.head(mats, b)) * self.unit - r
                    if gap < best:
                        best, where = gap, "%s %s against %s-%s" % (side, label, a, b)
        return best, where

    # ----- the rules ------------------------------------------------------------------------
    def check(self, act, take=None, frames=None, exit_at=None, holds=()):
        """Asserts the story-take rules (the brief's R2 list) over every frame of an action and
        returns the measurements. Raises AssertionError naming every rule that fails."""
        start, end = (int(v) for v in act.frame_range)
        if take is not None:
            exit_at = take.exit_at
            holds = take.holds
        base = self.base()
        bm = self.fk(base)
        unit = self.unit
        fails = []
        poses = [self.sample(act, f) for f in range(start, end + 1)]
        mats = [self.fk(p) for p in poses]
        hips = full("Hips")

        def bone_err(p):
            return max(angle_between(p[n][1], base[n][1]) for n in self.order)

        # 1. The ends are the base.
        for f in (start, end):
            p = poses[f - start]
            e = bone_err(p)
            hd = (mats[f - start][hips].translation - bm[hips].translation).length * unit
            if e > 0.5 or hd > 0.001:
                fails.append("rule 1: frame %d is %.2f deg / %.4f m from the base" % (f, e, hd))
        # 2. The base by the exit, held breathing after it.
        breathers = {full(n) for n in ("Spine1", "Spine2", "LeftShoulder", "RightShoulder", "Head", "Neck")}
        worst_after = 0.0
        for f in range(exit_at, end + 1):
            p = poses[f - start]
            for n in self.order:
                tol = 3.5 if n in breathers else 0.5
                e = angle_between(p[n][1], base[n][1])
                worst_after = max(worst_after, e if n not in breathers else 0.0)
                if e > tol:
                    fails.append("rule 2: %s at frame %d is %.2f deg from the base after the exit" % (short(n), f, e))
                    break
        # 3. The hips stay put.
        h0 = bm[hips].translation
        travel = max(math.hypot(m[hips].translation.x - h0.x, m[hips].translation.z - h0.z) for m in mats) * unit
        low = min(m[hips].translation.y for m in mats) / h0.y
        hy = max(abs(yaw_of(self.forward_of(m, "Hips")) - yaw_of(self.forward_of(bm, "Hips"))) for m in mats)
        if travel > 0.03:
            fails.append("rule 3: hips travel %.3f m" % travel)
        if low < 0.95:
            fails.append("rule 3: hips fall to %.3f of the base" % low)
        if hy > 2.0:
            fails.append("rule 3: hips yaw %.2f deg" % hy)
        # 4. The legs keep the base.
        leg_err = 0.0
        for p in poses:
            for side in SIDES:
                for part in LEGS:
                    n = full(side + part)
                    leg_err = max(leg_err, angle_between(p[n][1], base[n][1]))
        if leg_err > 0.5:
            fails.append("rule 4: a leg bone moves %.2f deg" % leg_err)
        # 5. Facing.
        def line_yaw(m, a, b):
            v = self.head(m, a) - self.head(m, b)
            return yaw_of(Vector((-v.z, 0.0, v.x)))
        sho0 = line_yaw(bm, "LeftArm", "RightArm")
        hip0 = line_yaw(bm, "LeftUpLeg", "RightUpLeg")
        chest0 = yaw_of(self.forward_of(bm, "Spine2")) - yaw_of(self.forward_of(bm, "Spine"))
        chest = sho_all = hip_all = sho_early = 0.0
        for i, m in enumerate(mats):
            f = start + i
            c = abs(yaw_of(self.forward_of(m, "Spine2")) - yaw_of(self.forward_of(m, "Spine")) - chest0)
            sh = abs(line_yaw(m, "LeftArm", "RightArm") - sho0)
            hp = abs(line_yaw(m, "LeftUpLeg", "RightUpLeg") - hip0)
            chest = max(chest, c)
            sho_all = max(sho_all, sh, hp)
            if 6 <= f <= 21:
                sho_early = max(sho_early, sh, hp)
        if chest > 12.05:
            fails.append("rule 5: chest yaw %.1f deg" % chest)
        if sho_early > 15.0:
            fails.append("rule 5: shoulder/hip line %.1f deg over f6-f21" % sho_early)
        if sho_all > 20.0:
            fails.append("rule 5: shoulder/hip line %.1f deg" % sho_all)
        # 6. Alive: nothing still for more than 15 frames; every hold breathes.
        run = longest = 0
        for i in range(1, len(poses)):
            moved = max(angle_between(poses[i][n][1], poses[i - 1][n][1]) for n in self.order)
            run = run + 1 if moved < 0.02 else 0
            longest = max(longest, run)
        if longest > 15:
            fails.append("rule 6: the pose holds unchanged for %d frames" % longest)
        sp2 = full("Spine2")
        for (a, b) in holds:
            vals = [angle_between(poses[f - start][sp2][1], base[sp2][1]) for f in range(a, b + 1)]
            if max(vals) - min(vals) < 0.4:
                fails.append("rule 6: the hold %d-%d does not breathe (Spine2 varies %.2f deg)" % (a, b, max(vals) - min(vals)))
        # 7. Hands clear of the head and the spine.
        near_head = near_spine = 99.0
        near_what = None
        for i, p in enumerate(poses):
            m = mats[i]
            lm = self.landmarks(p)
            axis_a, axis_b = self.head(m, "Hips"), self.head(m, "Neck")
            for side in SIDES:
                for label, point in (("palm", self.palm(m, side)), ("middle tip", self.head(m, side + "HandMiddle4")),
                                     ("index tip", self.head(m, side + "HandIndex4")), ("thumb tip", self.head(m, side + "HandThumb4"))):
                    d = (point - lm["C"]).length * unit
                    if d < near_head:
                        near_head, near_what = d, "%s %s at frame %d" % (side, label, start + i)
                near_spine = min(near_spine, seg_dist(self.palm(m, side), axis_a, axis_b) * unit)
        if near_head < 0.10:
            fails.append("rule 7: a hand comes %.3f m from the head centre (%s)" % (near_head, near_what))
        if near_spine < 0.08:
            fails.append("rule 7: a palm comes %.3f m from the spine" % near_spine)
        # And never through the body: hand points against the torso, pelvis and thigh capsules.
        clearance, where = 99.0, None
        for i, p in enumerate(poses):
            c, w = self.body_clearance(mats[i])
            if c < clearance:
                clearance, where = c, (start + i, w)
        if clearance < 0.0:
            fails.append("rule 7: a hand is %.3f m inside the body at frame %d (%s)" % (-clearance, where[0], where[1]))
        # 8. 30 fps from frame 0, one action, armature only.
        if start != 0 or bpy.context.scene.render.fps != FPS:
            fails.append("rule 8: frames start at %d at %d fps" % (start, bpy.context.scene.render.fps))
        if act.name.endswith("_loop"):
            fails.append("rule 8: a story take is not a loop")
        report = {
            "frames": (start, end), "exit": exit_at, "hips_travel_m": travel, "hips_low": low, "hips_yaw": hy,
            "legs_deg": leg_err, "chest_yaw": chest, "lines_early": sho_early, "lines_all": sho_all,
            "longest_still": longest, "near_head_m": near_head, "near_spine_m": near_spine,
            "after_exit_deg": worst_after, "body_clearance_m": clearance,
        }
        print("CHECK %s %s" % (act.name, " ".join("%s=%s" % (k, ("%.3f" % v) if isinstance(v, float) else v)
                                                  for k, v in report.items())))
        if fails:
            raise AssertionError("%s fails: %s" % (act.name, "; ".join(fails)))
        return report


def both(part, value):
    """The same parameters for the left and the right: both("Arm", {"flex": 25})."""
    return {"Left" + part: value, "Right" + part: value}


def merge(*specs):
    """One turn() spec from several; later parameters add to earlier ones."""
    out = {}
    for spec in specs:
        for bone, params in spec.items():
            if isinstance(params, dict) and not (bone.endswith("Fingers") or bone.endswith("Thumb")):
                tgt = out.setdefault(bone, {})
                for k, v in params.items():
                    tgt[k] = tgt.get(k, 0.0) + v
            else:
                out[bone] = params
    return out


def bump(start, peak, end, size):
    """A smooth rise from start to size at peak and back to nothing at end, as frame -> value."""
    def value(f):
        if f <= start or f >= end:
            return 0.0
        if f <= peak:
            return size * ease((f - start) / float(peak - start))
        return size * (1.0 - ease((f - peak) / float(end - peak)))
    return value


def seg_seg_dist(p1, q1, p2, q2, samples=24):
    """The closest approach of two segments (sampled along the first, exact onto the second)."""
    best = 1e9
    for i in range(samples + 1):
        p = p1.lerp(q1, i / float(samples))
        best = min(best, seg_dist(p, p2, q2))
    return best


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.dot(ab), 1e-9)))
    return (p - (a + ab * t)).length


class Take:
    """A take as key poses plus layers. Between keys each bone slerps with an ease; layers add
    anatomical deltas on top at every frame (breathing, the head's small motions, scalar curves).
    bake() keys every frame of a new action."""

    def __init__(self, rig, name, frames, exit_at):
        self.rig = rig
        self.name = name
        self.frames = frames
        self.exit_at = exit_at
        self.keys = []
        self.layers = []
        self.holds = []

    def key(self, frame, pose, via=False):
        """A key pose. A via key is passed through: the ease runs over the whole stretch between
        the ordinary keys either side, so the body does not stop at it."""
        self.keys = [k for k in self.keys if k[0] != frame] + [(frame, copy_pose(pose), via)]
        self.keys.sort(key=lambda k: k[0])
        return pose

    def via(self, frame, pose):
        return self.key(frame, pose, via=True)

    def leave_base(self, start, end, upper=None):
        """The way out of the base's clasp between two frames: release, then aside. upper is
        the upper-body spec the way-points carry (a bow that has already begun)."""
        span = end - start
        self.via(start + max(1, round(span * 0.4)), self.rig.release(upper))
        self.via(start + max(2, round(span * 0.6)), self.rig.aside(upper))

    def return_to_base(self, start, end, upper=None):
        """The way back into the clasp: the arms down to the sides first (so hands in front come
        down beside the body, not down its middle), then aside and release; keys the base at end
        (the base's arms under upper, when an upper-body spec is still held)."""
        span = end - start
        rig = self.rig
        self.via(start + max(1, round(span * 0.3)), rig.hang_with(upper, both("Arm", {"abd": 10.0})))
        self.via(start + max(2, round(span * 0.55)), rig.aside(upper))
        self.via(start + max(3, round(span * 0.78)), rig.release(upper))
        self.key(end, rig.turn(rig.base(), upper) if upper else rig.base())

    def hold(self, start, end):
        """Marks a hold (for rule 6's breathing check); the poses come from key()."""
        self.holds.append((start, end))

    def layer(self, fn):
        """fn(frame) -> a turn() spec added on top of the keyed pose at that frame."""
        self.layers.append(fn)

    def curve(self, bone, param, points):
        """A scalar delta curve: points are (frame, degrees), eased between, held at the ends."""
        pts = sorted(points)

        def value(f):
            if f <= pts[0][0]:
                return pts[0][1]
            for (a, va), (b, vb) in zip(pts, pts[1:]):
                if a <= f <= b:
                    return va + (vb - va) * ease((f - a) / float(b - a))
            return pts[-1][1]
        self.layer(lambda f: {bone: {param: value(f)}})

    def breathe(self, start, end, period, depth):
        breathe(self, start, end, period, depth)

    def micro(self, start, end, phase=0.0):
        """The head's small motions, eased in and out over 6 frames."""
        def fn(f):
            if f < start or f > end:
                return {}
            env = min(1.0, (f - start) / 6.0, (end - f) / 6.0)
            env = ease(env)
            t = f / float(FPS)
            yaw = 1.2 * math.sin(2 * math.pi * 0.37 * t + phase) + 0.8 * math.sin(2 * math.pi * 0.61 * t)
            flex = 0.6 * math.sin(2 * math.pi * 0.43 * t)
            return {"Head": {"yaw": yaw * env, "flex": flex * env}}
        self.layer(fn)

    def pose_at(self, f):
        keys = self.keys
        if f <= keys[0][0]:
            pose = copy_pose(keys[0][1])
        elif f >= keys[-1][0]:
            pose = copy_pose(keys[-1][1])
        else:
            stops = [k for k in keys if not k[2]] or [keys[0], keys[-1]]
            for (a, _, _), (b, _, _) in zip(stops, stops[1:]):
                if a <= f <= b:
                    break
            # One ease from stop a to stop b; the via keys between are points along it.
            u = ease((f - a) / float(b - a))
            chain = [k for k in keys if a <= k[0] <= b]
            fu = a + u * (b - a)
            for (ka, pa, _), (kb, pb, _) in zip(chain, chain[1:]):
                if ka <= fu <= kb:
                    t = (fu - ka) / float(kb - ka) if kb > ka else 1.0
                    if len(chain) == 2:
                        t = u
                    pose = {}
                    for n in pa:
                        la, qa = pa[n]
                        lb, qb = pb[n]
                        if qa.dot(qb) < 0.0:
                            qb = -qb
                        pose[n] = (la.lerp(lb, t), qa.slerp(qb, t))
                    break
        spec = {}
        for fn in self.layers:
            for bone, params in fn(f).items():
                if isinstance(params, dict):
                    tgt = spec.setdefault(bone, {})
                    for k, v in params.items():
                        tgt[k] = tgt.get(k, 0.0) + v
                else:
                    spec[bone] = params
        return self.rig.turn(pose, spec) if spec else pose

    def bake(self):
        rig = self.rig
        act = bpy.data.actions.new(self.name)
        act.use_fake_user = True
        rig.assign(act)
        scene = bpy.context.scene
        for f in range(0, self.frames + 1):
            pose = self.pose_at(f)
            scene.frame_set(f)
            for pb in rig.armature.pose.bones:
                loc, q = pose[pb.name]
                pb.rotation_mode = 'QUATERNION'
                pb.location = loc
                pb.rotation_quaternion = q
                pb.scale = (1.0, 1.0, 1.0)
                pb.keyframe_insert("location", frame=f, group=pb.name)
                pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
                pb.keyframe_insert("scale", frame=f, group=pb.name)
        act.frame_range = (0, self.frames)
        rig.takes[self.name] = act
        return act


def breathe(take, start, end, period, depth):
    """Breathing over [start, end]: Spine2 flex +1.2ds, Spine1 +0.6ds, both shoulders raise
    +0.8ds, Head flex -0.4ds, with s = (1 - cos 2pi(t - t0)/P) / 2 (P in seconds)."""
    def fn(f):
        if f < start or f > end:
            return {}
        s = (1.0 - math.cos(2.0 * math.pi * (f - start) / (period * FPS))) * 0.5
        # Let go of the breath over the window's last six frames, so nothing pops when it ends.
        d = depth * s * ease(min(1.0, (end - f) / 6.0))
        return {"Spine2": {"flex": 1.2 * d}, "Spine1": {"flex": 0.6 * d},
                "LeftShoulder": {"raise": 0.8 * d}, "RightShoulder": {"raise": 0.8 * d},
                "Head": {"flex": -0.4 * d}}
    take.layer(fn)


def export(rig, act, path, config=None):
    """Writes the armature with one action as the file's one take; every other action goes."""
    config = config or CONFIG
    for other in list(bpy.data.actions):
        if other != act:
            bpy.data.actions.remove(other)
    rig.assign(act)
    start, end = (int(v) for v in act.frame_range)
    scene = bpy.context.scene
    scene.frame_start = start
    scene.frame_end = end
    scene.frame_set(start)
    for obj in bpy.data.objects:
        obj.select_set(False)
    rig.armature.select_set(True)
    bpy.context.view_layer.objects.active = rig.armature
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.abspath(path),
        use_selection=True,
        object_types={'ARMATURE'},
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options=SCALE_OPTIONS[config],
        bake_space_transform=False,
        path_mode='AUTO',
        embed_textures=False,
    )
    print("EXPORTED %s: take %s, frames %d-%d, %d bones, config %s"
          % (path, act.name, start, end, len(rig.armature.data.bones), config))
    return path


def finish(rig, act, take, default_name):
    """Checks the take and exports it. With --draft after the output path a failing take is
    exported anyway (for looking at), and the failure is raised after the export."""
    failure = None
    try:
        rig.check(act, take)
    except AssertionError as e:
        failure = e
        print("CHECK FAILED", e)
    args = extra_args()
    if "--check-only" not in args and (failure is None or "--draft" in args):
        export(rig, act, output_path(default_name))
    if failure is not None:
        raise failure


def _script_args():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def output_path(default_name):
    """The first argument after '--' unless it is a flag, or Animation/Humanoid/<default_name>."""
    args = _script_args()
    if args and not args[0].startswith("--"):
        return args[0]
    return os.path.join(HUMANOID, default_name)


def extra_args():
    """The flags, after the output path or in its place: --check-only skips the export, --draft
    exports a failing take anyway."""
    args = _script_args()
    return args[1:] if args and not args[0].startswith("--") else args

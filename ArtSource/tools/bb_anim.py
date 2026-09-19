"""Authoring animation for the Quaternius rig in Blender, headless.

The six shipped bodies share one Generic skeleton, CharacterArmature (23 bones, 25 fps), with
a handful of one-shot takes and no loops. This imports that rig with its takes, lets a script
sample any take's pose at any frame and build new actions on top of it - keyframed bone by bone,
so a loop is a base pose plus small motions that return to it - and exports the armature with
its new actions as one FBX of takes, which Unity imports as Generic clips whose curve paths match
the prefabs' bone paths exactly. No mesh travels; the clips play on the bodies already shipped.

    import bb_anim
    rig = bb_anim.load()                       # the armature, takes attached
    base = rig.sample("SitDown", -1)           # the last frame of a take, as a pose
    act = rig.action("SitIdle_loop", 75)       # a new take, 75 frames
    rig.key(act, 1, base); rig.key(act, 75, base)
    rig.key(act, 38, bb_anim.nudge(base, {"Torso": (2.0, 0.0, 0.0)}))
    bb_anim.export(rig, [act], path)
"""
import math
import os

import bpy
from mathutils import Euler, Quaternion, Vector

SOURCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Gamesim", "Art",
                      "External", "QuaterniusCharacters", "Casual_Male.fbx")
FPS = 25


class Rig:
    def __init__(self, armature):
        self.armature = armature
        self.takes = {}
        for act in bpy.data.actions:
            # The importer names a take "CharacterArmature|CharacterArmature|SitDown".
            self.takes[act.name.split("|")[-1]] = act
        if armature.animation_data is None:
            armature.animation_data_create()

    @property
    def bones(self):
        return [b.name for b in self.armature.pose.bones]

    def sample(self, take, frame):
        """The pose of a take at a frame (negative counts from the end), as {bone: (loc, quat)}."""
        act = self.takes[take]
        start, end = act.frame_range
        if frame < 0:
            frame = int(end) + 1 + frame
        self.armature.animation_data.action = act
        bpy.context.scene.frame_set(int(frame))
        bpy.context.view_layer.update()
        pose = {}
        for pb in self.armature.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pose[pb.name] = (pb.location.copy(), pb.rotation_quaternion.copy())
        return pose

    def action(self, name, frames):
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        act.frame_range = (1, frames)
        act["bb_frames"] = frames
        return act

    def key(self, act, frame, pose):
        """Keys every bone of a pose at a frame of an action."""
        self.armature.animation_data.action = act
        bpy.context.scene.frame_set(frame)
        for name, (loc, quat) in pose.items():
            pb = self.armature.pose.bones.get(name)
            if pb is None:
                continue
            pb.rotation_mode = 'QUATERNION'
            pb.location = loc
            pb.rotation_quaternion = quat
            pb.keyframe_insert("location", frame=frame, group=name)
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=name)


def nudge(pose, offsets):
    """A copy of a pose with bones rotated by (x, y, z) degrees in their local space."""
    out = {name: (loc.copy(), quat.copy()) for name, (loc, quat) in pose.items()}
    for name, degrees in offsets.items():
        if name not in out:
            continue
        loc, quat = out[name]
        delta = Euler(tuple(math.radians(d) for d in degrees), 'XYZ').to_quaternion()
        out[name] = (loc, quat @ delta)
    return out


def load(source=SOURCE):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(source), use_anim=True, ignore_leaf_bones=True,
                             automatic_bone_orientation=False)
    armature = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for mesh in [o for o in bpy.data.objects if o.type == 'MESH']:
        bpy.data.objects.remove(mesh, do_unlink=True)
    return Rig(armature)


def export(rig, actions, path):
    """Writes the armature and the given actions as takes; every other take is dropped."""
    keep = {a.name for a in actions}
    for act in list(bpy.data.actions):
        if act.name not in keep:
            bpy.data.actions.remove(act)
    rig.armature.animation_data.action = actions[0]
    # Unity makes the FBX's single top-level node the model root and names it after the file,
    # which would drop "CharacterArmature/" from every curve path - and the prefabs' bones live
    # at "CharacterArmature/Bone/...". An empty above the armature keeps the armature a child,
    # and the paths line up with the bodies.
    root = bpy.data.objects.new(os.path.splitext(os.path.basename(path))[0], None)
    bpy.context.scene.collection.objects.link(root)
    rig.armature.parent = root
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    rig.armature.select_set(True)
    bpy.context.view_layer.objects.active = rig.armature
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.abspath(path),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        object_types={'ARMATURE', 'EMPTY'},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode='COPY',
        embed_textures=False,
    )
    print("EXPORTED %s: %d takes (%s), %d bones" % (path, len(actions), ", ".join(sorted(keep)), len(rig.bones)))
    return path


def output_path(default_name):
    import sys
    if "--" in sys.argv and sys.argv.index("--") + 1 < len(sys.argv):
        return sys.argv[sys.argv.index("--") + 1]
    return os.path.join(os.getcwd(), default_name)

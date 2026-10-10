"""Render a Humanoid take to a contact sheet, so a take can be looked at instead of trusted.

The takes are skinless: a Mixamo armature and nothing else. This skins a capsule body onto it -
limbs, a pelvis, a chest, a head with a nose so the facing reads, hands with fingers - tinted
left blue and right red, and renders the chosen frames from the front and the side with Workbench,
one row per file and view, one column per frame, into one PNG.

    blender --background --factory-startup --python ArtSource/tools/bb_anim_look.py -- \
        <out.png> <take.fbx> [<take2.fbx> ...] [--frames 0,59,118] [--views front,side] \
        [--pair 0.46] [--size 300]

--pair d stands a second copy of each take d metres in front of the first, turned to face it, so
a two-body take (the embrace) is seen as it plays.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_anim  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
out = argv[0]
files, frames, views, pair, size, zoom = [], [0], ["front", "side"], None, 300, None
i = 1
while i < len(argv):
    a = argv[i]
    if a == "--frames":
        frames = [int(v) for v in argv[i + 1].split(",")]
        i += 2
    elif a == "--views":
        views = argv[i + 1].split(",")
        i += 2
    elif a == "--pair":
        pair = float(argv[i + 1])
        i += 2
    elif a == "--size":
        size = int(argv[i + 1])
        i += 2
    elif a == "--zoom":
        # ortho scale in metres and the height the camera centres on, e.g. 0.8,1.45 for the head
        zoom = [float(v) for v in argv[i + 1].split(",")]
        i += 2
    else:
        files.append(a)
        i += 1

COLORS = {
    "torso": (0.62, 0.62, 0.60, 1.0), "head": (0.86, 0.72, 0.60, 1.0), "nose": (0.25, 0.10, 0.08, 1.0),
    "left": (0.30, 0.48, 0.85, 1.0), "right": (0.85, 0.32, 0.28, 1.0), "floor": (0.35, 0.36, 0.38, 1.0),
    "left2": (0.30, 0.70, 0.55, 1.0), "right2": (0.85, 0.65, 0.20, 1.0), "torso2": (0.50, 0.46, 0.56, 1.0),
}


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = COLORS[name]
    return mat


def capsule(bm, deform, group, a, b, r, mat_index):
    """A cylinder from a to b with spheres at both ends, every vertex weighted to group."""
    d = b - a
    length = d.length
    new = []
    if length > 1e-6:
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        m = Matrix.Translation((a + b) * 0.5) @ q.to_matrix().to_4x4()
        res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=12, radius1=r, radius2=r,
                                    depth=length, matrix=m)
        new += res["verts"]
    for p in (a, b):
        res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=r, matrix=Matrix.Translation(p))
        new += res["verts"]
    for v in new:
        v[deform][group] = 1.0
    faces = {f for v in new for f in v.link_faces}
    for f in faces:
        f.material_index = mat_index


def skin(arm, second=False):
    """A capsule body skinned to the armature in its rest pose (armature space, its units)."""
    rig = bb_anim.Rig(arm, {})
    unit = rig.unit
    bones = arm.data.bones
    mesh = bpy.data.meshes.new(arm.name + "_body")
    obj = bpy.data.objects.new(arm.name + "_body", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = arm
    obj.matrix_parent_inverse = Matrix.Identity(4)
    names = ["torso2" if second else "torso", "head", "nose", "left2" if second else "left", "right2" if second else "right"]
    for n in names:
        obj.data.materials.append(material(n))
    groups = {}
    for b in bones:
        groups[b.name] = obj.vertex_groups.new(name=b.name).index
    bm = bmesh.new()
    deform = bm.verts.layers.deform.verify()

    def H(n):
        return bones[bb_anim.full(n)].head_local.copy()

    def g(n):
        return groups[bb_anim.full(n)]

    cm = 1.0 / unit
    TORSO, HEAD, NOSE, LEFT, RIGHT = 0, 1, 2, 3, 4
    capsule(bm, deform, g("Hips"), H("LeftUpLeg"), H("RightUpLeg"), 0.075 * cm, TORSO)
    capsule(bm, deform, g("Spine"), H("Spine"), H("Spine1"), 0.105 * cm, TORSO)
    capsule(bm, deform, g("Spine1"), H("Spine1"), H("Spine2"), 0.115 * cm, TORSO)
    capsule(bm, deform, g("Spine2"), H("Spine2"), H("Neck") - Vector((0, 0.05 * cm, 0)), 0.12 * cm, TORSO)
    capsule(bm, deform, g("Neck"), H("Neck"), H("Head"), 0.045 * cm, TORSO)
    head = H("Head")
    h = (H("HeadTop_End") - head).length
    centre = head + Vector((0, 0.5 * h, 0.05 * h))
    capsule(bm, deform, g("Head"), centre - Vector((0, 0.12 * h, 0)), centre + Vector((0, 0.12 * h, 0)), 0.42 * h, HEAD)
    nose = head + Vector((0, 0.38 * h, 0.5 * h))
    capsule(bm, deform, g("Head"), nose, nose + Vector((0, 0, 0.12 * h)), 0.07 * h, NOSE)
    for side, mat in (("Left", LEFT), ("Right", RIGHT)):
        capsule(bm, deform, g(side + "Shoulder"), H(side + "Shoulder"), H(side + "Arm"), 0.04 * cm, TORSO)
        capsule(bm, deform, g(side + "Arm"), H(side + "Arm"), H(side + "ForeArm"), 0.045 * cm, mat)
        capsule(bm, deform, g(side + "ForeArm"), H(side + "ForeArm"), H(side + "Hand"), 0.037 * cm, mat)
        capsule(bm, deform, g(side + "Hand"), H(side + "Hand"), H(side + "HandMiddle1"), 0.028 * cm, mat)
        for f in ("Index", "Middle", "Ring", "Pinky", "Thumb"):
            r = (0.011 if f == "Thumb" else 0.0085) * cm
            for k in (1, 2, 3):
                capsule(bm, deform, g(side + "Hand" + f + str(k)), H(side + "Hand" + f + str(k)),
                        H(side + "Hand" + f + str(k + 1)), r, mat)
        capsule(bm, deform, g(side + "UpLeg"), H(side + "UpLeg"), H(side + "Leg"), 0.07 * cm, mat)
        capsule(bm, deform, g(side + "Leg"), H(side + "Leg"), H(side + "Foot"), 0.05 * cm, mat)
        capsule(bm, deform, g(side + "Foot"), H(side + "Foot"), H(side + "ToeBase"), 0.04 * cm, mat)
        capsule(bm, deform, g(side + "ToeBase"), H(side + "ToeBase"), H(side + "Toe_End"), 0.03 * cm, mat)
    bm.to_mesh(mesh)
    bm.free()
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    mod.use_vertex_groups = True
    return obj


scene = bb_anim.fresh_scene()
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = False
scene.render.resolution_x = size
scene.render.resolution_y = int(size * 1.3)
scene.render.image_settings.file_format = 'PNG'
scene.world = bpy.data.worlds.new("look")
scene.world.color = (0.93, 0.93, 0.93)

floor_mesh = bpy.data.meshes.new("floor")
floor_mesh.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
floor = bpy.data.objects.new("floor", floor_mesh)
floor.data.materials.append(material("floor"))
scene.collection.objects.link(floor)

cam_data = bpy.data.cameras.new("look")
cam_data.type = 'ORTHO'
cam = bpy.data.objects.new("look", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

label_curve = bpy.data.curves.new("label", 'FONT')
label = bpy.data.objects.new("label", label_curve)
label.data.materials.append(material("nose"))
scene.collection.objects.link(label)
label_curve.size = 0.09

tmp = os.path.join(os.path.dirname(os.path.abspath(out)), "_look_cell.png")
cells = []  # rows of images
row_names = []
for path in files:
    for obj in [o for o in bpy.data.objects if o.name not in ("floor", "look", "label")]:
        bpy.data.objects.remove(obj, do_unlink=True)
    arm, act = bb_anim.import_fbx(path)
    skin(arm)
    bodies = [arm]
    if pair:
        arm2, act2 = bb_anim.import_fbx(path)
        holder = bpy.data.objects.new("pair_holder", None)
        scene.collection.objects.link(holder)
        holder.location = (0.0, -pair, 0.0)
        holder.rotation_euler = (0.0, 0.0, math.pi)
        arm2.parent = holder
        skin(arm2, second=True)
        bodies.append(arm2)
    start, end = (int(v) for v in act.frame_range)
    for view in views:
        row = []
        for f in frames:
            f = min(max(f, start), end)
            scene.frame_set(f)
            bpy.context.view_layer.update()
            pts = []
            for a in bodies:
                for pb in a.pose.bones:
                    pts.append(a.matrix_world @ pb.head)
            xs = [p.x for p in pts]
            ys = [p.y for p in pts]
            cx = (min(xs) + max(xs)) * 0.5
            cy = (min(ys) + max(ys)) * 0.5
            cz = 1.0 if zoom is None else zoom[1]
            cam_data.ortho_scale = (2.5 if not pair else 2.7) if zoom is None else zoom[0]
            sc = cam_data.ortho_scale
            top = cz + sc * 0.5 - sc * 0.06
            label_curve.size = 0.036 * sc
            if view == "front":
                cam.location = (cx, cy - 10.0, cz)
                cam.rotation_euler = (math.radians(90), 0.0, 0.0)
                label.location = (cx - sc / 2.7, cy - 2.0, top)
                label.rotation_euler = (math.radians(90), 0.0, 0.0)
            elif view == "side":
                cam.location = (cx + 10.0, cy, cz)
                cam.rotation_euler = (math.radians(90), 0.0, math.radians(90))
                label.location = (cx + 2.0, cy - sc / 2.7, top)
                label.rotation_euler = (math.radians(90), 0.0, math.radians(90))
            elif view == "top":
                cam.location = (cx, cy, 10.0)
                cam.rotation_euler = (0.0, 0.0, 0.0)
                label.location = (cx - 1.0, cy + 1.0, 3.0)
                label.rotation_euler = (0.0, 0.0, 0.0)
            elif view == "three":
                cam.location = (cx + 7.0, cy - 7.0, cz + 3.0)
                cam.rotation_euler = (math.radians(73), 0.0, math.radians(45))
                label.location = (cx - 0.9, cy - 0.9, 2.3)
                label.rotation_euler = (math.radians(90), 0.0, math.radians(45))
            label_curve.body = "%s %s f%d" % (os.path.splitext(os.path.basename(path))[0].replace("bb_anim_", ""), view, f)
            scene.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp, check_existing=False)
            w, hgt = img.size
            px = np.array(img.pixels[:], dtype=np.float32).reshape(hgt, w, 4)
            bpy.data.images.remove(img)
            row.append(px)
        cells.append(row)
        row_names.append("%s %s" % (os.path.basename(path), view))

rows = [np.concatenate(r, axis=1) for r in cells]
width = max(r.shape[1] for r in rows)
rows = [np.pad(r, ((0, 0), (0, width - r.shape[1]), (0, 0)), constant_values=1.0) for r in rows]
# Rows were read bottom-up (Blender's pixel order); stack so the first row is on top.
sheet = np.concatenate(rows[::-1], axis=0)
H, W = sheet.shape[0], sheet.shape[1]
image = bpy.data.images.new("sheet", width=W, height=H, alpha=True)
image.pixels.foreach_set(sheet.ravel())
image.filepath_raw = os.path.abspath(out)
image.file_format = 'PNG'
image.save()
if os.path.exists(tmp):
    os.remove(tmp)
print("LOOKED %s: %d rows (%s), frames %s" % (out, len(rows), "; ".join(row_names), frames))

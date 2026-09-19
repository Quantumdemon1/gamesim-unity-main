"""Export conventions for Gamesim's Blender-authored assets — MASTER-PLAN Part 4, §4.3, as code.

Every asset script imports this and exports through it, so the conventions are checked on every
export rather than remembered: names carry the bb_ prefix, transforms are applied, root origins sit
on the floor, there are no n-gons, and the FBX leaves with the one set of settings Unity's importer
expects (metres, -Z forward / Y up with the transform baked, triangulated, materials by name).

Run inside Blender:  blender --background --python <asset script>.py -- <output.fbx>
"""
import os
import re

import bpy

NAME = re.compile(r"^bb_[a-z]+_[A-Za-z0-9]+(_[A-Za-z0-9]+)*?(_lod[0-9])?(_col)?$")
FLOOR_TOLERANCE = 0.001


class ExportError(Exception):
    pass


def fresh_scene():
    """An empty metric scene, so an export never carries a default cube or a stale unit scale."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = 'METERS'
    return scene


def collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def link_only(obj, coll):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    coll.objects.link(obj)


def material(name, base_color, metallic=0.0, roughness=0.6, emission=None, emission_strength=0.0, alpha=1.0):
    """One Principled BSDF per material, named bb_mat_<name>, so Unity's importer maps it by name."""
    full = name if name.startswith("bb_mat_") else "bb_mat_" + name
    mat = bpy.data.materials.get(full) or bpy.data.materials.new(full)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base_color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if alpha < 1.0:
        mat.surface_render_method = 'BLENDED'
    return mat


def apply_transforms(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.select_all(action='DESELECT')


def check_object(obj):
    problems = []
    if not NAME.match(obj.name):
        problems.append(obj.name + ": name must be bb_<category>_<name>[_<variant>][_lodN][_col]")
    if obj.type != 'MESH':
        return problems
    if any(abs(s - 1.0) > 1e-6 for s in obj.scale):
        problems.append(obj.name + ": scale is not applied")
    if any(abs(r) > 1e-6 for r in obj.rotation_euler):
        problems.append(obj.name + ": rotation is not applied")
    mesh = obj.data
    if mesh.name != obj.name:
        problems.append(obj.name + ": mesh data is named " + mesh.name + "; mesh, object and file share the name")
    if any(len(poly.vertices) > 4 for poly in mesh.polygons):
        problems.append(obj.name + ": n-gons present; quads or triangles only")
    if obj.parent is None:
        if abs(obj.location.z) > FLOOR_TOLERANCE or abs(obj.location.x) > FLOOR_TOLERANCE or abs(obj.location.y) > FLOOR_TOLERANCE:
            problems.append(obj.name + ": root origin must be at the floor contact point (0, 0, 0)")
        lowest = min(((obj.matrix_world @ v.co).z for v in mesh.vertices), default=0.0)
        if abs(lowest) > FLOOR_TOLERANCE:
            problems.append(obj.name + ": lowest point is at z=%.3f; the floor is z=0" % lowest)
    return problems


def check_collection(coll):
    problems = []
    for obj in coll.all_objects:
        problems.extend(check_object(obj))
    return problems


def export_collection(coll, path):
    """The one export call. Raises ExportError listing every convention the collection breaks."""
    problems = check_collection(coll)
    if problems:
        raise ExportError("\n".join(problems))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in coll.all_objects:
        obj.select_set(True)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        object_types={'MESH', 'EMPTY', 'ARMATURE'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_triangles=True,
        add_leaf_bones=False,
        path_mode='COPY',
        embed_textures=False,
        use_custom_props=False,
    )
    bpy.ops.object.select_all(action='DESELECT')
    return path


RIG_NAME = "CharacterArmature"
# The shipped bodies' feet sit this far below the floor; a body on their rig stands where they stand.
RIG_FOOT_DEPTH = 0.02


def check_character(coll):
    """The checklist for a body on the game's rig.

    Two of the set-piece rules do not apply and are replaced: the armature keeps the shipped rig's
    name, because every Generic clip binds by that path; and the lowest point may sit the rig's own
    foot depth below the floor, because the six shipped bodies do, and a body that stood higher
    would float beside them. Everything else holds: bb_ names on the meshes, applied transforms,
    mesh named for its object, quads and triangles only, exactly one rig.
    """
    problems = []
    rigs = [obj for obj in coll.all_objects if obj.type == 'ARMATURE']
    if len(rigs) != 1 or rigs[0].name != RIG_NAME:
        problems.append("a character carries exactly one armature named " + RIG_NAME)
    for obj in coll.all_objects:
        if obj.type == 'ARMATURE':
            if any(abs(s - 1.0) > 1e-6 for s in obj.scale) or any(abs(r) > 1e-6 for r in obj.rotation_euler):
                problems.append(obj.name + ": rig transforms are not applied")
            continue
        if not NAME.match(obj.name):
            problems.append(obj.name + ": name must be bb_<category>_<name>[_<variant>][_lodN][_col]")
        if obj.type != 'MESH':
            continue
        if any(abs(s - 1.0) > 1e-6 for s in obj.scale) or any(abs(r) > 1e-6 for r in obj.rotation_euler):
            problems.append(obj.name + ": scale or rotation is not applied")
        if obj.data.name != obj.name:
            problems.append(obj.name + ": mesh data is named " + obj.data.name + "; mesh, object and file share the name")
        if any(len(poly.vertices) > 4 for poly in obj.data.polygons):
            problems.append(obj.name + ": n-gons present; quads or triangles only")
        if not any(m.type == 'ARMATURE' for m in obj.modifiers):
            problems.append(obj.name + ": not skinned to the rig")
        lowest = min(((obj.matrix_world @ v.co).z for v in obj.data.vertices), default=0.0)
        if lowest < -RIG_FOOT_DEPTH - FLOOR_TOLERANCE or lowest > FLOOR_TOLERANCE:
            problems.append(obj.name + ": lowest point is at z=%.3f; a body stands between z=0 and the rig's foot depth" % lowest)
        if not any(slot.material is not None and slot.material.name.startswith("Face") for slot in obj.material_slots):
            problems.append(obj.name + ": no \"Face\" material; the runtime finds the eyes by that name")
    return problems


def export_character(coll, path, glb_path=None):
    """A rigged body: the FBX the importer takes as a Generic rig, and a GLB beside it when asked."""
    problems = check_character(coll)
    if problems:
        raise ExportError("\n".join(problems))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in coll.all_objects:
        obj.select_set(True)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        object_types={'MESH', 'EMPTY', 'ARMATURE'},
        use_mesh_modifiers=False,
        mesh_smooth_type='FACE',
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='COPY',
        embed_textures=False,
        use_custom_props=False,
    )
    if glb_path:
        bpy.ops.export_scene.gltf(
            filepath=glb_path,
            export_format='GLB',
            use_selection=True,
            export_apply=False,
            export_animations=False,
            export_yup=True,
        )
    bpy.ops.object.select_all(action='DESELECT')
    return path


def output_path(default_name):
    """The path after '--' on the command line, or <this script's folder>/<default_name>."""
    import sys
    if "--" in sys.argv and sys.argv.index("--") + 1 < len(sys.argv):
        return sys.argv[sys.argv.index("--") + 1]
    return os.path.join(os.getcwd(), default_name)


def report(coll, path):
    meshes = [o for o in coll.all_objects if o.type == 'MESH']
    tris = sum(len(o.data.loop_triangles) if o.data.loop_triangles else sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    mats = sorted({slot.material.name for o in meshes for slot in o.material_slots if slot.material})
    print("EXPORTED %s: %d objects, ~%d triangles, materials %s" % (path, len(meshes), tris, ", ".join(mats)))

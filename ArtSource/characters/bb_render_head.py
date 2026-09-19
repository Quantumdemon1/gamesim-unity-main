"""Renders a front headshot of a body FBX, with the material colours, to a PNG - for looking at the
face's geometry before and after a shape key.

    blender --background --python ArtSource/characters/bb_render_head.py -- <fbx> <out.png> [shapekey=weight ...]
"""
import math
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
path, out = args[0], os.path.abspath(args[1])
weights = dict(a.split("=") for a in args[2:])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
body = next(o for o in bpy.data.objects if o.type == "MESH")

if body.data.shape_keys:
    for name, value in weights.items():
        block = body.data.shape_keys.key_blocks.get(name)
        if block:
            block.value = float(value)

# Head bounds from the Face material's polygons, in world space.
mesh = body.data
face_index = next((i for i, s in enumerate(body.material_slots) if s.material and s.material.name == "Face"), 0)
verts = {v for p in mesh.polygons if p.material_index == face_index for v in p.vertices}
coords = [body.matrix_world @ mesh.vertices[v].co for v in verts]
centre = sum(coords, Vector()) / len(coords)
size = max(max(c.z for c in coords) - min(c.z for c in coords), max(c.x for c in coords) - min(c.x for c in coords)) * 2.4

scene = bpy.context.scene
scene.render.engine = os.environ.get("BB_ENGINE", "BLENDER_EEVEE")
scene.render.resolution_x = scene.render.resolution_y = 512
scene.render.filepath = out
scene.render.image_settings.file_format = "PNG"

cam_data = bpy.data.cameras.new("HeadCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = size
cam = bpy.data.objects.new("HeadCam", cam_data)
scene.collection.objects.link(cam)
cam.location = centre + Vector((0.0, -6.0, 0.0))
cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)
scene.camera = cam

light_data = bpy.data.lights.new("Key", "SUN")
light_data.energy = 3.0
light = bpy.data.objects.new("Key", light_data)
scene.collection.objects.link(light)
light.rotation_euler = (math.radians(60.0), 0.0, math.radians(-20.0))
scene.world = bpy.data.worlds.new("W")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.6, 1.0)
scene.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

print("body", body.name, "hide_render", body.hide_render, "centre", tuple(round(x,3) for x in centre), "size", round(size,3), "cam", tuple(round(x,3) for x in cam.location), "clip", cam_data.clip_start, cam_data.clip_end)
bpy.ops.render.render(write_still=True)
print("rendered ->", out)

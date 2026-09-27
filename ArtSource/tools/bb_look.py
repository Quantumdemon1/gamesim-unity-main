"""Render an authored FBX to a PNG, so a set piece can be looked at instead of trusted.

The export report says how many triangles a piece has and what its materials are called. It does
not say whether the thing is the right shape, and a piece that is wrong is usually wrong in a way
a single frame would have caught - a frame floating above its feet, discs threaded through the
plinth, a lane one tenth its intended length.

Frames the imported object from a three-quarter view with an orthographic camera sized to its own
bounds, on a neutral floor, under Workbench. No lights needed and no materials honoured: this is
for silhouette and proportion, not for look.

    blender --background --python ArtSource/tools/bb_look.py -- <input.fbx> <output.png> [angle]
"""
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(argv) < 2:
    raise SystemExit("usage: bb_look.py -- <input.fbx> <output.png> [angle-degrees]")
source, target = argv[0], argv[1]
angle = float(argv[2]) if len(argv) > 2 else 35.0

bpy.ops.wm.read_homefile(use_empty=True)

win = bpy.context.window_manager.windows[0]
area = next(a for a in win.screen.areas if a.type == "VIEW_3D")
region = next(r for r in area.regions if r.type == "WINDOW")
with bpy.context.temp_override(window=win, screen=win.screen, area=area, region=region):
    bpy.ops.import_scene.fbx(filepath=source)

scene = bpy.context.scene

xs, ys, zs = [], [], []
for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    for corner in obj.bound_box:
        world = obj.matrix_world @ Vector(corner)
        xs.append(world.x); ys.append(world.y); zs.append(world.z)
if not xs:
    raise SystemExit("nothing imported from " + source)

cx, cy, cz = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2
extent = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs), 0.2)

# A floor, so the piece is seen standing on something rather than adrift.
floor = bpy.data.meshes.new("floor")
span = extent * 3
floor.from_pydata([(cx - span, cy - span, 0.0), (cx + span, cy - span, 0.0),
                   (cx + span, cy + span, 0.0), (cx - span, cy + span, 0.0)], [], [(0, 1, 2, 3)])
floor.update()
scene.collection.objects.link(bpy.data.objects.new("floor", floor))

cam_data = bpy.data.cameras.new("Look")
cam_data.type = "ORTHO"
cam_data.ortho_scale = extent * 1.5
cam = bpy.data.objects.new("Look", cam_data)
scene.collection.objects.link(cam)
a = math.radians(angle)
tilt = math.radians(22.0)
dist = extent * 4
cam.location = (cx + math.sin(a) * dist, cy - math.cos(a) * dist, cz + math.sin(tilt) * dist)
cam.rotation_euler = (math.radians(90) - tilt, 0.0, a)
scene.camera = cam

try:
    scene.render.engine = "BLENDER_WORKBENCH"
except TypeError:
    pass
scene.render.resolution_x = 520
scene.render.resolution_y = 460
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = target
bpy.ops.render.render(write_still=True)

print("LOOKED %s -> %s (extent %.2f m, %d mesh objects)"
      % (os.path.basename(source), target, extent, len([o for o in bpy.data.objects if o.type == "MESH"]) - 1))

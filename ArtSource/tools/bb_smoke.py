"""The smallest asset that can exercise the whole pipeline: one cube, one material, one export.

    blender --background --python ArtSource/tools/bb_smoke.py -- <output.fbx>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_export  # noqa: E402

bb_export.fresh_scene()
coll = bb_export.collection("bb_test_cube")

import bpy  # noqa: E402

bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
cube = bpy.context.active_object
cube.name = "bb_test_cube"
cube.data.name = "bb_test_cube"
# The origin stays on the floor; the mesh rises from it.
for vertex in cube.data.vertices:
    vertex.co.z += 0.5
bb_export.link_only(cube, coll)
cube.data.materials.append(bb_export.material("test", (0.9, 0.3, 0.2)))

path = bb_export.output_path("bb_test_cube.fbx")
bb_export.export_collection(coll, path)
bb_export.report(coll, path)

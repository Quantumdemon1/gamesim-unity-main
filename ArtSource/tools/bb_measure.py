"""Prints each FBX's bounds as Unity sees them (x, y up, z), for the size table in AuthoredAssetImportTests.

    blender --background --python ArtSource/tools/bb_measure.py -- <fbx> [<fbx> ...]
"""
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
for path in args:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        for v in obj.data.vertices:
            w = obj.matrix_world @ v.co
            lo = Vector(min(a, b) for a, b in zip(lo, w))
            hi = Vector(max(a, b) for a, b in zip(hi, w))
    size = hi - lo
    # Blender z is Unity y.
    print("MEASURE %s x=%.3f y=%.3f z=%.3f" % (path.split("/")[-1], size.x, size.z, size.y))

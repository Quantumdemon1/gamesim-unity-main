"""What a shipped Quaternius body offers a face system: materials, the Face region's UVs and bounds,
eye geometry, shape keys - and the feature clusters a procedural shape key would move.

    blender --background --python ArtSource/characters/bb_inspect_face.py -- <fbx>
"""
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
path = args[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)


def clusters(points, radius):
    """Greedy clustering by distance: good enough to name eyes, brows and a mouth."""
    groups = []
    for p in points:
        for g in groups:
            if (g["centre"] - p).length < radius:
                g["points"].append(p)
                g["centre"] = sum(g["points"], Vector()) / len(g["points"])
                break
        else:
            groups.append({"centre": p.copy(), "points": [p]})
    return groups


for obj in bpy.data.objects:
    if obj.type != "MESH":
        print("OBJ", obj.name, obj.type)
        continue
    mesh = obj.data
    print("MESH", obj.name, "verts", len(mesh.vertices), "polys", len(mesh.polygons),
          "shape keys", [k.name for k in mesh.shape_keys.key_blocks] if mesh.shape_keys else None,
          "uv layers", [u.name for u in mesh.uv_layers])
    for index, slot in enumerate(obj.material_slots):
        polys = [p for p in mesh.polygons if p.material_index == index]
        if not polys:
            continue
        verts = {v for p in polys for v in p.vertices}
        coords = [obj.matrix_world @ mesh.vertices[v].co for v in verts]
        lo = Vector((min(c.x for c in coords), min(c.y for c in coords), min(c.z for c in coords)))
        hi = Vector((max(c.x for c in coords), max(c.y for c in coords), max(c.z for c in coords)))
        mat = slot.material
        colour = None
        if mat and mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    colour = tuple(round(x, 2) for x in node.inputs["Base Color"].default_value[:3])
        print("  MAT", index, mat.name if mat else None, "polys", len(polys), "verts", len(verts),
              "bounds", tuple(round(x, 3) for x in lo), tuple(round(x, 3) for x in hi), "colour", colour)
        if mat and mat.name in ("Face", "Hair", "Skin"):
            # The front of the head only: y toward the viewer, above the neck.
            front = [c for c in coords if c.y < -0.3 and c.z > 2.3]
            for g in clusters(front, 0.12):
                c = g["centre"]
                print("    cluster", mat.name, "n", len(g["points"]), "centre", tuple(round(x, 3) for x in c),
                      "span", round(max(p.x for p in g["points"]) - min(p.x for p in g["points"]), 3),
                      round(max(p.z for p in g["points"]) - min(p.z for p in g["points"]), 3))

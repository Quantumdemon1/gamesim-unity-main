"""Builders for stylised set pieces: boxes, cylinders, rings and discs, quads and triangles only.

Everything is built in metres with Z up and joined into one object per asset whose origin is the
floor contact point, which is what bb_export.check_object then verifies. Parts are positioned by
their world coordinates directly; nothing relies on object transforms, so there is nothing to
apply and nothing to get wrong on export.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


def _finish(bm, name, mat=None, smooth_sides=False):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    if smooth_sides:
        for poly in mesh.polygons:
            poly.use_smooth = abs(poly.normal.z) < 0.5
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if mat is not None:
        mesh.materials.append(mat)
    return obj


def box(name, size, center, mat=None):
    """An axis-aligned box of the given (x, y, z) size, centred at center."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(center)
    return _finish(bm, name, mat)


def tilted_box(name, size, hinge, angle_degrees, mat=None):
    """A box rotated about the x axis at its bottom edge (the hinge), for backrests and lids."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    rotation = Matrix.Rotation(math.radians(angle_degrees), 4, 'X')
    for v in bm.verts:
        local = Vector((v.co.x * size[0], v.co.y * size[1], (v.co.z + 0.5) * size[2]))
        v.co = rotation @ local + Vector(hinge)
    return _finish(bm, name, mat)


def cylinder(name, radius, height, center_bottom, mat=None, segments=32):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=segments,
                          radius1=radius, radius2=radius, depth=height)
    for v in bm.verts:
        v.co += Vector(center_bottom) + Vector((0.0, 0.0, height / 2.0))
    return _finish(bm, name, mat, smooth_sides=True)


def disc(name, radius, center, mat=None, segments=32):
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, cap_tris=True, segments=segments, radius=radius)
    for v in bm.verts:
        v.co += Vector(center)
    return _finish(bm, name, mat)


def ring(name, outer, inner, height, center_bottom, mat=None, segments=32):
    """An annulus extruded to a height, closed top and bottom: quads all the way round."""
    bm = bmesh.new()

    def loop(radius, z):
        return [bm.verts.new((radius * math.cos(2 * math.pi * i / segments),
                              radius * math.sin(2 * math.pi * i / segments), z)) for i in range(segments)]

    outer_bottom, inner_bottom = loop(outer, 0.0), loop(inner, 0.0)
    outer_top, inner_top = loop(outer, height), loop(inner, height)

    def quads(a, b):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], a[j], b[j], b[i]))

    quads(outer_bottom, outer_top)
    quads(inner_top, inner_bottom)
    quads(outer_top, inner_top)
    quads(inner_bottom, outer_bottom)
    for v in bm.verts:
        v.co += Vector(center_bottom)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _finish(bm, name, mat, smooth_sides=True)


def plane(name, size, center, mat=None):
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0] / 2.0, v.co.y * size[1] / 2.0, 0.0)) + Vector(center)
    return _finish(bm, name, mat)


def join(parts, name):
    """Joins parts into one object named name (mesh too), origin at the world origin."""
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.select_all(action='DESELECT')
    return obj


def child(obj, parent):
    """Parents without moving: parts are already in world coordinates."""
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj

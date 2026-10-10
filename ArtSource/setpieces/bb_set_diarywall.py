"""The diary room's upholstered interview wall: square biscuit tufting behind the confessional chair.

4.6 m wide, 2.9 m high and 0.10 m deep - the box DiarySeatPose stood there before, to the
centimetre - with its origin on the floor under the middle of the wall. The back is at -0.05 in
depth and the cushions' crowns at +0.05, so placed at the studio's -0.85 the crowns stand exactly
at DiaryStudioBranding.WallFace (-0.80); the grooves are at +0.03. The front faces +y (Unity +z),
towards the camera.

Square biscuit tufting at a 0.15 m pitch, the grid measured from the left edge and the floor, so
it lies under the pack texture's own grid (8 by 8 to every 1.2 m tile, DiaryStudioBranding.PadTile):
the texture is laid with UVs 0..1 across the whole front, and Pad() repeats it 3.83 by 2.42 times.
The tufted field is the middle 3.15 m between the grid lines at -1.55 and +1.60 (the brief's
middle 3.2 m, snapped to the grid); buttons sit recessed at the groove crossings inside it. The
outer margins are flat, because the slat panels (0.7 m) stand over them. A 0.04 m round piping
runs along the top and both sides. One material, bb_mat_diary_upholstery. Set dressing: no
collider.

    blender --background --factory-startup --python ArtSource/setpieces/bb_set_diarywall.py -- <output.fbx>
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

W, H = 4.6, 2.9
BACK, CROWN, GROOVE, BUTTON = -0.05, 0.05, 0.03, 0.022
PITCH = 0.15
LEFT, RIGHT = -W / 2 + 5 * PITCH, -W / 2 + 26 * PITCH   # -1.55 and +1.60: grid lines either side of the middle 3.2 m
FLAT = 0.042          # the flat margins, just behind the crowns
SUB = 3               # quads per cell edge
PIPE = 0.02           # piping radius (0.04 m round)

X.fresh_scene()
coll = X.collection("bb_set_diarywall")
upholstery = X.material("diary_upholstery", (0.30, 0.22, 0.46), roughness=0.85)


def cushion(u, v):
    """Depth over one cell, u and v in 0..1 across it: the groove at the edges, the crown in the middle."""
    # A pillow: full across most of the cell and falling steeply into the groove, so the grid reads
    # as stitched squares rather than as a ripple (a square root read soft in the render).
    s = math.sin(math.pi * u) * math.sin(math.pi * v)
    return GROOVE + (CROWN - GROOVE) * max(0.0, s) ** 0.3


def depth(x, z):
    cx = (x + W / 2) / PITCH
    cz = z / PITCH
    return cushion(cx - math.floor(cx), cz - math.floor(cz))


def on_line(p, origin):
    k = (p - origin) / PITCH
    return abs(k - round(k)) < 1e-6


xs = [round(LEFT + PITCH * i / SUB, 6) for i in range(int(round((RIGHT - LEFT) / PITCH)) * SUB + 1)]
zs = [round(H * 0 + PITCH * j / SUB, 6) for j in range(int(math.floor(H / PITCH)) * SUB + 1)]
if zs[-1] < H - 1e-6:
    # The top row: a part cell up to the wall's top, at the cell's own subdivision.
    top = zs[-1]
    zs += [round(top + (H - top) * s / SUB, 6) for s in range(1, SUB + 1)]

bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new("UVMap")

# The tufted field: a heightfield on the grid's own lines, SUB quads to a cell, wound to face +y.
# Each cell has vertices of its own, so a cushion is smooth across itself and creased at its
# stitching: shared along the grooves, the normals smoothed straight over them and the tufting
# read as a ripple.
def height(i, j):
    x, z = xs[i], zs[j]
    y = depth(x, z) if 0 < i < len(xs) - 1 else GROOVE
    if on_line(x, -W / 2) and on_line(z, 0.0) and 0 < i < len(xs) - 1 and 0 < j < len(zs) - 1:
        y = BUTTON
    return y


grid = {}
for i, x in enumerate(xs):
    for j, z in enumerate(zs):
        grid[i, j] = (x, height(i, j), z)
column = {}
for ci in range(0, len(xs) - 1, SUB):
    for cj in range(0, len(zs) - 1, SUB):
        own = {}
        for di in range(SUB + 1):
            for dj in range(SUB + 1):
                i, j = min(ci + di, len(xs) - 1), min(cj + dj, len(zs) - 1)
                own[di, dj] = bm.verts.new(grid[i, j])
                if i in (0, len(xs) - 1):
                    column.setdefault((i, j), own[di, dj])
        for di in range(SUB):
            for dj in range(SUB):
                if ci + di >= len(xs) - 1 or cj + dj >= len(zs) - 1:
                    continue
                bm.faces.new((own[di, dj], own[di, dj + 1], own[di + 1, dj + 1], own[di + 1, dj]))

# The flat margins, one quad each, and the narrow wall from each margin down to the field's edge groove.
for side, inner, outer in ((-1, LEFT, -W / 2), (1, RIGHT, W / 2)):
    a = bm.verts.new((inner, FLAT, 0.0)); b = bm.verts.new((outer, FLAT, 0.0))
    c = bm.verts.new((outer, FLAT, H)); d = bm.verts.new((inner, FLAT, H))
    bm.faces.new((a, d, c, b) if side > 0 else (a, b, c, d))
    edge = [column[0 if side < 0 else len(xs) - 1, j] for j in range(len(zs))]
    for j in range(len(zs) - 1):
        lo, hi = edge[j], edge[j + 1]
        m0 = bm.verts.new((inner, FLAT, lo.co.z)); m1 = bm.verts.new((inner, FLAT, hi.co.z))
        bm.faces.new((lo, hi, m1, m0) if side > 0 else (m0, m1, hi, lo))

# The back and the slab's outer sides.
bl = bm.verts.new((-W / 2, BACK, 0.0)); br = bm.verts.new((W / 2, BACK, 0.0))
tr = bm.verts.new((W / 2, BACK, H)); tl = bm.verts.new((-W / 2, BACK, H))
bm.faces.new((bl, br, tr, tl))
for (p, q) in (((-W / 2, 0.0), (-W / 2, H)), ((W / 2, H), (W / 2, 0.0)), ((-W / 2, H), (W / 2, H)), ((W / 2, 0.0), (-W / 2, 0.0))):
    v0 = bm.verts.new((p[0], BACK, p[1])); v1 = bm.verts.new((q[0], BACK, q[1]))
    v2 = bm.verts.new((q[0], FLAT, q[1])); v3 = bm.verts.new((p[0], FLAT, p[1]))
    bm.faces.new((v0, v1, v2, v3))

# Buttons: a small diamond, 0.018 m across, sat in the dimple at every crossing inside the field.
r = 0.009
buttons = 0
for i, x in enumerate(xs):
    if not (0 < i < len(xs) - 1) or not on_line(x, -W / 2):
        continue
    for j, z in enumerate(zs):
        if not (0 < j < len(zs) - 1) or not on_line(z, 0.0):
            continue
        y = BUTTON + 0.004
        vs = [bm.verts.new((x, y, z - r)), bm.verts.new((x + r, y, z)), bm.verts.new((x, y, z + r)), bm.verts.new((x - r, y, z))]
        bm.faces.new((vs[0], vs[3], vs[2], vs[1]))
        buttons += 1

bm.normal_update()
# UVs 0..1 across the whole front (the brief's override of bb_build's metre projection), the
# other faces by the same plane - nothing else on the wall carries the pattern.
for face in bm.faces:
    for loop in face.loops:
        co = loop.vert.co
        loop[uv_layer].uv = ((co.x + W / 2) / W, co.z / H)
backwards = sum(1 for f in bm.faces if f.calc_center_median().y > 0.015 and f.normal.y < -0.2)
mesh = bpy.data.meshes.new("bb_set_diarywall_front")
bm.to_mesh(mesh)
bm.free()
for poly in mesh.polygons:
    poly.use_smooth = True
front = bpy.data.objects.new("bb_set_diarywall_front", mesh)
bpy.context.scene.collection.objects.link(front)
mesh.materials.append(upholstery)


def tube(name, points, radius, segments=8):
    """A round tube along a polyline (the piping), quads only, capped at both ends."""
    tb = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        a = points[max(0, i - 1)]
        b = points[min(len(points) - 1, i + 1)]
        t = (Vector(b) - Vector(a)).normalized()
        side = t.cross(Vector((0.0, 1.0, 0.0)))
        side.normalize()
        up = side.cross(t).normalized()
        ring = []
        for s in range(segments):
            ang = 2.0 * math.pi * s / segments
            ring.append(tb.verts.new(Vector(p) + (side * math.cos(ang) + up * math.sin(ang)) * radius))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for s in range(segments):
            tb.faces.new((rings[i][s], rings[i + 1][s], rings[i + 1][(s + 1) % segments], rings[i][(s + 1) % segments]))
    tb.faces.new(rings[0])
    tb.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(tb, faces=tb.faces)
    big = [f for f in tb.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(tb, faces=big)
    return B._finish(tb, name, upholstery, smooth_sides=True)


# The piping: up the left side, along the top, down the right, sitting on the front edge.
py = CROWN - PIPE
left = [(-W / 2 + PIPE, py, PIPE + z * (H - 2 * PIPE) / 24) for z in range(25)]
top = [(-W / 2 + PIPE + x * (W - 2 * PIPE) / 46, py, H - PIPE) for x in range(47)]
right = [(W / 2 - PIPE, py, H - PIPE - z * (H - 2 * PIPE) / 24) for z in range(25)]
pipe = tube("piping", left + top[1:] + right[1:], PIPE)

wall = B.join([front, pipe], "bb_set_diarywall")
X.link_only(wall, coll)
verts = wall.data.vertices
tri_count = sum(len(p.vertices) - 2 for p in wall.data.polygons)
print("DIARYWALL %d triangles, %d buttons, %d backward front faces, bounds x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (
    tri_count, buttons, backwards,
    min(v.co.x for v in verts), max(v.co.x for v in verts), min(v.co.y for v in verts), max(v.co.y for v in verts),
    min(v.co.z for v in verts), max(v.co.z for v in verts)))

path = X.output_path("bb_set_diarywall.fbx")
X.export_collection(coll, path)
X.report(coll, path)

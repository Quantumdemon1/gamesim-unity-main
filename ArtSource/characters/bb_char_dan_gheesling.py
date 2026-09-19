"""Dan Gheesling, on the game's own rig: a houseguest body for the All-Stars roster's template.

The character sheet (stylised realism: spiky brown hair, dark button-up with the sleeves rolled to
the elbow, dark jeans, white low sneakers, a grin) is carried by silhouette, outfit and palette at
the cast's own scale and poly budget. The body is the shipped CC0 base (Quaternius, Casual2_Male)
because every clip the game plays - walk, sit, talk, listen, the four reactions - is a Generic clip
authored on that 32-bone skeleton, and a body on any other rig would stand still. The eyes stay on
the "Face" material, which is how FaceExpression finds them; the grin is on its own material so the
eye clusters are exactly what they were.

Built as the set pieces are: primitives in metres, quads and triangles, one Principled BSDF per
material; then weighted from the body's nearest vertex and joined into it. Exports the FBX the
importer knows (Characters/Generic: a Generic rig, readable for the face) and, beside it, a GLB.

    blender --background --python ArtSource/characters/bb_char_dan_gheesling.py -- <output.fbx>
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402

NAME = "bb_char_dan_gheesling"
BASE = os.path.join(HERE, "..", "..", "Assets", "Gamesim", "Art", "External", "QuaterniusCharacters", "Casual2_Male.fbx")

# The sheet's palette. Lowercase names on purpose: the runtime recolours any material whose name
# contains "Shirt" with the houseguest's palette colour, and Dan's shirt is black on the sheet.
PALETTE = {
    "Skin": ("bb_mat_dan_skin", (0.85, 0.64, 0.49), 0.65),
    "Shirt": ("bb_mat_dan_shirt", (0.13, 0.13, 0.14), 0.70),
    "Pants": ("bb_mat_dan_jeans", (0.16, 0.20, 0.30), 0.75),
    "Belt": ("bb_mat_dan_belt", (0.09, 0.07, 0.06), 0.60),
    "Hair": ("bb_mat_dan_hair", (0.30, 0.19, 0.10), 0.70),
}
NEW = {
    "sneaker": ("bb_mat_dan_sneaker", (0.92, 0.92, 0.92), 0.45),
    "sole": ("bb_mat_dan_sole", (0.78, 0.78, 0.76), 0.60),
    "button": ("bb_mat_dan_button", (0.74, 0.74, 0.71), 0.40),
    "mouth": ("bb_mat_dan_mouth", (0.96, 0.94, 0.90), 0.40),
}


def paint(mat, colour, roughness):
    """One Principled BSDF wired to the output, its colour also on the viewport display."""
    mat.use_nodes = True
    tree = mat.node_tree
    bsdfs = [n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"]
    out = next((n for n in tree.nodes if n.type == "OUTPUT_MATERIAL"), None)
    bsdf = bsdfs[0] if bsdfs else tree.nodes.new("ShaderNodeBsdfPrincipled")
    if out is None:
        out = tree.nodes.new("ShaderNodeOutputMaterial")
    for n in list(tree.nodes):
        if n not in (bsdf, out):
            tree.nodes.remove(n)
    if not any(link.to_node == out for link in tree.links):
        tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = (colour[0], colour[1], colour[2], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    mat.diffuse_color = (colour[0], colour[1], colour[2], 1.0)
    mat.roughness = roughness
    mat.metallic = 0.0
    return mat


def material(name, colour, roughness):
    return paint(bpy.data.materials.get(name) or bpy.data.materials.new(name), colour, roughness)


# ---------------------------------------------------------------- the base body

X.fresh_scene()
coll = X.collection(NAME)
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=os.path.abspath(BASE), use_anim=False, ignore_leaf_bones=False)
imported = [o for o in bpy.data.objects if o not in before]
for ob in imported:
    X.link_only(ob, coll)
rig = next(o for o in imported if o.type == "ARMATURE")
body = next(o for o in imported if o.type == "MESH")
rig.name = rig.data.name = "CharacterArmature"   # the clips bind by this path; it is not a bb_ name
body.name = body.data.name = NAME
rig.animation_data_clear()
body.animation_data_clear()
for pb in rig.pose.bones:
    pb.matrix_basis.identity()
rig.location = (0.0, 0.0, 0.0)
me = body.data

slot = {s.material.name.split(".")[0]: i for i, s in enumerate(body.material_slots)}
for old, (new, colour, rough) in PALETTE.items():
    mat = body.material_slots[slot[old]].material
    mat.name = new
    paint(mat, colour, rough)
face = body.material_slots[slot["Face"]].material
face.name = "Face"
paint(face, (1.0, 1.0, 0.94), 0.5)
for key, (new, colour, rough) in NEW.items():
    me.materials.append(material(new, colour, rough))
M = {s.material.name: i for i, s in enumerate(body.material_slots)}
SKIN, SHIRT, HAIR = M["bb_mat_dan_skin"], M["bb_mat_dan_shirt"], M["bb_mat_dan_hair"]
SNEAKER, SOLE, BUTTON, MOUTH = M["bb_mat_dan_sneaker"], M["bb_mat_dan_sole"], M["bb_mat_dan_button"], M["bb_mat_dan_mouth"]

# Long sleeves rolled to the elbow: the upper arms wear the shirt; forearms and hands stay skin.
# Bounded in z as well as x, because the head is wider than the shoulders and sits above them.
for p in me.polygons:
    if p.material_index == SKIN and 0.30 < abs(p.center.x) < 0.80 and 1.55 < p.center.z < 2.15:
        p.material_index = SHIRT
# White sneakers: everything below the trouser hem.
for p in me.polygons:
    if p.center.z < 0.17:
        p.material_index = SNEAKER
# The base's hair goes; Dan's is built below.
bm = bmesh.new()
bm.from_mesh(me)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index == HAIR], context="FACES")
bm.to_mesh(me)
bm.free()
me.update()

# ---------------------------------------------------------------- parts

parts = []


def emit(name, bm, mat_index):
    for f in bm.faces:
        f.material_index = mat_index
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for s in body.material_slots:
        mesh.materials.append(s.material)
    ob = bpy.data.objects.new(name, mesh)
    coll.objects.link(ob)
    parts.append(ob)
    return ob


def box(name, size, center, mat, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if rot is not None:
        bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return emit(name, bm, mat)


def cone(name, r1, r2, depth, matrix, mat, segments=6):
    bm = bmesh.new()
    kw = dict(cap_ends=True, cap_tris=True, segments=segments, depth=depth)
    try:
        bmesh.ops.create_cone(bm, radius1=r1, radius2=r2, **kw)
    except TypeError:  # the older spelling of the same op
        bmesh.ops.create_cone(bm, diameter1=r1 * 2, diameter2=r2 * 2, **kw)
    bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
    return emit(name, bm, mat)


def aim(pos, direction):
    return Matrix.Translation(pos) @ Vector(direction).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()


# The hair cap: a shell over the scalp, above the brows at the front, lower at the sides and back.
def scalp_face(p):
    c = p.center
    if p.material_index != SKIN:
        return False
    if c.y < -0.22:
        return c.z > 2.79
    if c.y <= 0.12:
        return c.z > 2.60
    return c.z > 2.46


scalp = [p for p in me.polygons if scalp_face(p)]
bm = bmesh.new()
vmap = {}
for p in scalp:
    vs = []
    for i in p.vertices:
        if i not in vmap:
            vmap[i] = bm.verts.new(me.vertices[i].co.copy())
        vs.append(vmap[i])
    bm.faces.new(vs)
bm.normal_update()
region = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces))
for v in [g for g in region["geom"] if isinstance(g, bmesh.types.BMVert)]:
    v.co += v.normal * 0.05
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
emit("part_hair_cap", bm, HAIR)

# The spikes: candidates from the scalp's faces, edges and corners, thinned to an even spread,
# swept up, forward at the front and back at the back, tallest over the brow.
centre = Vector((0.0, 0.03, 2.78))
cands = []
for p in scalp:
    cands.append(p.center.copy())
    vs = [me.vertices[i].co for i in p.vertices]
    for a, b in zip(vs, vs[1:] + vs[:1]):
        cands.append((a + b) / 2.0)
    cands.extend(v.copy() for v in vs)
cands = [c for c in cands if c.z > 2.80 and not (c.y < -0.30 and c.z < 2.92)]
cands.sort(key=lambda c: (-c.z, abs(c.x)))
anchors = []
for c in cands:
    if all((c - a).length > 0.125 for a in anchors):
        anchors.append(c)
for i, c in enumerate(anchors[:30]):
    n = (c - centre).normalized()
    sweep = -0.40 if c.y < -0.15 else (0.35 if c.y > 0.15 else -0.05)
    d = (n * 0.55 + Vector((0.0, sweep, 1.0))).normalized()
    front = max(0.0, min(1.0, (0.25 - c.y) / 0.6))
    h = 0.15 + 0.12 * front
    r = 0.062 + 0.018 * ((i * 5) % 3) / 2.0
    base = c + n * 0.045
    cone("part_spike_%02d" % i, r, 0.01, h, aim(base + d * (h / 2.0), d), HAIR)

# The shirt: a collar stand, two collar points, a placket, five buttons, a cuff where each sleeve rolls.
cone("part_collar_band", 0.215, 0.215, 0.07, Matrix.Translation(Vector((0.0, 0.02, 2.085))), SHIRT, segments=14)
for sign, tag in ((1.0, "l"), (-1.0, "r")):
    rot = Matrix.Rotation(math.radians(-22.0), 4, "X") @ Matrix.Rotation(math.radians(-30.0 * sign), 4, "Z")
    box("part_collar_" + tag, (0.17, 0.035, 0.21), (sign * 0.105, -0.245, 2.13), SHIRT, rot)
box("part_placket_low", (0.07, 0.03, 0.66), (0.0, -0.232, 1.53), SHIRT)
box("part_placket_high", (0.07, 0.03, 0.22), (0.0, -0.258, 1.95), SHIRT)
button_rot = Matrix.Rotation(math.radians(90.0), 4, "X")
for k, z in enumerate((1.30, 1.47, 1.64, 1.81, 1.97)):
    y = -0.252 if z < 1.85 else -0.278
    cone("part_button_%d" % k, 0.021, 0.021, 0.014, Matrix.Translation(Vector((0.0, y, z))) @ button_rot, BUTTON, segments=10)
for sign, tag in ((1.0, "l"), (-1.0, "r")):
    cone("part_cuff_" + tag, 0.155, 0.155, 0.10,
         Matrix.Translation(Vector((sign * 0.805, 0.01, 1.885))) @ Matrix.Rotation(math.radians(90.0), 4, "Y"), SHIRT, segments=12)

# The sneakers' soles.
for sign, tag in ((1.0, "l"), (-1.0, "r")):
    box("part_sole_" + tag, (0.27, 0.34, 0.04), (sign * 0.264, 0.045, 0.0), SOLE)

# The grin: a shallow arc of small blocks on its own material, clear of the Face submesh, whose two
# eye clusters FaceExpression splits by side.
chin = [v.co for v in me.vertices if abs(v.co.x) < 0.16 and 2.33 < v.co.z < 2.47 and v.co.y < 0]
front_y = min(v.y for v in chin) if chin else -0.47
for k, x in enumerate((-0.11, -0.055, 0.0, 0.055, 0.11)):
    dip = 0.028 * (1.0 - (abs(x) / 0.11) ** 2)
    box("part_mouth_%d" % k, (0.062, 0.02, 0.026), (x, front_y - 0.008, 2.385 + dip), MOUTH)

# ---------------------------------------------------------------- weights and the join

names = [g.name for g in body.vertex_groups]


def weights_of(vi):
    return [(names[g.group], g.weight) for g in me.vertices[vi].groups if g.weight > 0.0]


for ob in parts:
    for n in names:
        ob.vertex_groups.new(name=n)
    if ob.name.startswith(("part_hair", "part_spike", "part_mouth")):
        ob.vertex_groups["Head"].add([v.index for v in ob.data.vertices], 1.0, "REPLACE")
        continue
    for v in ob.data.vertices:
        ok, _, _, fi = body.closest_point_on_mesh(v.co)
        if not ok:
            continue
        nearest = min(me.polygons[fi].vertices, key=lambda idx: (me.vertices[idx].co - v.co).length)
        for gname, w in weights_of(nearest):
            ob.vertex_groups[gname].add([v.index], w, "REPLACE")

with bpy.context.temp_override(active_object=body, selected_editable_objects=[body] + parts, selected_objects=[body] + parts):
    bpy.ops.object.join()
me = body.data
me.name = NAME

# The base imports facing -Y and the six shipped bodies face +Z in Unity; through this pipeline's
# axis baking that needs the model facing +Y here. Rotated in the data, mesh and bones alike, so
# every transform stays applied and the clips' bone-local rotations still mean what they meant.
TURN = Matrix.Rotation(math.pi, 4, "Z")
me.transform(TURN)
rig.data.transform(TURN)
me.update()

# ---------------------------------------------------------------- export

path = X.output_path(NAME + ".fbx")
X.export_character(coll, path, glb_path=os.path.splitext(path)[0] + ".glb")
counts = {}
for p in me.polygons:
    counts[p.material_index] = counts.get(p.material_index, 0) + 1
print("bb_char_dan_gheesling: verts %d polys %d dims %.2f x %.2f x %.2f" % (
    len(me.vertices), len(me.polygons), body.dimensions.x, body.dimensions.y, body.dimensions.z))
print("  materials:", {s.material.name: counts.get(i, 0) for i, s in enumerate(body.material_slots)})
print("  exported:", path, "and", os.path.splitext(path)[0] + ".glb")

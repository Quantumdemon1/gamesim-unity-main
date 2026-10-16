"""Project-authored flight case: original03 geometry and its four procedural atlas maps.

Everything here is made by this script: the geometry from bb_build primitives, the four
1024x1024 maps from the glyph table and the hash grain below (no image is read in), and the FBX
through the house's one export call, bb_export.export_collection (metres, -Z forward / Y up with
the transform baked), like every other set piece. Front at -y (Unity -z): the BROADCAST CASE 01
label and the latches; the rear LIVE mark and the hinges at +y (Unity +z). Origin at the floor
under the centre; 0.90 x 0.50 x 0.50 m. Dressing, so no collider. Run only against an ABSENT
output root; no saved Blender file or render.

    blender --background --factory-startup --python bb_set_flight_case.py -- <absent output root>

writes <root>/SetPieces/bb_set_flight_case.fbx and <root>/Textures/FlightCase/bb_flightcase_*.png.
"""
import sys
sys.dont_write_bytecode = True
import array
import hashlib
import json
import math
import os
from pathlib import Path
import bpy

ROOT_NAME = "bb_set_flight_case"
MAP_NAMES = ("albedo", "metallic", "normal", "occlusion")
UV_NAME = "UVMap"
UV_EPSILON = 1e-7
# Seams flatter than this are smooth: above the non-planar bevel quads' diagonals (1.2 degrees at
# most), below the model's shallowest real crease (12.6 degrees), so any value in that gap exports
# the same normals (1, 5 and 10 were tried).
SHARP_DEGREES = 5.0
GLYPHS = {
    "A": (14,17,17,31,17,17,17), "B": (30,17,17,30,17,17,30),
    "C": (14,17,16,16,16,17,14), "D": (30,17,17,17,17,17,30),
    "E": (31,16,16,30,16,16,31), "F": (31,16,16,30,16,16,16),
    "I": (31,4,4,4,4,4,31), "L": (16,16,16,16,16,16,31),
    "O": (14,17,17,17,17,17,14), "R": (30,17,17,30,20,18,17),
    "S": (15,16,16,14,1,1,30), "T": (31,4,4,4,4,4,4),
    "V": (17,17,17,17,17,10,4), "0": (14,17,19,21,25,17,14),
    "1": (4,12,4,4,4,4,14), " ": (0,0,0,0,0,0,0),
}
# Author-designed atlas tiles, all sizes fit 4x4 grid with four-pixel guard bands.
TILES = {"navy":0, "steel":1, "brass":2, "rubber":3, "label":4,
         "hazard":5, "inset":6, "mark":7}
COLORS = [(0.075,0.13,0.22),(.60,.65,.72),(.67,.49,.22),(.025,.031,.040),
          (.80,.83,.87),(.74,.54,.20),(.04,.065,.10),(.07,.12,.19)]
METAL = [.04,.82,.84,0.0,0.0,0.0,.12,.04]
SMOOTH = [.30,.51,.50,.16,.31,.31,.23,.30]


def fresh_write(path, data):
    with open(path, "xb") as stream:
        stream.write(data)


def write_json(path, value):
    data = (json.dumps(value, indent=2, sort_keys=True, allow_nan=False)+"\n").encode()
    if len(data) > 2*1024*1024:
        raise RuntimeError("JSON evidence exceeds 2 MiB")
    fresh_write(path, data)


def check_absent(path):
    if path.exists():
        raise RuntimeError("Refuse existing artifact: " + str(path))


def glyph_pixel(text, x, y, scale, left, bottom):
    x, y = x-left, y-bottom
    if x < 0 or y < 0 or y >= 7*scale:
        return False
    col = x // (6*scale)
    if col >= len(text) or x % (6*scale) >= 5*scale:
        return False
    return bool(GLYPHS[text[col]][6-y//scale] & (1 << (4-(x % (6*scale))//scale)))


def make_maps(out):
    images = {}
    for kind in MAP_NAMES:
        pixels = array.array("f", [0.0]) * (1024*1024*4)
        for y in range(1024):
            for x in range(1024):
                tile = (y//256)*4+x//256
                tile = tile if tile < 8 else 1
                px, py = x % 256, y % 256
                # Deterministic fine original grain; no RNG or third-party imagery.
                noise = (((x*73856093) ^ (y*19349663)) & 255)/255.0-.5
                rgb = COLORS[tile]
                if tile in (0,1,2,6,7):
                    grain = noise*.020 + math.sin(py*.71)*(.008 if tile in (1,2) else .002)
                    rgb = tuple(max(0.0,min(1.0,c+grain)) for c in rgb)
                if tile == 4:
                    border = px<9 or px>246 or py<9 or py>246
                    ink = glyph_pixel("BROADCAST",px,py,4,20,145) or glyph_pixel("CASE 01",px,py,4,44,76)
                    rgb = (.035,.060,.095) if ink or border else rgb
                if tile == 5:
                    rgb = (.05,.06,.075) if ((px+py)//37)%2 else rgb
                if tile == 7 and glyph_pixel("LIVE",px,py,8,34,100):
                    rgb = (.76,.59,.27)
                if kind == "albedo":
                    rgba = (*rgb,1.0)
                elif kind == "metallic":
                    rgba = (METAL[tile],0.0,0.0,SMOOTH[tile]+noise*.025)
                elif kind == "normal":
                    # Very restrained tangent-space micrograin; geometric bevels do the work.
                    dx = noise*.013 if tile in (0,1,2,6,7) else 0.0
                    rgba = (.5+dx,.5,math.sqrt(max(0,1-(2*dx)**2))*.5+.5,1.0)
                else:
                    # Neutral map, deliberately NOT a claim of baked inter-part AO.
                    rgba = (1.0,1.0,1.0,1.0)
                offset = (y*1024+x)*4
                pixels[offset:offset+4] = array.array("f", rgba)
        path = out / ("bb_flightcase_"+kind+".png")
        check_absent(path)
        image = bpy.data.images.new("bb_flightcase_"+kind, width=1024, height=1024, alpha=True)
        image.colorspace_settings.name = "sRGB" if kind == "albedo" else "Non-Color"
        image.pixels.foreach_set(pixels)
        image.filepath_raw = str(path)
        image.file_format = "PNG"
        image.update()
        image.save()
        images[kind] = image
    return images


def atlas_material(images):
    mat = bpy.data.materials.new("bb_mat_flightcase_atlas")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    uv_map = nodes.new("ShaderNodeUVMap")
    uv_map.uv_map = UV_NAME
    uv_map.location = (-900, 0)
    tex = {}
    for index, kind in enumerate(MAP_NAMES):
        node = nodes.new("ShaderNodeTexImage")
        node.image, node.label, node.location = images[kind], kind, (-650,-index*220)
        node.interpolation = "Linear"
        node.extension = "EXTEND"
        links.new(uv_map.outputs["UV"], node.inputs["Vector"])
        tex[kind] = node
    links.new(tex["albedo"].outputs["Color"], shader.inputs["Base Color"])
    channel = nodes.new("ShaderNodeSeparateColor")
    links.new(tex["metallic"].outputs["Color"],channel.inputs["Color"])
    links.new(channel.outputs["Red"],shader.inputs["Metallic"])
    invert = nodes.new("ShaderNodeMath")
    invert.operation = "SUBTRACT"
    invert.inputs[0].default_value = 1.0
    links.new(tex["metallic"].outputs["Alpha"],invert.inputs[1])
    links.new(invert.outputs[0],shader.inputs["Roughness"])
    normal = nodes.new("ShaderNodeNormalMap")
    normal.uv_map = UV_NAME
    links.new(tex["normal"].outputs["Color"],normal.inputs["Color"])
    links.new(normal.outputs["Normal"],shader.inputs["Normal"])
    return mat


def material_uv_evidence(mat):
    textures = [n for n in mat.node_tree.nodes if n.bl_idname == "ShaderNodeTexImage"]
    uv_nodes = [n for n in mat.node_tree.nodes if n.bl_idname == "ShaderNodeUVMap"]
    if len(textures) != 4 or len(uv_nodes) != 1 or uv_nodes[0].uv_map != UV_NAME:
        raise RuntimeError("Exact atlas UV source graph required")
    rows = []
    for node in textures:
        links = list(node.inputs["Vector"].links)
        if (node.image is None or node.image.name not in ["bb_flightcase_"+kind for kind in MAP_NAMES]
                or len(links) != 1 or links[0].from_node != uv_nodes[0]
                or links[0].from_socket.name != "UV"):
            raise RuntimeError("Texture Vector is not bound to exact UVMap")
        rows.append({"texture":node.image.name,"vectorSource":links[0].from_node.name,
                     "vectorSocket":links[0].from_socket.name,"uvMap":uv_nodes[0].uv_map})
    if len({row["texture"] for row in rows}) != 4:
        raise RuntimeError("Atlas texture assignment duplicates")
    normals = [n for n in mat.node_tree.nodes if n.bl_idname == "ShaderNodeNormalMap"]
    if len(normals) != 1 or normals[0].uv_map != UV_NAME:
        raise RuntimeError("Tangent normal basis is not exact UVMap")
    return rows


def one_uv_layer(mesh):
    if len(mesh.uv_layers) > 1:
        raise RuntimeError("Ambiguous part UV layers; refuse guessed merge")
    layer = mesh.uv_layers.active if mesh.uv_layers else mesh.uv_layers.new(name=UV_NAME)
    if layer is None:
        raise RuntimeError("Part UV layer has no active selection")
    layer.name = UV_NAME
    mesh.uv_layers.active_index = 0
    layer.active_render = True
    return layer


def tile_uv(obj, tile):
    layer = one_uv_layer(obj.data)
    tx, ty = tile % 4, tile//4
    coords = [v.co for v in obj.data.vertices]
    lo = [min(v[a] for v in coords) for a in range(3)]
    hi = [max(v[a] for v in coords) for a in range(3)]
    for face in obj.data.polygons:
        major = max(range(3),key=lambda a:abs(face.normal[a]))
        axes = (0,1) if major==2 else ((0,2) if major==1 else (1,2))
        for index in face.loop_indices:
            co = obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv = [(co[a]-lo[a])/max(hi[a]-lo[a],1e-8) for a in axes]
            layer.uv[index].vector = ((tx+.016+uv[0]*.968)/4,(ty+.016+uv[1]*.968)/4)


def checked_uv_layer(mesh):
    if len(mesh.uv_layers) != 1:
        raise RuntimeError("Exactly one joined UV layer required")
    layer = mesh.uv_layers[0]
    if layer.name != UV_NAME or mesh.uv_layers.active_index != 0 or not layer.active_render:
        raise RuntimeError("Named/active/render UVMap contract failed")
    if len(layer.uv) != len(mesh.loops) or len(layer.data) != len(mesh.loops) or not mesh.loops:
        raise RuntimeError("UV/loop cardinality failed")
    if any(tuple(a.vector) != tuple(b.uv) for a,b in zip(layer.uv,layer.data)):
        raise RuntimeError("Modern/legacy UV readback differs")
    return layer


def uv_tile(uv):
    values = [float(v) for v in uv]
    if len(values) != 2 or any(not math.isfinite(v) or v < 0 or v > 1 for v in values):
        raise RuntimeError("Nonfinite/out-of-range UV")
    tx, ty = [int(v*4) for v in values]
    tile = ty*4+tx
    if tile not in TILES.values() or tx >= 4 or ty >= 4:
        raise RuntimeError("UV outside authored atlas tiles")
    if not ((tx+.016)/4-UV_EPSILON <= values[0] <= (tx+.984)/4+UV_EPSILON and
            (ty+.016)/4-UV_EPSILON <= values[1] <= (ty+.984)/4+UV_EPSILON):
        raise RuntimeError("UV loop violates semantic tile guard")
    return tile


def part_uv_evidence(obj, expected_tile):
    layer = checked_uv_layer(obj.data)
    actual = [uv_tile(loop.vector) for loop in layer.uv]
    if any(tile != expected_tile for tile in actual):
        raise RuntimeError("Part loop assigned to wrong semantic atlas tile: "+obj.name)
    values = [tuple(loop.vector) for loop in layer.uv]
    encoded = json.dumps(values,separators=(",",":"),allow_nan=False).encode()
    return {"part":obj.name,"tile":expected_tile,"loops":len(values),"uvLayer":UV_NAME,
            "activeRender":True,"allLoopsGuarded":True,"zeroLoopCount":0,
            "min":[min(v[a] for v in values) for a in range(2)],
            "max":[max(v[a] for v in values) for a in range(2)],
            "uvSha256":hashlib.sha256(encoded).hexdigest()}


def joined_uv_evidence(obj, expected_histogram=None):
    layer = checked_uv_layer(obj.data)
    histogram = {str(tile):0 for tile in sorted(TILES.values())}
    for loop in layer.uv:
        histogram[str(uv_tile(loop.vector))] += 1
    if expected_histogram is not None and histogram != expected_histogram:
        raise RuntimeError("Joined semantic tile loop inventory changed")
    return {"name":obj.name,"uvLayer":UV_NAME,"activeRender":True,"loops":len(layer.uv),
            "zeroLoopCount":0,"allLoopsGuarded":True,"tileHistogram":histogram}

def build_lod(B, X, coll, mat, lod):
    parts = []
    part_tiles = {}
    segments = 3 if lod==0 else 1
    def soft(name,size,center,tile="steel",radius=.006):
        obj = B.soft_box(name,size,center,mat,radius=radius,segments=segments)
        tile_uv(obj,TILES[tile])
        # Hard-surface shading: planar panels stay flat and every bevel facet keeps its crease
        # (12.6 degrees and up), but a seam under SHARP_DEGREES is one surface with one normal.
        # The export splits each quad into two triangles, and shaded flat each triangle took its
        # own normal: a bevel corner's non-planar quad gave two normals up to 1.2 degrees apart on
        # its diagonal's corners, and coplanar triangles two that differ by float noise, on one
        # vertex with one UV. Unity's import keeps a single normal for such a corner, so the
        # imported mesh moved those normals past FlightCaseAssetTests' 1e-5 guard.
        obj.data.shade_smooth()
        obj.data.set_sharp_from_angle(angle=math.radians(SHARP_DEGREES))
        parts.append(obj)
        part_tiles[obj] = TILES[tile]
        return obj
    def decal(name,width,height,center,tile):
        x,y,z = center
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(x-width/2,y,z-height/2),(x+width/2,y,z-height/2),
                         (x+width/2,y,z+height/2),(x-width/2,y,z+height/2)],[],[(0,1,2,3) if y<0 else (3,2,1,0)])
        mesh.update()
        expected_y = -1.0 if y < 0 else 1.0
        if len(mesh.polygons) != 1 or mesh.polygons[0].normal.y * expected_y < .999:
            raise RuntimeError("Graphic face winding is not outward")
        obj = bpy.data.objects.new(name,mesh)
        bpy.context.scene.collection.objects.link(obj)
        mesh.materials.append(mat)
        tile_uv(obj,TILES[tile])
        if y>0:
            for loop in mesh.uv_layers[0].uv:
                loop.vector.x=(2*(TILES[tile]%4)+1)/4-loop.vector.x
        parts.append(obj)
        part_tiles[obj] = TILES[tile]
    # Two solid, separate closed shells, not an open bin. Black gasket gives the lid seam.
    soft("body",(.85,.45,.302),(0,0,.184),"navy",.009)
    soft("gasket",(.854,.454,.012),(0,0,.339),"rubber",.004)
    soft("lid",(.85,.45,.137),(0,0,.4145),"navy",.009)
    # Recess rail faces behind corner protectors/latches, retaining the corner-defined envelope.
    for z in (.060,.324,.354,.478):
        for y in (-.230,.230):
            soft("rail_long",(.86,.032,.040),(0,y,z),radius=.004)
        for x in (-.430,.430):
            soft("rail_end",(.032,.436,.040),(x,0,z),radius=.004)
    # Corner protections define exact full .9 x .5 envelope; top rails define z=.5.
    for x in (-.415,.415):
        for y in (-.215,.215):
            for z in (.075,.455):
                soft("corner",(.070,.070,.090),(x,y,z),radius=.010)
    for x in (-.32,.32):
        for y in (-.16,.16):
            soft("foot",(.11,.09,.024),(x,y,.012),"rubber",.005)
    # Inset front/back wall panels are framed by the raised flight-case extrusions.
    for side in (-1,1):
        soft("panel",(.756,.010,.198),(0,side*.226,.198),"navy",.005)
        # Real handle recess cup with visible side/end walls and a separate bent grip.
        soft("cup_back",(.245,.006,.122),(0,side*.233,.212),"inset",.006)
        for x in (-.121,.121):
            soft("cup_side",(.022,.022,.124),(x,side*.238,.212),radius=.004)
        for z in (.151,.273):
            soft("cup_end",(.264,.022,.022),(0,side*.238,z),radius=.004)
        if lod==0:
            handle = B.tube_along("grip",[(-.095,side*.24,.233),(-.095,side*.24,.190),
                                           (.095,side*.24,.190),(.095,side*.24,.233)],
                                  .009,segments=8,mat=mat)
            tile_uv(handle,TILES["rubber"])
            parts.append(handle)
            part_tiles[handle] = TILES["rubber"]
        else:
            soft("grip",(.208,.018,.025),(0,side*.24,.192),"rubber",.006)
        for x in (-.098,.098):
            soft("grip_mount",(.035,.018,.035),(x,side*.24,.233),radius=.004)
    # Two front draw latches span the genuine lid seam; rear hinges are separate hardware.
    for x in (-.285,.285):
        # Base front=-.248; lever front=-.250; rails front=-.246 (no shared front plane).
        soft("latch_base",(.078,.025,.135),(x,-.2355,.337),radius=.008)
        soft("latch_lever",(.048,.010,.071),(x,-.245,.330),"brass",.004)
        soft("latch_hook",(.035,.009,.032),(x,-.245,.377),radius=.003)
        soft("hinge_plate",(.096,.018,.105),(x,.239,.344),radius=.005)
        soft("hinge_knuckle",(.106,.019,.027),(x,.2405,.344),"brass",.005)
    # Top panel inset and graphic plates have dedicated atlas islands, not wraparound labels.
    soft("top_inset",(.738,.338,.006),(0,0,.486),"navy",.004)
    decal("broadcast_label",.245,.070,(0,-.2265,.421),"label")
    decal("hazard_left",.125,.035,(-.277,-.234,.186),"hazard")
    decal("hazard_right",.125,.035,(.277,-.234,.186),"hazard")
    decal("rear_mark",.125,.035,(.277,.234,.186),"mark")
    if lod==0:
        for x in (-.38,.38):
            for side in (-1,1):
                soft("hardware_screw",(.022,.008,.022),(x,side*.238,.297),"brass",.006)
    if len(parts) > 96 or len(part_tiles) != len(parts):
        raise RuntimeError("Unexpected semantic part inventory")
    part_proofs = [part_uv_evidence(part,part_tiles[part]) for part in parts]
    expected_histogram = {str(tile):0 for tile in sorted(TILES.values())}
    for proof in part_proofs:
        expected_histogram[str(proof["tile"])] += proof["loops"]
    obj = B.join(parts,ROOT_NAME+"_LOD"+str(lod))
    joined_proof = joined_uv_evidence(obj,expected_histogram)
    X.link_only(obj,coll)
    return obj, {"name":obj.name,"parts":part_proofs,"joined":joined_proof}


def mesh_evidence(obj, budget):
    mesh = obj.data
    mesh.calc_loop_triangles()
    if (any(abs(v)>1e-6 for v in obj.location) or any(abs(v)>1e-6 for v in obj.rotation_euler)
            or any(abs(v-1)>1e-6 for v in obj.scale)):
        raise RuntimeError("Origin/applied transform contract failed")
    coords = [obj.matrix_world @ v.co for v in mesh.vertices]
    lo = [min(v[a] for v in coords) for a in range(3)]
    hi = [max(v[a] for v in coords) for a in range(3)]
    if any(not math.isfinite(c) for v in coords for c in v):
        raise RuntimeError("Nonfinite mesh")
    if any(abs(a-b)>1e-6 for a,b in zip(lo,(-.45,-.25,0))) or any(abs(a-b)>1e-6 for a,b in zip(hi,(.45,.25,.5))):
        raise RuntimeError("Unexpected exact envelope: "+repr((lo,hi)))
    if len(mesh.loop_triangles)>budget or any(len(p.vertices)>4 for p in mesh.polygons):
        raise RuntimeError("Topology/budget refusal")
    uv_proof = joined_uv_evidence(obj)
    uv = checked_uv_layer(mesh)
    if len(mesh.materials)!=1 or mesh.materials[0].name!="bb_mat_flightcase_atlas":
        raise RuntimeError("Unexpected material")
    return {"name":obj.name,"vertices":len(mesh.vertices),"polygons":len(mesh.polygons),
            "triangles":len(mesh.loop_triangles),"budgetTriangles":budget,"boundsBlenderXYZ":{"min":lo,"max":hi},
            "unitySizeXYZ":[hi[0]-lo[0],hi[2]-lo[2],hi[1]-lo[1]],"uvLoops":len(uv.uv),
            "uvMin":[min(v.vector[a] for v in uv.uv) for a in range(2)],
            "uvMax":[max(v.vector[a] for v in uv.uv) for a in range(2)],"ngons":0,
            "uvGuard":uv_proof,
            "scale":list(obj.scale),"rotationEuler":list(obj.rotation_euler),"origin":list(obj.location),
            "material":"bb_mat_flightcase_atlas"}

def main():
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args) != 1:
        raise RuntimeError("One absent external output root required")
    output = Path(os.path.abspath(args[0]))
    for item in (output, *output.parents):
        if item.is_symlink() or (item.exists() and getattr(item.stat(), "st_file_attributes", 0) & 1024):
            raise RuntimeError("Reparse output ancestry refused")
    if output.exists():
        raise RuntimeError("Do not overwrite an existing export root")
    tools = Path(__file__).resolve().parent.parent / "tools"
    sys.path.insert(0, str(tools))
    import bb_build as B
    import bb_export as X
    output.mkdir()
    models = output / "SetPieces"
    textures = output / "Textures" / "FlightCase"
    models.mkdir()
    textures.mkdir(parents=True)
    X.fresh_scene()
    coll = X.collection(ROOT_NAME)
    images = make_maps(textures)
    mat = bpy.data.materials.new("bb_mat_flightcase_atlas")
    mat.use_nodes = True
    # Intentionally named-only: Unity maps the persistent URP atlas by material name.
    nodes = mat.node_tree.nodes
    if sorted(n.bl_idname for n in nodes) != ["ShaderNodeBsdfPrincipled", "ShaderNodeOutputMaterial"]:
        raise RuntimeError("Fresh neutral material graph required")
    built = [build_lod(B, X, coll, mat, lod) for lod in (0, 1)]
    root = bpy.data.objects.new(ROOT_NAME, None)
    coll.objects.link(root)
    for obj, _ in built:
        obj.parent = root
    proofs = [mesh_evidence(obj, cap) for (obj, _), cap in zip(built, (14000, 6000))]
    if [p["triangles"] for p in proofs] != [11792, 2648]:
        raise RuntimeError("Original authored triangle counts changed")
    problems = X.check_collection(coll)
    if problems:
        raise X.ExportError("\n".join(problems))
    props = bpy.ops.export_scene.fbx.get_rna_type().properties
    if props["use_space_transform"].default is not True or props["global_scale"].default != 1.0:
        raise RuntimeError("Original exporter defaults changed")
    fbx = models / (ROOT_NAME + ".fbx")
    check_absent(fbx)
    # The house's one export call (-Z forward, Y up, transform baked), as ArtSource/README asks of
    # every asset script; the earlier per-asset identity axes (Y forward, Z up) reached the same
    # orientation in Unity by another route, through a basis no other set piece uses.
    X.export_collection(coll, str(fbx))
    if sorted(p.name for p in models.iterdir()) != [ROOT_NAME + ".fbx"]:
        raise RuntimeError("Unexpected model dependency")
    if sorted(p.name for p in textures.iterdir()) != sorted("bb_flightcase_"+kind+".png" for kind in MAP_NAMES):
        raise RuntimeError("Unexpected atlas output")
    pins = []
    for path in [fbx, *(textures / ("bb_flightcase_"+k+".png") for k in MAP_NAMES)]:
        with path.open("rb") as stream:
            length = os.fstat(stream.fileno()).st_size
            if length <= 0 or length > 2*1024*1024:
                raise RuntimeError("Controlled output exceeds 2 MiB")
            data = stream.read(length)
            if len(data) != length or stream.read(1) or os.fstat(stream.fileno()).st_size != length:
                raise RuntimeError("Controlled output changed during bounded read")
        pins.append({"name":str(path.relative_to(output)), "bytes":length,
                     "sha256":hashlib.sha256(data).hexdigest()})
    write_json(output / "source-export.json", {
        "sourceOnly": True, "unityAccepted": False, "visualAccepted": False,
        "axisForward": "-Z", "axisUp": "Y", "globalExporterChanged": False,
        "meshes": proofs, "parts": [proof for _, proof in built],
        "outputs": pins})


if __name__ == "__main__":
    main()

"""Poly Haven (CC0) furniture and surfaces into the pipeline (VISUAL-TARGET.md V4).

Fetch a model by id from api.polyhaven.com, import its 1k glTF, join it into one mesh, cut it to
the triangle budget, put its origin on the floor under its centre, name it bb_set_<piece>, write
its textures in URP's layout, and export it through the same checklist every authored piece passes.

    blender --background --python ArtSource/tools/bb_polyhaven.py -- <asset_id> <piece> [--tris 4000]

or, from a per-piece script in ArtSource/setpieces (the script is the source, as for every piece):

    import bb_polyhaven as P
    P.build("Sofa_01", "ph_sofa", tris=4000)

A *texture* set - a surface rather than a thing - takes the same route without a mesh:

    blender --background --python ArtSource/tools/bb_polyhaven.py -- --texture wood_floor ph_oak

or P.texture_set("wood_floor", "ph_oak"), which is what ArtSource/textures/bb_tex_polyhaven.py
drives for the floors and the walls.

Outputs, relative to the project:
    Assets/Gamesim/Art/Authored/SetPieces/bb_set_<piece>.fbx
    Assets/Gamesim/Art/Authored/Textures/PolyHaven/bb_tex_<piece>_{albedo,normal,metallic,occlusion}.png
    Assets/Gamesim/Art/Authored/Textures/bb_tex_<name>_{albedo,normal,metallic,occlusion}.png  (a texture set)

A piece's maps sit in the PolyHaven folder, which is the list the editor's material builder and
PolyHavenPieceTests walk; a surface's maps sit beside the baked floors in Textures, which is the
layout HouseFloorDressing reads. Both import by suffix (AuthoredAssetImporter): albedo sRGB,
normal a normal map, metallic and occlusion linear data.

The FBX names its material bb_mat_<piece> and carries no images: the editor builds the URP material
from the four textures (Gamesim/U07/Build the Poly Haven materials) and the importer matches it by
name, so a re-export keeps the material the scene already has. Downloads are cached under
ArtSource/polyhaven/<asset_id>/ (ignored by Git; the id is the source of record).
"""
import json
import math
import os
import sys
import urllib.request

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_export as X  # noqa: E402

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
CACHE = os.path.join(PROJECT, "ArtSource", "polyhaven")
SETPIECES = os.path.join(PROJECT, "Assets", "Gamesim", "Art", "Authored", "SetPieces")
SURFACES = os.path.join(PROJECT, "Assets", "Gamesim", "Art", "Authored", "Textures")
TEXTURES = os.path.join(SURFACES, "PolyHaven")
API = "https://api.polyhaven.com"
USER_AGENT = "gamesim-bb-polyhaven/1.0 (CC0 asset fetch for a Unity project)"
DEFAULT_TRIS = 4000
RESOLUTION = "1k"
# A texture set arrives as three files: the colour, the OpenGL tangent normal, and AO, roughness
# and metalness packed into one image's red, green and blue.
TEXTURE_FILES = (("Diffuse", "albedo"), ("nor_gl", "normal"), ("arm", "arm"))


def _get(url, binary=False):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        data = response.read()
    return data if binary else json.loads(data.decode("utf-8"))


def _download(url, path):
    """One file into the cache, once; returns its path."""
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(_get(url, binary=True))
    print("fetched", os.path.relpath(path, PROJECT))
    return path


def _record_info(asset_id, folder):
    """The asset's own page as JSON beside its files: the licence, the author, the real size."""
    info = _get(API + "/info/" + asset_id)
    with open(os.path.join(folder, "info.json"), "w", encoding="utf-8") as handle:
        json.dump(info, handle, indent=2)
    return info


def fetch(asset_id, resolution=RESOLUTION):
    """The asset's glTF and everything it includes, cached; returns the .gltf path."""
    folder = os.path.join(CACHE, asset_id)
    os.makedirs(folder, exist_ok=True)
    listing = _get(API + "/files/" + asset_id)
    entry = listing["gltf"][resolution]["gltf"]
    gltf_path = os.path.join(folder, os.path.basename(entry["url"]))
    files = [(entry["url"], gltf_path)]
    for relative, included in (entry.get("include") or {}).items():
        files.append((included["url"], os.path.join(folder, *relative.split("/"))))
    for url, path in files:
        _download(url, path)
    _record_info(asset_id, folder)
    return gltf_path


def fetch_texture(asset_id, resolution=RESOLUTION):
    """A texture set's colour, normal and packed ARM at <resolution>, cached; paths by role.

    PNG rather than JPEG, because the packed map's three channels are read apart and a JPEG's
    blocks bleed between them; what Poly Haven ships is sixteen bits a channel, which the writer
    below brings down to the eight the set's materials use."""
    folder = os.path.join(CACHE, asset_id)
    os.makedirs(folder, exist_ok=True)
    listing = _get(API + "/files/" + asset_id)
    paths = {}
    for key, role in TEXTURE_FILES:
        entry = ((listing.get(key) or {}).get(resolution) or {}).get("png")
        if entry is None:
            continue
        paths[role] = _download(entry["url"], os.path.join(folder, os.path.basename(entry["url"])))
    if "albedo" not in paths:
        raise RuntimeError(asset_id + " has no " + resolution + " colour map")
    _record_info(asset_id, folder)
    return paths


def _load_raw(path):
    """An image whose pixels are the numbers in the file - no colour management either way."""
    image = bpy.data.images.load(path, check_existing=False)
    image.colorspace_settings.name = 'Non-Color'
    return image


def texture_set(asset_id, name, folder=SURFACES, resolution=RESOLUTION):
    """A Poly Haven surface as the four maps a floor or a wall wears; returns the written paths.

    The colour and the normal are copied through untouched - the bytes in Poly Haven's file are
    already what Unity's importer expects of <c>_albedo</c> (sRGB) and <c>_normal</c> (an OpenGL
    tangent normal), so nothing is decoded and re-encoded and no gamma is applied twice. Only the
    packed map is taken apart, into the metallic-with-smoothness-in-alpha and occlusion pair the
    URP Lit shader reads."""
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    sources = fetch_texture(asset_id, resolution=resolution)
    prefix = "bb_tex_" + name
    written = []
    for role in ("albedo", "normal"):
        if role not in sources:
            continue
        image = _load_raw(sources[role])
        written.append(_write_png(prefix + "_" + role, _pixels(image), folder))
        bpy.data.images.remove(image)
    if "arm" in sources:
        image = _load_raw(sources["arm"])
        arm = _pixels(image)
        bpy.data.images.remove(image)
        occlusion, roughness, metallic = arm[..., 0], arm[..., 1], arm[..., 2]
        written.append(_write_png(prefix + "_metallic",
                                  np.stack([metallic, metallic, metallic, 1.0 - roughness], axis=-1), folder))
        written.append(_write_png(prefix + "_occlusion",
                                  np.stack([occlusion, occlusion, occlusion, np.ones_like(occlusion)], axis=-1), folder))
    with open(os.path.join(CACHE, asset_id, "surface.json"), "w", encoding="utf-8") as handle:
        json.dump({"asset": asset_id, "surface": name,
                   "textures": [os.path.relpath(t, PROJECT) for t in written]}, handle, indent=2)
    print("POLYHAVEN %s -> %s (%d maps)" % (asset_id, prefix, len(written)))
    return written


def import_model(gltf_path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=gltf_path)
    imported = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in imported if o.type == 'MESH']
    if not meshes:
        raise RuntimeError("no meshes in " + gltf_path)
    # The glTF's empties carry scale and placement; a mesh unparented from one keeps its world
    # matrix, or a 1.2 m table arrives 30 cm wide.
    for obj in meshes:
        if obj.parent is not None:
            world = obj.matrix_world.copy()
            obj.parent = None
            obj.matrix_world = world
    for obj in imported:
        if obj.type != 'MESH':
            bpy.data.objects.remove(obj)
    return meshes


def merge(meshes, name, coll, yaw=0.0):
    """One mesh, transforms applied, origin on the floor under the footprint's centre.

    <c>yaw</c> turns the piece about its up axis first, in degrees, before anything is applied: a
    scanned chair arrives facing whichever way it was scanned, and every authored piece in this set
    faces +y with its back at -y, which is what the placement's yaw column assumes."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if yaw:
        # The mesh data, not the object: a glTF's mesh can arrive with more than one user, and an
        # object-level transform_apply on shared data reports FINISHED and changes nothing.
        from mathutils import Matrix
        obj.data.transform(Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    seat(obj)
    X.link_only(obj, coll)
    bpy.ops.object.select_all(action='DESELECT')
    return obj


def seat(obj):
    """Put the mesh's origin on the floor under its footprint's centre, where the checklist wants it.

    Called again after the decimation, because collapsing an edge puts the vertex that replaces it
    where the error is least, which is not always between the two: a plant cut to a tenth of its
    scan came out standing 16 cm under the floor."""
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    shift = ((min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0, min(zs))
    for v in obj.data.vertices:
        v.co.x -= shift[0]
        v.co.y -= shift[1]
        v.co.z -= shift[2]
    obj.location = (0.0, 0.0, 0.0)
    return obj


def decimate(obj, tris):
    """Collapse to the budget, triangulated, so the checklist's n-gon rule holds after."""
    current = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if current > tris:
        modifier = obj.modifiers.new("budget", 'DECIMATE')
        modifier.decimate_type = 'COLLAPSE'
        modifier.ratio = tris / float(current)
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if any(len(p.vertices) > 4 for p in obj.data.polygons):
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY', ngon_method='BEAUTY')
        bpy.ops.object.mode_set(mode='OBJECT')
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def _image_inputs(material):
    """The glTF material's images by role: base colour, normal, and the packed AO/rough/metal."""
    roles = {"albedo": None, "normal": None, "arm": None}
    tree = material.node_tree
    principled = next((n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if principled is None:
        return roles

    def upstream_image(socket):
        seen = set()
        stack = [socket]
        while stack:
            current = stack.pop()
            for link in current.links:
                node = link.from_node
                if node in seen:
                    continue
                seen.add(node)
                if node.type == 'TEX_IMAGE' and node.image is not None:
                    return node.image
                stack.extend(node.inputs)
        return None

    roles["albedo"] = upstream_image(principled.inputs["Base Color"])
    roles["normal"] = upstream_image(principled.inputs["Normal"])
    roles["arm"] = upstream_image(principled.inputs["Roughness"]) or upstream_image(principled.inputs["Metallic"])
    return roles


def _pixels(image):
    import numpy as np
    width, height = image.size
    data = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(data)
    return data.reshape(height, width, 4)


def _write_png(name, rgba, folder):
    import numpy as np
    height, width = rgba.shape[:2]
    image = bpy.data.images.new(name, width=width, height=height, alpha=True, float_buffer=False)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    image.filepath_raw = os.path.join(folder, name + ".png")
    image.file_format = 'PNG'
    image.save()
    path = image.filepath_raw
    bpy.data.images.remove(image)
    return path


NEUTRAL = {"albedo": (1.0, 1.0, 1.0, 1.0), "normal": (0.5, 0.5, 1.0, 1.0), "arm": (1.0, 0.5, 0.0, 1.0)}


def _band(role, size):
    """What a material without this map contributes to the atlas: white, flat, or unoccluded."""
    import numpy as np
    width, height = size
    return np.tile(np.array(NEUTRAL[role], dtype=np.float32), (height, width, 1))


def piece_maps(obj, materials):
    """The piece's three source maps as arrays, whether it arrived with one material or several.

    A scanned plant is two materials - the pot and the leaves - with an image set each and the same
    0..1 UV square twice over, and folding them into one material paints the leaves in terracotta.
    So the maps are stacked instead, one band a material, bottom band first, and every face's UVs
    are squeezed into its own band: one image, one material, nothing repainted."""
    roles = [_image_inputs(material) for material in materials]
    if len(materials) == 1:
        return dict((role, None if image is None else _pixels(image)) for role, image in roles[0].items())
    return atlas(obj, roles)


def atlas(obj, roles):
    """Stack each role's images into bands and move the UVs to match; returns the stacked arrays."""
    import numpy as np
    layer = obj.data.uv_layers.active
    if layer is None:
        raise RuntimeError("a piece with several materials needs UVs to atlas")
    stray = [uv for uv in (loop.uv for loop in layer.data) if not (-0.01 <= uv.x <= 1.01 and -0.01 <= uv.y <= 1.01)]
    if stray:
        raise RuntimeError("UVs run outside the 0..1 square, so the materials cannot share one map")

    count = len(roles)
    maps = {}
    for role in ("albedo", "normal", "arm"):
        images = [role_set[role] for role_set in roles]
        present = [image for image in images if image is not None]
        if not present:
            continue
        size = tuple(present[0].size)
        if any(tuple(image.size) != size for image in present):
            raise RuntimeError("the materials' " + role + " maps are different sizes")
        bands = [_band(role, size) if image is None else _pixels(image) for image in images]
        maps[role] = np.concatenate(bands, axis=0)

    for poly in obj.data.polygons:
        band = min(poly.material_index, count - 1)
        for loop in poly.loop_indices:
            uv = layer.data[loop].uv
            uv.y = (uv.y + band) / count
    return maps


def write_textures(maps, piece, folder=TEXTURES):
    """URP's layout: albedo (sRGB), normal (OpenGL, as Poly Haven's nor_gl), metallic with smoothness
    in alpha (linear), occlusion (linear). Returns the written paths."""
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    roles = maps
    written = []
    prefix = "bb_tex_" + piece
    if roles.get("albedo") is not None:
        written.append(_write_png(prefix + "_albedo", roles["albedo"], folder))
    if roles.get("normal") is not None:
        written.append(_write_png(prefix + "_normal", roles["normal"], folder))
    if roles.get("arm") is not None:
        arm = roles["arm"]
        occlusion, roughness, metallic = arm[..., 0], arm[..., 1], arm[..., 2]
        metallic_map = np.stack([metallic, metallic, metallic, 1.0 - roughness], axis=-1)
        occlusion_map = np.stack([occlusion, occlusion, occlusion, np.ones_like(occlusion)], axis=-1)
        written.append(_write_png(prefix + "_metallic", metallic_map, folder))
        written.append(_write_png(prefix + "_occlusion", occlusion_map, folder))
    return written


def strip_images(material):
    """The FBX carries the material's name and nothing else; the editor owns the textures."""
    tree = material.node_tree
    for node in list(tree.nodes):
        if node.type in ('TEX_IMAGE', 'NORMAL_MAP', 'SEPARATE_COLOR', 'SEPRGB', 'MAPPING', 'TEX_COORD'):
            tree.nodes.remove(node)


def build(asset_id, piece, tris=DEFAULT_TRIS, out=None, fresh=False, yaw=0.0):
    """The whole route for one piece; returns (fbx path, texture paths, triangle count)."""
    if fresh:
        X.fresh_scene()
    name = "bb_set_" + piece
    gltf_path = fetch(asset_id)
    coll = X.collection(name)
    obj = merge(import_model(gltf_path), name, coll, yaw=yaw)
    count = decimate(obj, tris)
    seat(obj)
    materials = [slot.material for slot in obj.material_slots if slot.material]
    if not materials:
        raise RuntimeError(asset_id + " imported without a material")
    # One material a piece, and one map set: several materials are stacked into bands first, so
    # the pot keeps its glaze and the leaves keep their green.
    textures = write_textures(piece_maps(obj, materials), piece)
    material = materials[0]
    for slot in obj.material_slots:
        slot.material = material
    strip_images(material)
    material.name = "bb_mat_" + piece
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for poly in obj.data.polygons:
        poly.material_index = 0
    out = out or os.path.join(SETPIECES, name + ".fbx")
    X.export_collection(coll, out)
    X.report(coll, out)
    with open(os.path.join(CACHE, asset_id, "piece.json"), "w", encoding="utf-8") as handle:
        json.dump({"asset": asset_id, "piece": piece, "tris": count, "textures": [os.path.relpath(t, PROJECT) for t in textures]}, handle, indent=2)
    print("POLYHAVEN %s -> %s (%d tris, %d textures)" % (asset_id, os.path.relpath(out, PROJECT), count, len(textures)))
    return out, textures, count


def _main(argv):
    args = [a for a in argv if not a.startswith("--")]
    tris = DEFAULT_TRIS
    if "--tris" in argv:
        tris = int(argv[argv.index("--tris") + 1])
        args = [a for a in args if a != str(tris)]
    yaw = 0.0
    if "--yaw" in argv:
        yaw = float(argv[argv.index("--yaw") + 1])
        args = [a for a in args if a != str(yaw)]
    if len(args) < 2:
        raise SystemExit(__doc__)
    if "--texture" in argv:
        texture_set(args[0], args[1])
    else:
        build(args[0], args[1], tris=tris, fresh=True, yaw=yaw)


if __name__ == "__main__":
    _main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

"""Poly Haven (CC0) furniture into the pipeline (VISUAL-TARGET.md V4).

Fetch a model by id from api.polyhaven.com, import its 1k glTF, join it into one mesh, cut it to
the triangle budget, put its origin on the floor under its centre, name it bb_set_<piece>, write
its textures in URP's layout, and export it through the same checklist every authored piece passes.

    blender --background --python ArtSource/tools/bb_polyhaven.py -- <asset_id> <piece> [--tris 4000]

or, from a per-piece script in ArtSource/setpieces (the script is the source, as for every piece):

    import bb_polyhaven as P
    P.build("Sofa_01", "ph_sofa", tris=4000)

Outputs, relative to the project:
    Assets/Gamesim/Art/Authored/SetPieces/bb_set_<piece>.fbx
    Assets/Gamesim/Art/Authored/Textures/PolyHaven/bb_tex_<piece>_{albedo,normal,metallic,occlusion}.png

The FBX names its material bb_mat_<piece> and carries no images: the editor builds the URP material
from the four textures (Gamesim/U07/Build the Poly Haven materials) and the importer matches it by
name, so a re-export keeps the material the scene already has. Downloads are cached under
ArtSource/polyhaven/<asset_id>/ (ignored by Git; the id is the source of record).
"""
import json
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
TEXTURES = os.path.join(PROJECT, "Assets", "Gamesim", "Art", "Authored", "Textures", "PolyHaven")
API = "https://api.polyhaven.com"
USER_AGENT = "gamesim-bb-polyhaven/1.0 (CC0 asset fetch for a Unity project)"
DEFAULT_TRIS = 4000
RESOLUTION = "1k"


def _get(url, binary=False):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        data = response.read()
    return data if binary else json.loads(data.decode("utf-8"))


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
        if os.path.exists(path) and os.path.getsize(path) > 0:
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as handle:
            handle.write(_get(url, binary=True))
        print("fetched", os.path.relpath(path, PROJECT))
    with open(os.path.join(folder, "info.json"), "w", encoding="utf-8") as handle:
        json.dump(_get(API + "/info/" + asset_id), handle, indent=2)
    return gltf_path


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


def merge(meshes, name, coll):
    """One mesh, transforms applied, origin on the floor under the footprint's centre."""
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
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    shift = ((min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0, min(zs))
    for v in obj.data.vertices:
        v.co.x -= shift[0]
        v.co.y -= shift[1]
        v.co.z -= shift[2]
    obj.location = (0.0, 0.0, 0.0)
    X.link_only(obj, coll)
    bpy.ops.object.select_all(action='DESELECT')
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


def write_textures(material, piece, folder=TEXTURES):
    """URP's layout: albedo (sRGB), normal (OpenGL, as Poly Haven's nor_gl), metallic with smoothness
    in alpha (linear), occlusion (linear). Returns the written paths."""
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    roles = _image_inputs(material)
    written = []
    prefix = "bb_tex_" + piece
    if roles["albedo"] is not None:
        written.append(_write_png(prefix + "_albedo", _pixels(roles["albedo"]), folder))
    if roles["normal"] is not None:
        written.append(_write_png(prefix + "_normal", _pixels(roles["normal"]), folder))
    if roles["arm"] is not None:
        arm = _pixels(roles["arm"])
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


def build(asset_id, piece, tris=DEFAULT_TRIS, out=None, fresh=False):
    """The whole route for one piece; returns (fbx path, texture paths, triangle count)."""
    if fresh:
        X.fresh_scene()
    name = "bb_set_" + piece
    gltf_path = fetch(asset_id)
    coll = X.collection(name)
    obj = merge(import_model(gltf_path), name, coll)
    count = decimate(obj, tris)
    materials = [slot.material for slot in obj.material_slots if slot.material]
    if not materials:
        raise RuntimeError(asset_id + " imported without a material")
    if len(materials) > 1:
        # One material a piece: the first is kept and the rest fold into it.
        for slot in obj.material_slots:
            slot.material = materials[0]
    material = materials[0]
    textures = write_textures(material, piece)
    strip_images(material)
    material.name = "bb_mat_" + piece
    obj.data.materials.clear()
    obj.data.materials.append(material)
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
    if len(args) < 2:
        raise SystemExit(__doc__)
    build(args[0], args[1], tris=tris, fresh=True)


if __name__ == "__main__":
    _main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

"""Bakes a procedural Blender material to the texture set Unity's URP/Lit expects.

A surface script describes a material with Blender's procedural nodes - bricks, waves, noise -
and this bakes it, on a unit plane with a 0..1 UV square, to two power-of-two PNGs:

    <name>_albedo.png   the base colour (sRGB), from a Diffuse bake with only the colour pass
    <name>_normal.png   the tangent-space normal (linear), from a Normal bake

Cycles does the baking on the CPU, so it runs headless. Tileability comes from the surface,
not from here: bricks and waves repeat exactly when their scale is a whole number of cells per
UV square, and a surface that wants noise should mix it in below the level a seam can read at.

    import bb_bake
    bb_bake.bake(build_material, "bb_tex_plank_oak", size=1024, out_dir=...)

where build_material(node_tree, bsdf) wires the tree and returns the node feeding the BSDF's
Normal input (or None for a flat surface).
"""
import os

import bpy


def _reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.render.bake.target = 'IMAGE_TEXTURES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = False
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = 4
    return scene


def _plane():
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.0, 0.0, 0.0))
    plane = bpy.context.active_object
    plane.name = "bake_plane"
    # A single UV square, exactly 0..1: the plane primitive already carries one.
    return plane


def _image(name, size, srgb):
    image = bpy.data.images.new(name, width=size, height=size)
    image.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    return image


def _bake_into(plane, material, image, bake_type, out_path, pass_filter=None):
    # Cycles bakes into the active, selected image node of the active object's material; every
    # one of those has to be set explicitly, and the object selection has to be exact.
    nodes = material.node_tree.nodes
    target = nodes.new('ShaderNodeTexImage')
    target.image = image
    for node in nodes:
        node.select = False
    target.select = True
    nodes.active = target
    bpy.ops.object.select_all(action='DESELECT')
    plane.select_set(True)
    bpy.context.view_layer.objects.active = plane
    kwargs = {'type': bake_type, 'margin': 4, 'use_clear': True}
    if pass_filter:
        kwargs['pass_filter'] = pass_filter
    bpy.ops.object.bake(**kwargs)
    image.filepath_raw = out_path
    image.file_format = 'PNG'
    image.save()
    nodes.remove(target)


def bake(build_material, name, size=1024, out_dir=None):
    """Bakes the surface built by build_material to <out_dir>/<name>_albedo.png and _normal.png."""
    _reset()
    plane = _plane()
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    build_material(tree, bsdf)
    plane.data.materials.append(material)
    bpy.context.view_layer.objects.active = plane
    plane.select_set(True)

    # Absolute on purpose: Blender resolves a relative image path against its own notion of a
    # working directory, not the shell's, and a bake once landed beside the executable.
    out_dir = os.path.abspath(out_dir or os.getcwd())
    os.makedirs(out_dir, exist_ok=True)
    albedo = os.path.join(out_dir, name + "_albedo.png")
    normal = os.path.join(out_dir, name + "_normal.png")
    _bake_into(plane, material, _image(name + "_albedo", size, True), 'DIFFUSE', albedo, {'COLOR'})
    _bake_into(plane, material, _image(name + "_normal", size, False), 'NORMAL', normal)
    print("BAKED %s: %s, %s (%d px)" % (name, albedo, normal, size))
    return albedo, normal


def output_dir(default_subdir):
    """The output directory: the argument after '--', or a default under Assets."""
    import sys
    if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
        return sys.argv[sys.argv.index("--") + 1]
    return default_subdir

"""The wall texture: dark plaster with a faint trowel, for the shell's wall slab.

One 2 m tile at 1024 px, albedo plus tangent normal. The shell's meshes carry world-scale box
UVs, so the tile is two metres on every wall. The base is the darkened broadcast wall tone the
slab has always had; the texture adds the mottle and the relief that stop a 28 m wall reading as
one flat card, and nothing louder - the walls are the backdrop the neon outlines sit on.

    blender --background --python ArtSource/textures/bb_tex_walls.py -- <output directory>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_bake  # noqa: E402


def plaster(tree, bsdf):
    nodes, links = tree.nodes, tree.links
    coords = nodes.new('ShaderNodeTexCoord')
    mottle = nodes.new('ShaderNodeTexNoise')
    mottle.inputs['Scale'].default_value = 5.0
    mottle.inputs['Detail'].default_value = 6.0
    mottle.inputs['Roughness'].default_value = 0.55
    links.new(coords.outputs['UV'], mottle.inputs['Vector'])
    trowel = nodes.new('ShaderNodeTexNoise')
    trowel.inputs['Scale'].default_value = 40.0
    trowel.inputs['Detail'].default_value = 2.0
    links.new(coords.outputs['UV'], trowel.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.40, 0.43, 0.47, 1.0)
    ramp.color_ramp.elements[1].color = (0.52, 0.55, 0.58, 1.0)
    links.new(mottle.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.25
    bump.inputs['Distance'].default_value = 0.01
    links.new(trowel.outputs['Fac'], bump.inputs['Height'])
    bump_wide = nodes.new('ShaderNodeBump')
    bump_wide.inputs['Strength'].default_value = 0.15
    bump_wide.inputs['Distance'].default_value = 0.02
    links.new(mottle.outputs['Fac'], bump_wide.inputs['Height'])
    links.new(bump.outputs['Normal'], bump_wide.inputs['Normal'])
    links.new(bump_wide.outputs['Normal'], bsdf.inputs['Normal'])
    return bump_wide


if __name__ == "__main__":
    out = bb_bake.output_dir(os.path.join(HERE, "..", "..", "Assets", "Gamesim", "Art", "Authored", "Textures"))
    bb_bake.bake(plaster, "bb_tex_plaster", size=1024, out_dir=out)

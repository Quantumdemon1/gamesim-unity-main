"""The floor textures: oak planks, walnut planks, slate tiles and lawn, baked from procedural surfaces.

Each is one 2 m tile (the lawn, 4 m) at 1024 px, albedo plus tangent normal, laid by
HouseFloorDressing with the tiling that makes the tile that size on each floor. The plank and
tile patterns are Blender's Brick texture at whole cells per tile, so they repeat exactly; the
grain and the lawn use noise, which does not, mixed in below the level a seam reads at from the
overhead camera.

    blender --background --python ArtSource/textures/bb_tex_floors.py -- <output directory>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_bake  # noqa: E402


def _rgb(r, g, b):
    return (r, g, b, 1.0)


def planks(light, dark, mortar, grain_strength=0.35):
    """Staggered planks, four to the tile across and one long: 0.5 m wide, 2 m long at a 2 m tile."""
    def surface(tree, bsdf):
        nodes, links = tree.nodes, tree.links
        coords = nodes.new('ShaderNodeTexCoord')
        brick = nodes.new('ShaderNodeTexBrick')
        brick.offset = 0.5
        brick.offset_frequency = 2
        brick.squash = 1.0
        brick.inputs['Scale'].default_value = 1.0
        brick.inputs['Color1'].default_value = light
        brick.inputs['Color2'].default_value = dark
        brick.inputs['Mortar'].default_value = mortar
        brick.inputs['Mortar Size'].default_value = 0.006
        brick.inputs['Mortar Smooth'].default_value = 0.3
        brick.inputs['Bias'].default_value = 0.0
        brick.inputs['Brick Width'].default_value = 1.0
        brick.inputs['Row Height'].default_value = 0.125
        links.new(coords.outputs['UV'], brick.inputs['Vector'])

        # Grain: bands along the plank, distorted a little.
        mapping = nodes.new('ShaderNodeMapping')
        mapping.inputs['Scale'].default_value = (1.0, 40.0, 1.0)
        links.new(coords.outputs['UV'], mapping.inputs['Vector'])
        wave = nodes.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = 'Y'
        wave.inputs['Scale'].default_value = 1.0
        wave.inputs['Distortion'].default_value = 2.5
        wave.inputs['Detail'].default_value = 2.0
        wave.inputs['Detail Scale'].default_value = 3.0
        links.new(mapping.outputs['Vector'], wave.inputs['Vector'])
        grain = nodes.new('ShaderNodeMix')
        grain.data_type = 'RGBA'
        grain.blend_type = 'MULTIPLY'
        grain.inputs['Factor'].default_value = grain_strength
        links.new(brick.outputs['Color'], grain.inputs[6])
        ramp = nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (0.72, 0.72, 0.72, 1.0)
        ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1.0)
        links.new(wave.outputs['Fac'], ramp.inputs['Fac'])
        links.new(ramp.outputs['Color'], grain.inputs[7])
        links.new(grain.outputs[2], bsdf.inputs['Base Color'])
        bsdf.inputs['Roughness'].default_value = 0.55

        # Relief: the mortar gap is a step down; the grain is a whisper.
        bump_gap = nodes.new('ShaderNodeBump')
        bump_gap.inputs['Strength'].default_value = 0.6
        bump_gap.inputs['Distance'].default_value = 0.02
        links.new(brick.outputs['Fac'], bump_gap.inputs['Height'])
        bump_grain = nodes.new('ShaderNodeBump')
        bump_grain.inputs['Strength'].default_value = 0.08
        bump_grain.inputs['Distance'].default_value = 0.01
        links.new(wave.outputs['Fac'], bump_grain.inputs['Height'])
        links.new(bump_gap.outputs['Normal'], bump_grain.inputs['Normal'])
        links.new(bump_grain.outputs['Normal'], bsdf.inputs['Normal'])
        return bump_grain
    return surface


def tiles(light, dark, grout):
    """Square tiles, four by four to the tile: 0.5 m slate at a 2 m tile."""
    def surface(tree, bsdf):
        nodes, links = tree.nodes, tree.links
        coords = nodes.new('ShaderNodeTexCoord')
        brick = nodes.new('ShaderNodeTexBrick')
        brick.offset = 0.0
        brick.offset_frequency = 1
        brick.inputs['Scale'].default_value = 1.0
        brick.inputs['Color1'].default_value = light
        brick.inputs['Color2'].default_value = dark
        brick.inputs['Mortar'].default_value = grout
        brick.inputs['Mortar Size'].default_value = 0.008
        brick.inputs['Mortar Smooth'].default_value = 0.4
        brick.inputs['Brick Width'].default_value = 0.25
        brick.inputs['Row Height'].default_value = 0.25
        links.new(coords.outputs['UV'], brick.inputs['Vector'])
        noise = nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 18.0
        noise.inputs['Detail'].default_value = 4.0
        noise.inputs['Roughness'].default_value = 0.6
        links.new(coords.outputs['UV'], noise.inputs['Vector'])
        mottle = nodes.new('ShaderNodeMix')
        mottle.data_type = 'RGBA'
        mottle.blend_type = 'MULTIPLY'
        mottle.inputs['Factor'].default_value = 0.25
        links.new(brick.outputs['Color'], mottle.inputs[6])
        ramp = nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (0.8, 0.8, 0.8, 1.0)
        ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1.0)
        links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
        links.new(ramp.outputs['Color'], mottle.inputs[7])
        links.new(mottle.outputs[2], bsdf.inputs['Base Color'])
        bsdf.inputs['Roughness'].default_value = 0.35
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.5
        bump.inputs['Distance'].default_value = 0.02
        links.new(brick.outputs['Fac'], bump.inputs['Height'])
        surface_bump = nodes.new('ShaderNodeBump')
        surface_bump.inputs['Strength'].default_value = 0.12
        surface_bump.inputs['Distance'].default_value = 0.01
        links.new(noise.outputs['Fac'], surface_bump.inputs['Height'])
        links.new(bump.outputs['Normal'], surface_bump.inputs['Normal'])
        links.new(surface_bump.outputs['Normal'], bsdf.inputs['Normal'])
        return surface_bump
    return surface


def lawn(deep, bright):
    """Two greens through layered noise: clumps and blades. A 4 m tile keeps the repeat quiet."""
    def surface(tree, bsdf):
        nodes, links = tree.nodes, tree.links
        coords = nodes.new('ShaderNodeTexCoord')
        clumps = nodes.new('ShaderNodeTexNoise')
        clumps.inputs['Scale'].default_value = 6.0
        clumps.inputs['Detail'].default_value = 3.0
        links.new(coords.outputs['UV'], clumps.inputs['Vector'])
        blades = nodes.new('ShaderNodeTexNoise')
        blades.inputs['Scale'].default_value = 90.0
        blades.inputs['Detail'].default_value = 5.0
        blades.inputs['Roughness'].default_value = 0.7
        links.new(coords.outputs['UV'], blades.inputs['Vector'])
        mix_fac = nodes.new('ShaderNodeMath')
        mix_fac.operation = 'MULTIPLY_ADD'
        mix_fac.inputs[1].default_value = 0.7
        mix_fac.inputs[2].default_value = 0.0
        links.new(clumps.outputs['Fac'], mix_fac.inputs[0])
        fine = nodes.new('ShaderNodeMath')
        fine.operation = 'MULTIPLY_ADD'
        fine.inputs[1].default_value = 0.3
        links.new(blades.outputs['Fac'], fine.inputs[0])
        links.new(mix_fac.outputs[0], fine.inputs[2])
        colour = nodes.new('ShaderNodeMix')
        colour.data_type = 'RGBA'
        colour.inputs[6].default_value = deep
        colour.inputs[7].default_value = bright
        links.new(fine.outputs[0], colour.inputs['Factor'])
        links.new(colour.outputs[2], bsdf.inputs['Base Color'])
        bsdf.inputs['Roughness'].default_value = 0.9
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.35
        bump.inputs['Distance'].default_value = 0.02
        links.new(blades.outputs['Fac'], bump.inputs['Height'])
        links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        return bump
    return surface


SURFACES = {
    "bb_tex_plank_oak": planks(_rgb(0.62, 0.44, 0.26), _rgb(0.50, 0.34, 0.19), _rgb(0.16, 0.11, 0.07)),
    "bb_tex_plank_walnut": planks(_rgb(0.34, 0.22, 0.14), _rgb(0.25, 0.16, 0.10), _rgb(0.08, 0.05, 0.03)),
    "bb_tex_tile_slate": tiles(_rgb(0.36, 0.38, 0.41), _rgb(0.28, 0.30, 0.34), _rgb(0.55, 0.55, 0.53)),
    "bb_tex_lawn": lawn(_rgb(0.10, 0.28, 0.12), _rgb(0.30, 0.52, 0.18)),
}

if __name__ == "__main__":
    out = bb_bake.output_dir(os.path.join(HERE, "..", "..", "Assets", "Gamesim", "Art", "Authored", "Textures"))
    only = [a for a in sys.argv[sys.argv.index("--") + 2:]] if "--" in sys.argv else []
    for name, surface in SURFACES.items():
        if only and name not in only:
            continue
        bb_bake.bake(surface, name, size=1024, out_dir=out)

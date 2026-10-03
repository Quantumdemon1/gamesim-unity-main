"""Original house furnishings, in metres, using the established Gamesim FBX conventions.

blender --background --python ArtSource/tools/bb_house_amenities.py -- <output-directory>
Each export is a separate floor-rooted mesh. Collision and navigation belong to Unity placement.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_build as B
import bb_export as X


def rod(name, radius, a, b, material):
    start, end = Vector(a), Vector(b)
    delta = end - start
    obj = B.cylinder(name, radius, delta.length, (0, 0, 0), material, segments=12)
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(delta.normalized())
    obj.location = start
    return obj


def ball(name, centre, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=.025, location=centre)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def palette():
    return {
        'wood': X.material('amenity_walnut', (.19, .10, .06), roughness=.58),
        'ink': X.material('amenity_ink', (.025, .033, .045), roughness=.48),
        'steel': X.material('amenity_steel', (.29, .34, .40), metallic=.8, roughness=.32),
        'brass': X.material('amenity_brass', (.66, .42, .15), metallic=.75, roughness=.34),
        'felt': X.material('amenity_felt', (.035, .27, .24), roughness=.92),
        'cream': X.material('amenity_cream', (.78, .73, .62), roughness=.88),
        'white': X.material('amenity_ivory', (.86, .86, .80), roughness=.32),
        'blue': X.material('amenity_blue', (.07, .28, .55), roughness=.32),
        'red': X.material('amenity_red', (.65, .08, .06), roughness=.32),
        'lens': X.material('amenity_lens', (.045, .15, .26), metallic=.6, roughness=.18),
        'lamp': X.material('amenity_lamp', (.8, .84, .72), emission=(1, .85, .56), emission_strength=2),
        'tally': X.material('amenity_tally', (.6, .015, .01), emission=(1, .025, .01), emission_strength=2),
    }


def pooltable(m):
    parts = [B.soft_box('cabinet', (2.42, 1.40, .20), (0, 0, .64), m['wood'], radius=.05),
             B.box('bed', (2.20, 1.20, .065), (0, 0, .765), m['felt'])]
    for x in (-.86, .86):
        for y in (-.43, .43):
            parts.append(B.box('leg', (.15, .15, .53), (x, y, .265), m['wood']))
            parts.append(B.cylinder('foot', .095, .025, (x, y, 0), m['steel'], segments=16))
    # Rails stop at the six pockets, rather than drawing an unbroken cushion over them.
    for y in (-.65, .65):
        for x in (-.57, .57):
            parts.append(B.soft_box('long_rail', (1.00, .12, .10), (x, y, .80), m['wood'], radius=.015))
            parts.append(B.box('long_cushion', (1.00, .045, .04), (x, y * .91, .814), m['felt']))
        for x in (-.82, -.54, -.26, .26, .54, .82):
            parts.append(B.disc('sight', .013, (x, y, .852), m['white'], segments=8))
    for x in (-1.15, 1.15):
        parts.append(B.soft_box('end_rail', (.12, 1.03, .10), (x, 0, .80), m['wood'], radius=.015))
        parts.append(B.box('end_cushion', (.045, 1.03, .04), (x * .946, 0, .814), m['felt']))
    for x, y in [(-1.08, -.58), (-1.08, .58), (0, -.60), (0, .60), (1.08, -.58), (1.08, .58)]:
        parts.append(B.cylinder('pocket', .07, .028, (x, y, .797), m['ink'], segments=20))
        parts.append(B.ring('pocket_rim', .083, .067, .012, (x, y, .820), m['brass'], segments=20))
    # A small rack of balls and one cue establish scale. Their meshes are dressing, not physics.
    for row in range(3):
        for column in range(row + 1):
            x = .25 + row * .048
            y = (column - row * .5) * .055
            colour = [m['red'], m['blue'], m['white']][(row + column) % 3]
            parts.append(ball('ball', (x, y, .823), colour))
    parts.append(rod('cue', .008, (-.9, -.45, .84), (.55, -.38, .84), m['cream']))
    return parts


def hohbench(m):
    parts = [B.soft_box('seat', (1.42, .48, .13), (0, 0, .49), m['cream'], radius=.045),
             B.box('frame', (1.30, .39, .065), (0, 0, .405), m['wood']),
             B.box('brace', (1.16, .035, .045), (0, 0, .23), m['brass'])]
    for x in (-.55, .55):
        for y in (-.16, .16):
            parts.append(B.box('leg', (.055, .055, .39), (x, y, .195), m['wood']))
    # Welting separates the soft cushion from its dark frame.
    for y in (-.236, .236):
        parts.append(rod('welt', .008, (-.65, y, .465), (.65, y, .465), m['cream']))
    return parts


def tripod(m, top, spread):
    parts = []
    for angle in (30, 150, 270):
        a = math.radians(angle)
        x, y = math.cos(a) * spread, math.sin(a) * spread
        parts.append(B.cylinder('foot', .07, .025, (x, y, 0), m['ink'], segments=12))
        parts.append(rod('tripod_leg', .025, (x, y, .05), (0, 0, top), m['steel']))
    return parts


def lighttower(m):
    parts = tripod(m, .65, .47)
    # Triangular truss with alternating braces, rather than a solid pole silhouette.
    points = [(-.13, -.075), (.13, -.075), (0, .15)]
    for x, y in points:
        parts.append(rod('upright', .022, (x, y, .55), (x, y, 2.96), m['steel']))
    for level in range(6):
        low = .60 + level * .36
        for edge in range(3):
            a, b = points[edge], points[(edge + 1) % 3]
            parts.append(rod('brace', .013, (*a, low), (*b, low + .36), m['steel']))
    for x, z in ((-.18, 2.53), (.18, 2.53), (-.18, 2.89), (.18, 2.89)):
        parts.append(rod('lamp_can', .125, (x, .035, z), (x, -.15, z), m['ink']))
        parts.append(rod('lamp_lens', .105, (x, -.15, z), (x, -.162, z), m['lamp']))
        parts.append(B.box('yoke', (.32, .055, .035), (x, .035, z - .14), m['ink']))
    return parts


def studiocam(m):
    parts = tripod(m, 1.22, .43)
    parts += [B.cylinder('pan_head', .085, .12, (0, 0, 1.20), m['ink'], segments=16),
              B.box('mount', (.29, .19, .04), (0, 0, 1.34), m['steel']),
              B.soft_box('camera_body', (.36, .28, .23), (0, 0, 1.50), m['ink'], radius=.028),
              B.box('viewfinder', (.14, .12, .10), (.19, .06, 1.55), m['ink']),
              B.box('tally', (.06, .025, .035), (0, -.146, 1.615), m['tally'])]
    parts.append(rod('lens_barrel', .087, (0, -.13, 1.49), (0, -.31, 1.49), m['ink']))
    parts.append(rod('lens_glass', .070, (0, -.313, 1.49), (0, -.319, 1.49), m['lens']))
    parts.append(rod('pan_handle', .013, (.08, .06, 1.28), (.21, .37, 1.13), m['ink']))
    return parts


def export_named(name, path):
    builders = dict(pooltable=pooltable, hohbench=hohbench,
                    lighttower=lighttower, studiocam=studiocam)
    X.fresh_scene()
    identifier = 'bb_set_' + name
    coll = X.collection(identifier)
    obj = B.join(builders[name](palette()), identifier)
    X.apply_transforms([obj])
    # A tripod's asymmetric legs otherwise put its bounds centre behind its origin.
    # Placement and the existing authored-prop contract use the bounds centre in plan.
    lower = Vector(tuple(min(vertex.co[axis] for vertex in obj.data.vertices) for axis in range(3)))
    upper = Vector(tuple(max(vertex.co[axis] for vertex in obj.data.vertices) for axis in range(3)))
    centre = (lower + upper) * .5
    for vertex in obj.data.vertices:
        vertex.co.x -= centre.x
        vertex.co.y -= centre.y
    obj.data.update()
    X.link_only(obj, coll)
    X.export_collection(coll, path)
    X.report(coll, path)


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if len(args) != 1:
        raise X.ExportError('Provide one output directory after --.')
    directory = os.path.abspath(args[0])
    for name in ('pooltable', 'hohbench', 'lighttower', 'studiocam'):
        export_named(name, os.path.join(directory, 'bb_set_' + name + '.fbx'))


if __name__ == '__main__':
    main()

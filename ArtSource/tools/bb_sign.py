"""Builds lit wall signage: extruded neon lettering, optionally on a dark backing panel.

A shared builder rather than a set piece of its own, because the difference between the house's
signs is a string and a font, and `ArtSource/setpieces/bb_set_sign_*.py` each name one. That split
is what the repository's own rule asks for: every `bb_set_*.py` has exactly one `bb_set_*.fbx`, so
a parameterised script that emitted six exports would leave five files with no source and one
source with no file.

Letters are extruded geometry rather than a texture on a quad. Neon IS geometry - a bent tube with
depth and a bright edge - and a flat alpha-mapped panel gives the trick away at exactly the angle
the overview camera looks from. It also means no texture pipeline and no new importer rule: the
letters carry the same `bb_mat_neon_*` name every other glowing thing here does.

Origin centred left to right and at the BASE of the piece - the base of the lettering for a bare
sign, the base of the panel for a backed one - so it obeys the same "stands on z=0, centred in x
and z" rule every other prop does and a plan can hang it by its middle.
"""
import math
import os

import bpy

import bb_build as B
import bb_export as X

HERE = os.path.dirname(os.path.abspath(__file__))

# Inter for the block caps the mockups set their headings in; Segoe Script for the signs drawn as
# handwriting. A missing font falls back to Blender's own rather than failing the build - a sign in
# the wrong face is a note to fix, a build that stops is a blocked pipeline.
FONTS = {
    "caps": os.path.join(HERE, "..", "..", "Assets", "Gamesim", "Art", "Fonts", "Inter", "Inter-Bold.ttf"),
    "script": os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", "segoesc.ttf"),
}


def build(name, style, cap_height, backing, lines, out_path):
    """One sign. `style` is caps or script; `backing` is backed or bare; `lines` is a list."""
    X.fresh_scene()
    coll = X.collection(name)

    neon = X.material("neon_sign", (0.62, 0.80, 1.00), roughness=0.2,
                      emission=(0.52, 0.76, 1.0), emission_strength=3.2)
    panel = X.material("ink_sign", (0.05, 0.05, 0.07), metallic=0.2, roughness=0.7)

    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == "VIEW_3D")
    region = next(r for r in area.regions if r.type == "WINDOW")

    with bpy.context.temp_override(window=win, screen=win.screen, area=area, region=region):
        bpy.ops.object.text_add(location=(0.0, 0.0, 0.0))
        letters = bpy.context.object
        letters.data.body = "\n".join(lines)
        letters.data.align_x = "CENTER"
        letters.data.align_y = "CENTER"
        letters.data.size = cap_height
        letters.data.space_line = 0.95
        letters.data.extrude = max(0.015, cap_height * 0.05)
        # A sign is read across a room, not held up to the eye. Blender's default curve resolution
        # spends about six thousand triangles on fifteen letters. A script face carries many more
        # segments than a grotesque, so it is cut harder still; neither difference is visible at
        # the distance any of these hang.
        letters.data.resolution_u = 2 if style == "script" else 3
        font_path = os.path.abspath(FONTS.get(style, FONTS["caps"]))
        if os.path.exists(font_path):
            letters.data.font = bpy.data.fonts.load(font_path)
        else:
            print("NOTE no font at %s; using Blender's own" % font_path)
        # Stand the type up: a text object is drawn in the ground plane, and a sign hangs on a wall.
        letters.rotation_euler = (math.radians(90.0), 0.0, 0.0)
        bpy.ops.object.convert(target="MESH")

    letters = bpy.context.object
    letters.name = "letters"
    letters.data.materials.clear()
    letters.data.materials.append(neon)
    X.apply_transforms([letters])

    # Sit the lettering on z=0. A text object centres itself on its own baseline, so descenders and
    # second lines put the mesh below the origin, and every piece here is measured from the floor.
    low = min((letters.matrix_world @ vertex.co).z for vertex in letters.data.vertices)
    for vertex in letters.data.vertices:
        vertex.co.z -= low
    letters.data.update()

    parts = [letters]
    if backing == "backed":
        # A backed sign is measured from the bottom of its PANEL, not of its lettering: the panel is
        # the thing that hangs on the wall. So the letters move up by half the margin and the panel
        # fills the whole of it.
        span = letters.dimensions
        margin = cap_height * 0.9
        for vertex in letters.data.vertices:
            vertex.co.z += margin / 2.0
        letters.data.update()
        parts.append(B.box("backing",
                           (span.x + cap_height * 1.2, 0.04, span.z + margin),
                           (0.0, 0.05, (span.z + margin) / 2.0), panel))

    sign = B.join(parts, name)
    X.link_only(sign, coll)
    X.export_collection(coll, out_path)
    X.report(coll, out_path)

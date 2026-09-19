"""Tier 4 clutter: the small things that stop a dressed room reading as a greybox.

A module, not a script: the seven bb_set_*.py wrappers beside it each export one piece through
export(), so the checklist's one-script-one-export pairing holds. The pieces:

    bb_set_mug        a mug with a handle                       0.09 x 0.12 x 0.10
    bb_set_bottle     a bottle with a neck and a label          0.07 x 0.07 x 0.28
    bb_set_bookstack  three books, offset, lying flat           0.24 x 0.18 x 0.14
    bb_set_towel      a folded towel                            0.32 x 0.22 x 0.06
    bb_set_cushion    a scatter cushion                         0.40 x 0.40 x 0.12
    bb_set_laptop     an open laptop, lid raked back            0.33 x 0.30 x 0.22
    bb_set_tray       a bar tray with four glasses               0.40 x 0.30 x 0.14
    bb_set_remote     a television remote                        0.05 x 0.18 x 0.02
    bb_set_candle     a candle in a glass, lit                   0.08 x 0.08 x 0.13
    bb_set_photoframe a standing photo frame                     0.16 x 0.06 x 0.20
    bb_set_magazines  two magazines, fanned                      0.30 x 0.32 x 0.02
    bb_set_fruitbowl  a bowl with five oranges                   0.28 x 0.28 x 0.13
    bb_set_cable      an extension lead and its cable            0.62 x 0.20 x 0.04

Every one is native size (the plan's rows say 0 for height) and sits on a surface by the row's
lift. Origin at the base under the centre. Clutter, so no collider.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402


def materials():
    return {
        "white": X.material("linen_white", (0.90, 0.88, 0.82), roughness=0.4),
        "teal": X.material("velvet_teal", (0.05, 0.32, 0.38), roughness=0.75),
        "coral": X.material("cushion_coral", (0.90, 0.40, 0.35), roughness=0.6),
        "brass": X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35),
        "ink": X.material("ink", (0.08, 0.08, 0.10), roughness=0.55),
        "green": X.material("bottle_green", (0.12, 0.38, 0.22), roughness=0.25),
        "label": X.material("coping_stone", (0.85, 0.82, 0.75), roughness=0.7),
        "glass": X.material("tray_glass", (0.85, 0.9, 0.92), roughness=0.05, alpha=0.35),
        "screen": X.material("glow_screen", (0.20, 0.36, 0.55), roughness=0.2,
                             emission=(0.25, 0.45, 0.70), emission_strength=1.5),
        "book_a": X.material("book_teal", (0.10, 0.36, 0.40)),
        "book_b": X.material("book_coral", (0.86, 0.38, 0.32)),
        "book_c": X.material("book_brass", (0.78, 0.62, 0.28)),
        "wax": X.material("candle_wax", (0.95, 0.92, 0.85), roughness=0.5),
        "flame": X.material("glow_flame", (1.0, 0.78, 0.35), roughness=0.3,
                            emission=(1.0, 0.62, 0.22), emission_strength=3.0),
        "orange": X.material("fruit_orange", (0.95, 0.52, 0.12), roughness=0.55),
        "cream": X.material("cabinet_cream", (0.92, 0.90, 0.84), roughness=0.5),
    }


def mug(m):
    return [
        B.ring("body", 0.045, 0.04, 0.10, (0.0, 0.0, 0.0), m["white"], segments=16),
        B.disc("base", 0.045, (0.0, 0.0, 0.0), m["white"], segments=16),
        B.disc("coffee", 0.04, (0.0, 0.0, 0.085), m["ink"], segments=16),
        B.box("handle_out", (0.02, 0.012, 0.06), (0.058, 0.0, 0.05), m["white"]),
        B.box("handle_top", (0.03, 0.012, 0.012), (0.05, 0.0, 0.074), m["white"]),
        B.box("handle_bottom", (0.03, 0.012, 0.012), (0.05, 0.0, 0.026), m["white"]),
    ]


def bottle(m):
    return [
        B.cylinder("body", 0.035, 0.20, (0.0, 0.0, 0.0), m["green"], segments=14),
        B.cylinder("shoulder", 0.025, 0.02, (0.0, 0.0, 0.20), m["green"], segments=14),
        B.cylinder("neck", 0.014, 0.06, (0.0, 0.0, 0.22), m["green"], segments=10),
        B.box("label", (0.072, 0.03, 0.07), (0.0, -0.02, 0.10), m["label"]),
    ]


def bookstack(m):
    return [
        B.box("book_0", (0.22, 0.16, 0.045), (0.0, 0.0, 0.0225), m["book_a"]),
        B.box("book_1", (0.20, 0.15, 0.04), (0.015, -0.01, 0.065), m["book_b"]),
        B.box("book_2", (0.21, 0.14, 0.05), (-0.01, 0.015, 0.11), m["book_c"]),
    ]


def towel(m):
    return [
        B.box("fold_a", (0.32, 0.22, 0.03), (0.0, 0.0, 0.015), m["teal"]),
        B.box("fold_b", (0.30, 0.20, 0.03), (0.0, 0.0, 0.045), m["teal"]),
    ]


def cushion(m):
    return [
        B.box("core", (0.36, 0.36, 0.12), (0.0, 0.0, 0.06), m["coral"]),
        B.box("edge", (0.40, 0.40, 0.06), (0.0, 0.0, 0.06), m["coral"]),
    ]


def laptop(m):
    lid = B.box("lid", (0.33, 0.01, 0.22), (0.0, 0.0, 0.11), m["ink"])
    screen = B.box("screen", (0.30, 0.004, 0.19), (0.0, -0.007, 0.115), m["screen"])
    for part in (lid, screen):
        part.data.transform(Matrix.Rotation(math.radians(-20.0), 4, 'X'))
        part.data.transform(Matrix.Translation(Vector((0.0, 0.11, 0.015))))
    return [
        B.box("base", (0.33, 0.23, 0.015), (0.0, 0.0, 0.0075), m["ink"]),
        B.box("keys", (0.28, 0.12, 0.003), (0.0, -0.02, 0.0165), m["label"]),
        lid, screen,
    ]


def tray(m):
    parts = [
        B.box("tray", (0.40, 0.30, 0.012), (0.0, 0.0, 0.006), m["brass"]),
        B.box("lip_a", (0.40, 0.012, 0.03), (0.0, 0.144, 0.015), m["brass"]),
        B.box("lip_b", (0.40, 0.012, 0.03), (0.0, -0.144, 0.015), m["brass"]),
    ]
    for i, (x, y) in enumerate([(-0.12, -0.07), (-0.12, 0.07), (0.12, -0.07), (0.12, 0.07)]):
        parts.append(B.ring("glass_%d" % i, 0.032, 0.028, 0.12, (x, y, 0.012), m["glass"], segments=12))
        parts.append(B.disc("glass_base_%d" % i, 0.032, (x, y, 0.012), m["glass"], segments=12))
    return parts


def remote(m):
    parts = [B.box("body", (0.05, 0.18, 0.02), (0.0, 0.0, 0.01), m["ink"])]
    for i, (x, y) in enumerate([(-0.012, 0.05), (0.012, 0.05), (-0.012, 0.02), (0.012, 0.02), (0.0, -0.02), (0.0, -0.05)]):
        parts.append(B.cylinder("key_%d" % i, 0.006, 0.004, (x, y, 0.02), m["label"], segments=8))
    parts.append(B.box("pad", (0.03, 0.03, 0.004), (0.0, -0.035, 0.02), m["coral"]))
    return parts


def candle(m):
    return [
        B.ring("glass", 0.04, 0.036, 0.10, (0.0, 0.0, 0.0), m["glass"], segments=16),
        B.disc("glass_base", 0.04, (0.0, 0.0, 0.0), m["glass"], segments=16),
        B.cylinder("wax", 0.034, 0.07, (0.0, 0.0, 0.004), m["wax"], segments=16),
        B.cylinder("wick", 0.003, 0.012, (0.0, 0.0, 0.074), m["ink"], segments=6),
        B.cylinder("flame", 0.008, 0.03, (0.0, 0.0, 0.086), m["flame"], segments=8),
        B.cylinder("flame_tip", 0.004, 0.014, (0.0, 0.0, 0.116), m["flame"], segments=8),
    ]


def photoframe(m):
    frame = B.box("frame", (0.16, 0.012, 0.20), (0.0, 0.0, 0.10), m["brass"])
    photo = B.box("photo", (0.13, 0.004, 0.17), (0.0, -0.005, 0.10), m["teal"])
    face = B.box("face", (0.05, 0.003, 0.06), (0.0, -0.008, 0.11), m["coral"])
    leg = B.box("leg", (0.04, 0.12, 0.012), (0.0, 0.06, 0.006), m["brass"])
    leg.data.transform(Matrix.Rotation(math.radians(-55.0), 4, 'X'))
    leg.data.transform(Matrix.Translation(Vector((0.0, 0.02, 0.10))))
    for part in (frame, photo, face):
        part.data.transform(Matrix.Rotation(math.radians(8.0), 4, 'X'))
    return [frame, photo, face, leg]


def magazines(m):
    parts = []
    for i, (angle, colour, dz) in enumerate([(-12.0, m["coral"], 0.0), (14.0, m["teal"], 0.006)]):
        cover = B.box("magazine_%d" % i, (0.21, 0.28, 0.006), (0.0, 0.0, 0.003 + dz), colour)
        cover.data.transform(Matrix.Rotation(math.radians(angle), 4, 'Z'))
        parts.append(cover)
        title = B.box("title_%d" % i, (0.14, 0.03, 0.002), (0.0, 0.10, 0.0075 + dz), m["label"])
        title.data.transform(Matrix.Rotation(math.radians(angle), 4, 'Z'))
        parts.append(title)
    return parts


def fruitbowl(m):
    parts = [
        B.ring("bowl", 0.14, 0.13, 0.07, (0.0, 0.0, 0.01), m["cream"], segments=20),
        B.disc("bowl_base", 0.14, (0.0, 0.0, 0.01), m["cream"], segments=20),
        B.cylinder("foot", 0.06, 0.01, (0.0, 0.0, 0.0), m["cream"], segments=16),
    ]
    for i, (x, y, z) in enumerate([(-0.05, 0.0, 0.02), (0.05, 0.0, 0.02), (0.0, 0.05, 0.02), (0.0, -0.05, 0.02), (0.0, 0.0, 0.075)]):
        parts.append(B.cylinder("orange_%d" % i, 0.038, 0.055, (x, y, z), m["orange"], segments=12))
    return parts


def cable(m):
    parts = [
        B.box("lead", (0.26, 0.06, 0.035), (0.0, 0.0, 0.0175), m["cream"]),
        B.box("switch", (0.02, 0.012, 0.008), (-0.11, 0.0, 0.035), m["coral"]),
        B.box("cord", (0.36, 0.012, 0.012), (0.31, -0.04, 0.006), m["ink"]),
        B.box("cord_turn", (0.012, 0.09, 0.012), (0.49, -0.085, 0.006), m["ink"]),
        B.box("plug", (0.05, 0.03, 0.02), (0.13, 0.0, 0.01), m["ink"]),
    ]
    for i, x in enumerate([-0.07, -0.02, 0.03, 0.08]):
        parts.append(B.cylinder("socket_%d" % i, 0.014, 0.003, (x, 0.0, 0.035), m["ink"], segments=10))
    # The cord runs off to one side; the checklist wants the whole piece centred on its origin.
    for part in parts:
        part.data.transform(Matrix.Translation(Vector((-0.183, 0.05, 0.0))))
    return parts


PIECES = {
    "bb_set_mug": mug,
    "bb_set_bottle": bottle,
    "bb_set_bookstack": bookstack,
    "bb_set_towel": towel,
    "bb_set_cushion": cushion,
    "bb_set_laptop": laptop,
    "bb_set_tray": tray,
    "bb_set_remote": remote,
    "bb_set_candle": candle,
    "bb_set_photoframe": photoframe,
    "bb_set_magazines": magazines,
    "bb_set_fruitbowl": fruitbowl,
    "bb_set_cable": cable,
}



def export(name):
    """Builds one piece and exports it to the path after '--' (or <cwd>/<name>.fbx)."""
    X.fresh_scene()
    coll = X.collection(name)
    piece = B.join(PIECES[name](materials()), name)
    X.link_only(piece, coll)
    path = X.output_path(name + ".fbx")
    X.export_collection(coll, path)
    X.report(coll, path)

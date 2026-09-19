"""The house shell (MASTER-PLAN Part 4, Tier 2): the cutaway walls, dividers and yard fences as
authored geometry laid exactly over the colliders the NavMesh is baked from.

Reads house_walls.json (every thin, tall BoxCollider under "House Architecture", extracted from
the shipping scene) and builds, for each active wall, a slab with a cap trim along its top, a
skirting board along both faces, and a jamb post at every end that does not meet another wall -
which is exactly the ends that frame a doorway. Fences get slab and cap only.

Nothing here moves a collider: the export is visual only, placed at the world origin, and the
primitives keep their colliders with their renderers switched off. Origin (0, 0, 0), floor at z=0.

    blender --background --python ArtSource/shell/bb_shell.py -- <output.fbx>
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

CAP_H, CAP_PROUD = 0.06, 0.035
SKIRT_H, SKIRT_PROUD = 0.10, 0.02
JAMB, JAMB_EXTRA = 0.14, 0.05
TOUCH = 0.20  # an end within this of another wall's body is a corner or a tee, not a doorway


def load():
    with open(os.path.join(HERE, "house_walls.json"), encoding="utf-8") as handle:
        return [w for w in json.load(handle)["walls"] if w["active"]]


def unity_to_blender(center, size):
    """Unity (x, y, z) with y up -> Blender (x, y, z) with z up, y = Unity z."""
    cx, cy, cz = center
    sx, sy, sz = size
    return (cx, cz, cy), (sx, sz, sy)


def inside(point, wall, tolerance):
    c, s = wall["center"], wall["size"]
    return all(abs(point[i] - c[i]) <= s[i] / 2 + tolerance for i in (0, 2))


def open_ends(wall, walls):
    """The two end points of a wall's long axis, and whether each is free of other walls."""
    c, s = wall["center"], wall["size"]
    along = 0 if s[0] >= s[2] else 2
    ends = []
    for sign in (-1, 1):
        point = list(c)
        point[along] = c[along] + sign * s[along] / 2
        touching = any(other is not wall and inside(point, other, TOUCH) for other in walls)
        ends.append((point, along, not touching))
    return ends


def main():
    X.fresh_scene()
    coll = X.collection("bb_shell_house")
    wall_mat = X.material("shell_wall", (0.105, 0.115, 0.125), roughness=0.85)
    cap_mat = X.material("shell_cap", (0.77, 0.60, 0.28), metallic=0.6, roughness=0.4)
    skirt_mat = X.material("shell_skirt", (0.065, 0.11, 0.15), roughness=0.8)
    fence_mat = X.material("shell_fence", (0.065, 0.11, 0.15), roughness=0.8)

    walls = load()
    parts = []
    for index, wall in enumerate(walls):
        (cx, cy, cz), (sx, sy, sz) = unity_to_blender(wall["center"], wall["size"])
        height = sz
        base = cz - sz / 2.0
        tag = "%02d" % index
        is_fence = wall["kind"] == "fence"
        material = fence_mat if is_fence else wall_mat
        # The slab itself, a hair smaller than the collider so the trims can be proud of it.
        parts.append(B.box("slab_" + tag, (sx - 0.01, sy - 0.01, height), (cx, cy, base + height / 2.0), material))
        # Cap: proud on both faces, along the long axis.
        along_x = sx >= sy
        cap_size = (sx + (0.0 if along_x else 2 * CAP_PROUD), sy + (2 * CAP_PROUD if along_x else 0.0), CAP_H)
        parts.append(B.box("cap_" + tag, cap_size, (cx, cy, base + height - CAP_H / 2.0), cap_mat))
        if is_fence:
            continue
        # Skirting, proud on both faces.
        skirt_size = (sx + (2 * SKIRT_PROUD if not along_x else 0.0), sy + (2 * SKIRT_PROUD if along_x else 0.0), SKIRT_H)
        parts.append(B.box("skirt_" + tag, skirt_size, (cx, cy, base + SKIRT_H / 2.0), skirt_mat))
        # Jambs at doorway ends.
        for end_index, (point, along, free) in enumerate(open_ends(wall, walls)):
            if not free:
                continue
            px, py, pz = unity_to_blender(point, (0, 0, 0))[0]
            parts.append(B.box("jamb_%s_%d" % (tag, end_index), (JAMB, JAMB, height + JAMB_EXTRA),
                               (px, py, base + (height + JAMB_EXTRA) / 2.0), cap_mat))

    shell = B.join(parts, "bb_shell_house")
    X.link_only(shell, coll)
    path = X.output_path("bb_shell_house.fbx")
    X.export_collection(coll, path)
    X.report(coll, path)
    print("walls:", len(walls), "parts:", len(parts))


if __name__ == "__main__":
    main()

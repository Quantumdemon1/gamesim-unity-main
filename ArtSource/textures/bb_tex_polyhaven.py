"""The photographed surfaces: Poly Haven (CC0) texture sets for the floors and the walls (V4).

The baked surfaces in bb_tex_floors.py and bb_tex_walls.py are patterns - a Brick node's planks,
a noise field's plaster - and they read as patterns at the camera's distance. These are scans of
real floors and a real wall, four maps each, fetched at 1k through bb_polyhaven.fetch_texture and
written into the layout HouseFloorDressing already reads:

    Assets/Gamesim/Art/Authored/Textures/bb_tex_<name>_{albedo,normal,metallic,occlusion}.png

Each row carries the surface's real size in metres, straight off its Poly Haven page: that number
is the tile HouseFloorDressing.Plan lays it at, so a plank is a plank's width on the floor and the
grout lines in the kitchen are a foot apart rather than a guess. The baked pairs stay where they
are - nothing here deletes them, and a floor whose set has not been fetched keeps the one it has.

    blender --background --python ArtSource/textures/bb_tex_polyhaven.py -- [<name> ...]

Named arguments build only those surfaces; with none, all four.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_polyhaven as P  # noqa: E402


class Surface(object):
    """A Poly Haven texture set, the name it takes here, and the metres one tile covers."""

    def __init__(self, asset, name, metres, note):
        self.asset, self.name, self.metres, self.note = asset, name, metres, note


SURFACES = [
    # The living room, the private room and the nomination room: a clean, lacquered plank floor,
    # 1.70 m to the tile.
    Surface("wood_floor", "ph_oak", 1.70, "living room, private room, nomination room"),
    # The bedrooms and the HoH suite: darker, wider boards, 2.00 m to the tile.
    Surface("dark_wooden_planks", "ph_walnut", 2.00, "bedroom, HoH suite"),
    # The kitchen and the game room: square ceramic tiles with grout, 1.90 m to the tile.
    Surface("interior_tiles", "ph_tile", 1.90, "kitchen, game room"),
    # Every wall of the shell: a matte white stucco, 2.00 m to the tile, which is the tile the
    # shell's world-scale UVs were already cut for.
    Surface("white_stucco", "ph_plaster", 2.00, "the shell's walls"),
]

BY_NAME = dict((surface.name, surface) for surface in SURFACES)


def build(only=()):
    written = []
    for surface in SURFACES:
        if only and surface.name not in only:
            continue
        written += P.texture_set(surface.asset, surface.name)
        print("SURFACE %s = %s, %.2f m a tile (%s)" % (surface.name, surface.asset, surface.metres, surface.note))
    return written


if __name__ == "__main__":
    names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    build([name for name in names if not name.startswith("--")])

"""The hot tub: a wooden drum with a tiled well, a seating ring and a lit water surface.

2.4 m across, 0.85 m high, origin at the floor under the centre. A _col cylinder keeps
houseguests from walking through it.

    blender --background --python ArtSource/setpieces/bb_set_hottub.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_hottub")

wood = X.material("deck_wood", (0.45, 0.30, 0.18), roughness=0.55)
tile = X.material("pool_tile", (0.55, 0.85, 0.85), roughness=0.35)
stone = X.material("coping_stone", (0.85, 0.82, 0.75), roughness=0.7)
water = X.material("water", (0.15, 0.55, 0.85), roughness=0.1, emission=(0.05, 0.25, 0.45),
                   emission_strength=0.4, alpha=0.55)

OUTER, INNER, HEIGHT = 1.15, 1.0, 0.85
FLOOR_Z = 0.20

parts = [
    B.ring("shell", OUTER, INNER, HEIGHT, (0.0, 0.0, 0.0), wood),
    B.cylinder("well_floor", INNER, FLOOR_Z, (0.0, 0.0, 0.0), tile),
    B.ring("seat", INNER, 0.68, 0.12, (0.0, 0.0, 0.42), tile),
    B.ring("rim", OUTER + 0.06, INNER - 0.04, 0.05, (0.0, 0.0, HEIGHT), stone),
]
tub = B.join(parts, "bb_set_hottub")
X.link_only(tub, coll)

surface = B.disc("bb_set_hottub_water", INNER - 0.02, (0.0, 0.0, HEIGHT - 0.08), water)
X.link_only(B.child(surface, tub), coll)

collider = B.cylinder("bb_set_hottub_col", OUTER + 0.06, HEIGHT + 0.05, (0.0, 0.0, 0.0), segments=16)
X.link_only(B.child(collider, tub), coll)

path = X.output_path("bb_set_hottub.fbx")
X.export_collection(coll, path)
X.report(coll, path)

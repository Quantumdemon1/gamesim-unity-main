"""The backyard pool — the most recognisable thing about the set, and a piece no pack ships.

A raised deck rather than a sunken basin: the yard is a floor plane, so a pool cut below it would
show nothing. The deck rises 0.35 m, the basin sits inside it lined in tile, a translucent water
plane floats just under the coping, and steps lead in from the west end. 7.0 x 4.4 m footprint,
origin at the floor under the centre. A _col box covers the deck so houseguests path around it.

    blender --background --python ArtSource/setpieces/bb_set_pool.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_pool")

wood = X.material("deck_wood", (0.45, 0.30, 0.18), roughness=0.55)
tile = X.material("pool_tile", (0.55, 0.85, 0.85), roughness=0.35)
stone = X.material("coping_stone", (0.85, 0.82, 0.75), roughness=0.7)
water = X.material("water", (0.15, 0.55, 0.85), roughness=0.1, emission=(0.05, 0.25, 0.45),
                   emission_strength=0.4, alpha=0.55)

DECK_W, DECK_D, DECK_H = 7.0, 4.4, 0.35
BASIN_W, BASIN_D = 5.6, 3.0
FRAME = 0.7          # (DECK_W - BASIN_W) / 2
FLOOR_T = 0.04
LIP_W, LIP_H = 0.18, 0.04

parts = [
    # The deck frame around the basin.
    B.box("frame_n", (DECK_W, FRAME, DECK_H), (0.0, (BASIN_D + FRAME) / 2, DECK_H / 2), wood),
    B.box("frame_s", (DECK_W, FRAME, DECK_H), (0.0, -(BASIN_D + FRAME) / 2, DECK_H / 2), wood),
    B.box("frame_e", (FRAME, BASIN_D, DECK_H), ((BASIN_W + FRAME) / 2, 0.0, DECK_H / 2), wood),
    B.box("frame_w", (FRAME, BASIN_D, DECK_H), (-(BASIN_W + FRAME) / 2, 0.0, DECK_H / 2), wood),
    # The basin: a tiled floor and thin tiled walls lining the frame.
    B.box("basin_floor", (BASIN_W, BASIN_D, FLOOR_T), (0.0, 0.0, FLOOR_T / 2), tile),
    B.box("wall_n", (BASIN_W, 0.03, DECK_H - FLOOR_T), (0.0, BASIN_D / 2 - 0.015, (DECK_H + FLOOR_T) / 2), tile),
    B.box("wall_s", (BASIN_W, 0.03, DECK_H - FLOOR_T), (0.0, -BASIN_D / 2 + 0.015, (DECK_H + FLOOR_T) / 2), tile),
    B.box("wall_e", (0.03, BASIN_D, DECK_H - FLOOR_T), (BASIN_W / 2 - 0.015, 0.0, (DECK_H + FLOOR_T) / 2), tile),
    B.box("wall_w", (0.03, BASIN_D, DECK_H - FLOOR_T), (-BASIN_W / 2 + 0.015, 0.0, (DECK_H + FLOOR_T) / 2), tile),
    # Coping: a pale lip around the basin edge, proud of the deck.
    B.box("lip_n", (BASIN_W + 2 * LIP_W, LIP_W, LIP_H), (0.0, BASIN_D / 2 + LIP_W / 2, DECK_H + LIP_H / 2), stone),
    B.box("lip_s", (BASIN_W + 2 * LIP_W, LIP_W, LIP_H), (0.0, -BASIN_D / 2 - LIP_W / 2, DECK_H + LIP_H / 2), stone),
    B.box("lip_e", (LIP_W, BASIN_D, LIP_H), (BASIN_W / 2 + LIP_W / 2, 0.0, DECK_H + LIP_H / 2), stone),
    B.box("lip_w", (LIP_W, BASIN_D, LIP_H), (-BASIN_W / 2 - LIP_W / 2, 0.0, DECK_H + LIP_H / 2), stone),
    # Two steps down into the water at the west end.
    B.box("step_top", (0.45, 1.4, 0.20), (-BASIN_W / 2 + 0.225, 0.0, FLOOR_T + 0.10), tile),
    B.box("step_low", (0.45, 1.4, 0.10), (-BASIN_W / 2 + 0.675, 0.0, FLOOR_T + 0.05), tile),
]
pool = B.join(parts, "bb_set_pool")
X.link_only(pool, coll)

surface = B.plane("bb_set_pool_water", (BASIN_W - 0.04, BASIN_D - 0.04), (0.0, 0.0, DECK_H - 0.06), water)
X.link_only(B.child(surface, pool), coll)

collider = B.box("bb_set_pool_col", (DECK_W, DECK_D, DECK_H + LIP_H), (0.0, 0.0, (DECK_H + LIP_H) / 2))
X.link_only(B.child(collider, pool), coll)

path = X.output_path("bb_set_pool.fbx")
X.export_collection(coll, path)
X.report(coll, path)

"""Poly Haven's Potted Plant 02 (CC0), cut to the budget, as the house's potted plant (V4).

0.70 x 0.66 m across and 0.84 m tall as scanned: broad leaves over a round glazed pot. The kit id
<c>pottedPlant</c> resolves to it through the catalogue, and each row scales it to its own height,
so the scan's size only has to be the right shape. Origin at the floor under the pot. Furniture,
so no collider.

The budget is 1500, not a hero piece's 4000: there are eleven of these in the house and they are
dressing, not what the camera settles on. Potted Plant 01, the taller scan, was the first choice
and is not usable here - its leaves are a hundred and seventy thousand triangles of thin cards, and
the collapse that brings any scan down to a game budget cannot take those below about four
thousand without tearing the plant into confetti. This one's leaves are larger and fewer, and it
still reads as a plant at a tenth of that.

    blender --background --python ArtSource/setpieces/bb_set_ph_plant.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("potted_plant_02", "ph_plant", tris=1500, out=X.output_path("bb_set_ph_plant.fbx"), fresh=True)

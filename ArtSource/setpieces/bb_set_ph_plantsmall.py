"""Poly Haven's Potted Plant 04 (CC0), cut to the budget, as the small plant on the floor (V4).

0.17 x 0.19 m across and 0.27 m tall as scanned: a succulent in a small pot. The kit ids
<c>plantSmall1</c>, <c>plantSmall2</c> and <c>plantSmall3</c> all resolve to it, and each row
scales it to its own height - half a metre, on the floor beside a bed or a lounger. Origin at the
floor under the pot. Furniture, so no collider.

The budget is 900: there are a dozen of these and none of them is ever the subject of a shot.

    blender --background --python ArtSource/setpieces/bb_set_ph_plantsmall.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("potted_plant_04", "ph_plantsmall", tris=900, out=X.output_path("bb_set_ph_plantsmall.fbx"), fresh=True)

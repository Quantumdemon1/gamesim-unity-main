"""Poly Haven's ClassicNightstand 01 (CC0), cut to the budget, as the bedrooms' hero side table (VISUAL-TARGET.md V4).

0.57 m wide, 0.42 m deep, 0.70 m tall: a classic nightstand with a drawer, as imported. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_ph_sidetable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("ClassicNightstand_01", "ph_sidetable", tris=2000, out=X.output_path("bb_set_ph_sidetable.fbx"), fresh=True)

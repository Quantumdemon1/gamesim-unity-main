"""Poly Haven's Ottoman 01 (CC0), cut to the budget, as a hero seat for the private room (VISUAL-TARGET.md V4).

0.88 m by 0.62 m, 0.62 m tall: an upholstered ottoman, as imported. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_ph_ottoman.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("Ottoman_01", "ph_ottoman", tris=1500, out=X.output_path("bb_set_ph_ottoman.fbx"), fresh=True)

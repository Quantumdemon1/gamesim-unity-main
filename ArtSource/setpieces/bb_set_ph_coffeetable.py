"""Poly Haven's CoffeeTable 01 (CC0), cut to the budget, as the living room's hero coffee table (VISUAL-TARGET.md V4).

1.54 m by 0.97 m, 0.52 m tall: a low wooden coffee table, as imported. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_ph_coffeetable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("CoffeeTable_01", "ph_coffeetable", tris=2500, out=X.output_path("bb_set_ph_coffeetable.fbx"), fresh=True)

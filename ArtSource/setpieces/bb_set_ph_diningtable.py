"""Poly Haven's dining table (CC0), cut to the budget, as the kitchen's hero dining table (VISUAL-TARGET.md V4).

2.26 m by 1.39 m, 0.88 m tall: a long wooden dining table, as imported. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_ph_diningtable.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("dining_table", "ph_diningtable", tris=2500, out=X.output_path("bb_set_ph_diningtable.fbx"), fresh=True)

"""Poly Haven's ArmChair 01 (CC0), cut to the budget, as the living room's hero armchair (VISUAL-TARGET.md V4).

0.85 m wide, 0.77 m deep, 1.07 m tall: a high-backed armchair in worn leather, as imported. Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_ph_armchair.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("ArmChair_01", "ph_armchair", tris=3000, out=X.output_path("bb_set_ph_armchair.fbx"), fresh=True)

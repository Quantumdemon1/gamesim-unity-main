"""Poly Haven's Sofa 01 (CC0), cut to the budget, as the living room's hero sofa (VISUAL-TARGET.md V4).

1.57 m wide, 0.66 m deep, 0.80 m tall: a three-seater with a fabric weave, as imported (its back
at +y). Origin at the floor under the centre. Furniture, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_ph_sofa.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("Sofa_01", "ph_sofa", tris=4000, out=X.output_path("bb_set_ph_sofa.fbx"), fresh=True)

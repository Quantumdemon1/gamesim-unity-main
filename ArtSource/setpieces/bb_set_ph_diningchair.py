"""Poly Haven's GreenChair 01 (CC0), cut to the budget, as the kitchen's hero dining chair (VISUAL-TARGET.md V4).

0.67 m wide, 0.66 m deep, 1.06 m tall: a classic dining chair in green upholstery, as imported. Origin at the floor under the centre. Furniture, so no collider.

It is turned half a circle on the way in. The scan has its back at +y, and every authored piece in
this set has its back at -y and faces +y - which is what the yaw column of the sixteen places at
the long table means by 0 and 180. Without the turn every chair at the table faced away from it.

    blender --background --python ArtSource/setpieces/bb_set_ph_diningchair.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_polyhaven as P  # noqa: E402

P.build("GreenChair_01", "ph_diningchair", tris=2000, out=X.output_path("bb_set_ph_diningchair.fbx"), fresh=True, yaw=180.0)

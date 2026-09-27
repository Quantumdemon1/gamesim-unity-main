"""The competition's own title, centred on the backdrop between two of its tubes.

Block caps at 0.55 m, which is the largest type in the house: it is read from the far side of a
28 m yard, over the heads of three houseguests and through a lit gate.

Built by `ArtSource/tools/bb_sign.py`, which every sign shares: the difference between them is a
string, a font and a size. Origin centred left to right at the base of the piece, letters facing
-y (Unity -z). Signage, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sign_hoh.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_sign  # noqa: E402

bb_sign.build("bb_set_sign_hoh", "caps", 0.55, "bare", ['HOH COMPETITION'], X.output_path("bb_set_sign_hoh.fbx"))

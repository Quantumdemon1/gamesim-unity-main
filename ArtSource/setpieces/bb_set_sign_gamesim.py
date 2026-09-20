"""The brand, on a dark panel, for the living-room wall the mockups put it on.

The one backed sign: mockup-01 hangs it as a lit board rather than as bare tube, which is what
lets it sit against a pale wall without disappearing.

Built by `ArtSource/tools/bb_sign.py`, which every sign shares: the difference between them is a
string, a font and a size. Origin centred left to right at the base of the piece, letters facing
-y (Unity -z). Signage, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sign_gamesim.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_sign  # noqa: E402

bb_sign.build("bb_set_sign_gamesim", "caps", 0.46, "backed", ['GAMESIM'], X.output_path("bb_set_sign_gamesim.fbx"))

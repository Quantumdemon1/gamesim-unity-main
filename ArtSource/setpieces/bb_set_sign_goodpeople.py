"""The second half of the brand, in handwriting, for the yard wall.

Segoe Script at 0.42 m and unbacked, so it reads as a tube bent into words on a dark wall
rather than as a printed board.

Built by `ArtSource/tools/bb_sign.py`, which every sign shares: the difference between them is a
string, a font and a size. Origin centred left to right at the base of the piece, letters facing
-y (Unity -z). Signage, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sign_goodpeople.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_sign  # noqa: E402

bb_sign.build("bb_set_sign_goodpeople", "script", 0.42, "bare", ['Good People', 'Bigger Stories'], X.output_path("bb_set_sign_goodpeople.fbx"))

"""The show's line, right of the competition title.

Two lines at 0.30 m, with the full stops the mockup sets: the pause is the joke.

Built by `ArtSource/tools/bb_sign.py`, which every sign shares: the difference between them is a
string, a font and a size. Origin centred left to right at the base of the piece, letters facing
-y (Unity -z). Signage, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sign_samehouse.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_sign  # noqa: E402

bb_sign.build("bb_set_sign_samehouse", "caps", 0.3, "bare", ['SAME HOUSE.', 'DIFFERENT STORIES.'], X.output_path("bb_set_sign_samehouse.fbx"))

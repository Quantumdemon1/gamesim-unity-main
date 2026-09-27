"""The four words the format is built on, stacked left of the competition title.

Four lines at 0.26 m. Small on purpose: it is a list to notice, not a headline, and the mockup
sets it at about half the title's size.

Built by `ArtSource/tools/bb_sign.py`, which every sign shares: the difference between them is a
string, a font and a size. Origin centred left to right at the base of the piece, letters facing
-y (Unity -z). Signage, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sign_pillars.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_sign  # noqa: E402

bb_sign.build("bb_set_sign_pillars", "caps", 0.26, "bare", ['STRATEGY', 'STRENGTH', 'SOCIAL', 'SURVIVAL'], X.output_path("bb_set_sign_pillars.fbx"))

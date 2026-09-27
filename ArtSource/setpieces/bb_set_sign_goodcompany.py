"""The handwritten half of the living-room sign, hung under the brand panel.

Built as its own piece rather than as a second line of the panel, because the two are set in
different faces and one script cannot mix them.

Built by `ArtSource/tools/bb_sign.py`, which every sign shares: the difference between them is a
string, a font and a size. Origin centred left to right at the base of the piece, letters facing
-y (Unity -z). Signage, so no collider.

    blender --background --python ArtSource/setpieces/bb_set_sign_goodcompany.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_export as X  # noqa: E402
import bb_sign  # noqa: E402

bb_sign.build("bb_set_sign_goodcompany", "script", 0.34, "bare", ['Good Company'], X.output_path("bb_set_sign_goodcompany.fbx"))

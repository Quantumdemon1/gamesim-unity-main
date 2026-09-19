"""A folded towel, for the tub's rim. Built by clutter_pieces.py.

    blender --background --python ArtSource/setpieces/bb_set_towel.py -- <output.fbx>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clutter_pieces  # noqa: E402

clutter_pieces.export("bb_set_towel")

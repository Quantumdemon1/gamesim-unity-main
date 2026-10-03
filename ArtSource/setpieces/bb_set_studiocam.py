"""Regenerate the original studio camera: pass its FBX output path after --."""
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import bb_house_amenities as A

if __name__ == '__main__':
    A.export_named('studiocam', A.X.output_path('bb_set_studiocam.fbx'))

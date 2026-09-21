"""A competition gate: the lit frame a houseguest runs through, one per lane.

mockup-05 stands three of these across the yard behind the podiums - a tall rectangular tube of
neon, each in its lane's colour, against the dark back wall. They are what makes the yard read as
a built course rather than a patio with people on it, and they do it at silhouette scale, which is
what survives the overhead competition camera.

The neon is authored white and named for its glow; the placement step tints each of the three to
its lane, so one mesh serves all three rather than three near-identical files. 2.6 m to the top of
the frame, 1.7 m of clear width - a houseguest is 1.8 m, so the frame reads as an arch they pass
under rather than a doorway they fill. Origin on the floor at the centre of the opening. A yard
graphic, so no collider: nothing should walk into it.

    blender --background --python ArtSource/setpieces/bb_set_comp_gate.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_comp_gate")

# White, because the lane's colour is decided where it stands. Named for the glow so the importer
# lights it at the strength of the house's own neon.
neon = X.material("neon_gate", (0.90, 0.94, 1.00), roughness=0.25,
                  emission=(1.0, 1.0, 1.0), emission_strength=3.0)
ink = X.material("ink_gate", (0.06, 0.06, 0.08), metallic=0.3, roughness=0.5)

CLEAR = 1.70          # clear width between the uprights
TOP = 2.60            # top of the frame
TUBE = 0.09           # the neon's square section
FOOT = 0.22           # the dark foot each upright stands in
HALF = CLEAR / 2 + TUBE / 2

parts = [
    # The two uprights, from the top of their feet to under the head.
    B.box("upright_l", (TUBE, TUBE, TOP - FOOT - TUBE), (-HALF, 0.0, FOOT + (TOP - FOOT - TUBE) / 2), neon),
    B.box("upright_r", (TUBE, TUBE, TOP - FOOT - TUBE), (HALF, 0.0, FOOT + (TOP - FOOT - TUBE) / 2), neon),
    # The head, running the full width so the corners close.
    B.box("head", (CLEAR + TUBE * 2, TUBE, TUBE), (0.0, 0.0, TOP - TUBE / 2), neon),
    # Feet: the frame is bolted to the deck, and a floating tube reads as a mistake.
    B.box("foot_l", (TUBE * 2.6, TUBE * 2.6, FOOT), (-HALF, 0.0, FOOT / 2), ink),
    B.box("foot_r", (TUBE * 2.6, TUBE * 2.6, FOOT), (HALF, 0.0, FOOT / 2), ink),
]

gate = B.join(parts, "bb_set_comp_gate")
X.link_only(gate, coll)
path = X.output_path("bb_set_comp_gate.fbx")
X.export_collection(coll, path)
X.report(coll, path)

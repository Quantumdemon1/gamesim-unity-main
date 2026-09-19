"""A competition podium: the prop the yard camera holds on when the competition cuts to three.

The same silhouette the primitive composition had - a wide plinth, an inset body, a lit front
panel, a brass reveal under an overhanging counter, and the buzzer - as one mesh at the block's
own footprint (2.2 x 1.3 m, standing 1.09 m). The lit panel is on the -y side (Unity -z): the
houseguest stands behind the counter facing the house, the panel faces the yard and the camera.
Origin at the floor under the centre. Furniture, so no collider; the scene's original block
keeps the collider the NavMesh was baked from.

    blender --background --python ArtSource/setpieces/bb_set_podium.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_podium")

ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
linen = X.material("coping_stone", (0.85, 0.82, 0.75), roughness=0.7)
blue = X.material("neon_blue", (0.20, 0.50, 1.00), roughness=0.4,
                  emission=(0.25, 0.55, 1.0), emission_strength=2.0)
red = X.material("neon_red", (0.95, 0.15, 0.12), roughness=0.4,
                 emission=(1.0, 0.18, 0.12), emission_strength=2.0)

W, D, H = 2.2, 1.3, 1.0
parts = [
    B.box("plinth", (W * 1.06, D * 1.12, H * 0.10), (0.0, 0.0, H * 0.05), ink),
    B.box("body", (W * 0.88, D * 0.82, H * 0.78), (0.0, 0.0, H * 0.49), ink),
    # Proud of the body's front face by a centimetre, so it never fights it.
    B.box("front_panel", (W * 0.66, 0.02, H * 0.34), (0.0, -D * 0.42, H * 0.52), blue),
    B.box("reveal", (W * 0.90, D * 0.86, H * 0.04), (0.0, 0.0, H * 0.86), brass),
    B.box("counter", (W * 1.02, D * 1.06, H * 0.08), (0.0, 0.0, H * 0.92), linen),
    B.cylinder("buzzer_stem", 0.035, 0.06, (0.0, D * 0.22, H * 0.96), brass, segments=16),
    B.cylinder("buzzer", 0.10, 0.07, (0.0, D * 0.22, H * 1.02), red, segments=24),
]
podium = B.join(parts, "bb_set_podium")
X.link_only(podium, coll)

path = X.output_path("bb_set_podium.fbx")
X.export_collection(coll, path)
X.report(coll, path)

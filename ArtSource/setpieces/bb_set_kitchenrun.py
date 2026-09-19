"""The kitchen run: one counter along the kitchen's north wall, from the fridge to the end panel.

Tier 3 begins with the kitchen. This one mesh replaces seven kit entries - the stove, the sink,
the cabinet, the bar end and the three appliances' floor spots - with the run every kitchen in
the format has: a fridge at one end, cabinet doors, the sink with its tap, a stack of drawers,
the stove with four hob rings and an oven door, more doors, and an end panel. 6.4 m long, 0.62 m
deep, 0.92 m to the counter; the fridge is 1.45 m, under the 1.5 m cutaway wall, which is why
there are no upper cabinets. The front is -y (Unity -z), so with the wall to the north the run
faces the room at yaw 0. Origin at the floor under the footprint's centre. Furniture, so no
collider; the small appliances the kit still supplies sit on the counter by their plan rows.

    blender --background --python ArtSource/setpieces/bb_set_kitchenrun.py -- <output.fbx>
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
import bb_build as B  # noqa: E402
import bb_export as X  # noqa: E402

X.fresh_scene()
coll = X.collection("bb_set_kitchenrun")

cream = X.material("cabinet_cream", (0.88, 0.85, 0.78), roughness=0.5)
stone = X.material("counter_stone", (0.22, 0.24, 0.27), roughness=0.3)
steel = X.material("steel_appliance", (0.72, 0.74, 0.77), metallic=0.65, roughness=0.35)
ink = X.material("ink", (0.08, 0.08, 0.10), roughness=0.55)
brass = X.material("frame_brass", (0.80, 0.65, 0.30), metallic=0.8, roughness=0.35)
glass = X.material("oven_glass", (0.05, 0.06, 0.08), roughness=0.15)

L, D, H = 6.4, 0.62, 0.92
TOP = 0.04
x0 = -L / 2
# Sections along x, left to right: (name, width)
sections = [("fridge", 0.8), ("doors_a", 1.2), ("sink", 0.9), ("drawers", 0.9), ("stove", 0.9), ("doors_b", 1.2), ("end", 0.5)]
parts = []
x = x0
front = -D / 2   # the face toward the room


def span(name):
    left = x0
    for n, w in sections:
        if n == name:
            return left, left + w
        left += w
    raise KeyError(name)


# Plinth, carcass and counter, in three counter slabs with the sink between.
parts.append(B.box("plinth", (L, D - 0.08, 0.10), (0.0, 0.04, 0.05), ink))
parts.append(B.box("carcass", (L, D, H - TOP - 0.10), (0.0, 0.0, 0.10 + (H - TOP - 0.10) / 2), cream))
fl, fr = span("fridge")
sl, sr = span("sink")
parts.append(B.box("counter_left", (sl - fr, D + 0.02, TOP), ((fr + sl) / 2, 0.0, H - TOP / 2), stone))
parts.append(B.box("counter_right", (L / 2 - sr, D + 0.02, TOP), ((sr + L / 2) / 2, 0.0, H - TOP / 2), stone))
# The sink: counter strips front and back, the basin set lower between them, the tap behind.
parts.append(B.box("counter_sink_front", (sr - sl, 0.10, TOP), ((sl + sr) / 2, front + 0.05, H - TOP / 2), stone))
parts.append(B.box("counter_sink_back", (sr - sl, 0.14, TOP), ((sl + sr) / 2, D / 2 - 0.07, H - TOP / 2), stone))
parts.append(B.box("counter_sink_left", (0.08, D + 0.02, TOP), (sl + 0.04, 0.0, H - TOP / 2), stone))
parts.append(B.box("counter_sink_right", (0.08, D + 0.02, TOP), (sr - 0.04, 0.0, H - TOP / 2), stone))
parts.append(B.box("basin", (sr - sl - 0.16, D - 0.24, 0.16), ((sl + sr) / 2, -0.02, H - TOP - 0.08 - 0.10), steel))
parts.append(B.cylinder("tap_post", 0.018, 0.22, ((sl + sr) / 2, D / 2 - 0.09, H), brass, segments=12))
parts.append(B.box("tap_spout", (0.03, 0.18, 0.03), ((sl + sr) / 2, D / 2 - 0.18, H + 0.20), brass))

# The fridge: a taller box with two doors and long handles.
parts.append(B.box("fridge", (fr - fl - 0.02, D, 1.45), ((fl + fr) / 2, 0.0, 1.45 / 2), steel))
parts.append(B.box("fridge_seam", (fr - fl - 0.06, 0.01, 0.01), ((fl + fr) / 2, front - 0.005, 0.95), ink))
parts.append(B.box("fridge_handle_top", (0.03, 0.03, 0.34), (fr - 0.10, front - 0.03, 1.20), brass))
parts.append(B.box("fridge_handle_bottom", (0.03, 0.03, 0.34), (fr - 0.10, front - 0.03, 0.62), brass))

# Cabinet doors: two per doors section, proud of the carcass, with knobs.
for name in ("doors_a", "doors_b"):
    l, r = span(name)
    w = (r - l) / 2
    for i in range(2):
        cx = l + w * (i + 0.5)
        parts.append(B.box(name + "_%d" % i, (w - 0.03, 0.02, H - TOP - 0.16), (cx, front - 0.01, 0.10 + (H - TOP - 0.10) / 2), cream))
        parts.append(B.cylinder(name + "_knob_%d" % i, 0.015, 0.03, (cx + (0.06 if i == 0 else -0.06), 0.0, 0.0), brass, segments=8))
        knob = parts[-1]
        # A knob is a short cylinder standing out of the door: rotate it to point -y and place it.
        import math  # noqa: E402
        from mathutils import Matrix, Vector  # noqa: E402
        knob.data.transform(Matrix.Rotation(math.radians(90.0), 4, 'X'))
        knob.data.transform(Matrix.Translation(Vector((0.0, front - 0.02, H * 0.55))))

# Drawers: three fronts stacked, each with a bar handle.
dl, dr = span("drawers")
for i in range(3):
    z = 0.12 + i * (H - TOP - 0.14) / 3
    h = (H - TOP - 0.14) / 3 - 0.02
    parts.append(B.box("drawer_%d" % i, (dr - dl - 0.03, 0.02, h), ((dl + dr) / 2, front - 0.01, z + h / 2), cream))
    parts.append(B.box("drawer_handle_%d" % i, (0.24, 0.02, 0.02), ((dl + dr) / 2, front - 0.03, z + h / 2), brass))

# The stove: a black hob slab on the counter with four rings, and the oven door below.
vl, vr = span("stove")
parts.append(B.box("hob", (vr - vl - 0.06, D - 0.08, 0.015), ((vl + vr) / 2, 0.0, H + 0.0075), ink))
for i, (ox, oy) in enumerate([(-0.2, -0.13), (0.2, -0.13), (-0.2, 0.13), (0.2, 0.13)]):
    parts.append(B.ring("hob_ring_%d" % i, 0.10, 0.075, 0.008, ((vl + vr) / 2 + ox, oy, H + 0.015), brass, segments=24))
parts.append(B.box("oven_door", (vr - vl - 0.03, 0.02, 0.44), ((vl + vr) / 2, front - 0.01, 0.42), ink))
parts.append(B.box("oven_window", (vr - vl - 0.20, 0.01, 0.22), ((vl + vr) / 2, front - 0.025, 0.44), glass))
parts.append(B.box("oven_handle", (vr - vl - 0.16, 0.03, 0.03), ((vl + vr) / 2, front - 0.05, 0.66), brass))
parts.append(B.box("stove_dials", (0.30, 0.02, 0.04), ((vl + vr) / 2, front - 0.01, H - TOP - 0.07), brass))

# The end panel closes the run.
el, er = span("end")
parts.append(B.box("end_panel", (er - el, D, H - TOP), ((el + er) / 2, 0.0, (H - TOP) / 2), cream))

run = B.join(parts, "bb_set_kitchenrun")
X.link_only(run, coll)

path = X.output_path("bb_set_kitchenrun.fbx")
X.export_collection(coll, path)
X.report(coll, path)

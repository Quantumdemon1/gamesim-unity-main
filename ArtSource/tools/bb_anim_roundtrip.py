"""Gate 0: does a take survive bb_anim's import and export unchanged?

Loads a shipped Mixamo take, exports it untouched through bb_anim.export, imports the result into a
fresh scene and compares the two, bone by bone, at every 13th frame. Optionally also writes Gate
0b's base take (base() held 4 s, breathing with a 4 s period at depth 1) for the Unity probe.

    blender --background --factory-startup --python ArtSource/tools/bb_anim_roundtrip.py -- \
        <source.fbx> <out.fbx> [config A|B|C] [<base_out.fbx>]

Pass: 65 bones; the action spans 0..118 at 30 fps (for TalkB); every pose bone's world matrix at
frames 0, 13, ... within 0.1 degrees and 1 mm. Prints GATE0 PASS or GATE0 FAIL with the maxima.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_anim  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
source, out = argv[0], argv[1]
config = argv[2] if len(argv) > 2 else bb_anim.CONFIG
base_out = argv[3] if len(argv) > 3 else None


def record(armature, action):
    scene = bpy.context.scene
    ad = armature.animation_data
    ad.action = action
    if hasattr(ad, "action_slot") and ad.action_slot is None and len(action.slots) > 0:
        ad.action_slot = action.slots[0]
    start, end = (int(v) for v in action.frame_range)
    frames = list(range(start, end + 1, 13))
    rows = {}
    for f in frames:
        scene.frame_set(f)
        bpy.context.view_layer.update()
        rows[f] = {pb.name: (armature.matrix_world @ pb.matrix).copy() for pb in armature.pose.bones}
    return {"bones": len(armature.data.bones), "range": (start, end), "frames": frames, "rows": rows,
            "world": armature.matrix_world.copy(), "fps": scene.render.fps}


bb_anim.fresh_scene()
arm_a, act_a = bb_anim.import_fbx(source)
a = record(arm_a, act_a)
print("SOURCE bones %d range %s fps %d" % (a["bones"], a["range"], a["fps"]))
print("SOURCE world", [tuple(round(v, 5) for v in r) for r in a["world"]])
rig = bb_anim.Rig(arm_a, {os.path.splitext(os.path.basename(source))[0]: act_a})
bb_anim.export(rig, act_a, out, config)

bb_anim.fresh_scene()
arm_b, act_b = bb_anim.import_fbx(out)
b = record(arm_b, act_b)
print("ROUNDTRIP bones %d range %s fps %d" % (b["bones"], b["range"], b["fps"]))
print("ROUNDTRIP world", [tuple(round(v, 5) for v in r) for r in b["world"]])
print("ROUNDTRIP objects", [(o.name, o.type, o.parent.name if o.parent else None) for o in bpy.data.objects])

worst_deg = worst_mm = 0.0
worst_at = None
for f in a["frames"]:
    if f not in b["rows"]:
        print("MISSING frame", f)
        continue
    for name, ma in a["rows"][f].items():
        mb = b["rows"][f].get(name)
        if mb is None:
            print("MISSING bone", name)
            worst_deg = 999.0
            continue
        mm = (ma.translation - mb.translation).length * 1000.0
        deg = bb_anim.angle_between(ma.to_quaternion(), mb.to_quaternion())
        if deg > worst_deg or mm > worst_mm:
            worst_at = (f, name)
        worst_deg = max(worst_deg, deg)
        worst_mm = max(worst_mm, mm)
ok = (a["bones"] == 65 and b["bones"] == 65 and a["range"] == b["range"] and worst_deg <= 0.1 and worst_mm <= 1.0)
print("GATE0 %s config %s: bones %d/%d, range %s/%s, max %.4f deg, %.4f mm (worst at %s)"
      % ("PASS" if ok else "FAIL", config, a["bones"], b["bones"], a["range"], b["range"], worst_deg, worst_mm, worst_at))

if base_out:
    rig = bb_anim.load()
    base = rig.base()
    take = bb_anim.Take(rig, "Gate0Base_loop", 120, exit_at=120)
    take.key(0, base)
    take.key(120, base)
    bb_anim.breathe(take, 0, 120, 4.0, 1.0)
    act = take.bake()
    bb_anim.export(rig, act, base_out, config)

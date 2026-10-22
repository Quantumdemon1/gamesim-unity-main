"""NEW grounded noncontact farewell: open right palm, warm nod, base again.

30 frames/30fps; no hips/legs/root/visual translation and no invented contact.
Uses reviewed bb_anim anatomical keying and unchanged story rules before export.
External-only candidate. No Blender/native execution is authorized by this source.
"""
import argparse
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
FRAMES, EXIT_AT = 30, 29
PAIR_GAPS = (.6, .9, 1.2)
GREETING_START, GREETING_END = 10, 17
MAX_GROUND_DRIFT = 1e-6


def arguments():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", help="Absolute absent bb_anim_React_farewell.fbx")
    parser.add_argument("--tools-root", type=Path)
    parser.add_argument("--check-only", action="store_true")
    options = parser.parse_args(argv)
    output = Path(options.output)
    if not output.is_absolute() or output.name != "bb_anim_React_farewell.fbx":
        raise AssertionError("Explicit absolute exact-named new output required")
    if not output.parent.is_dir() or output.exists():
        raise AssertionError("Existing output directory and absent new FBX required")
    report = output.parent / "farewell-grounded-frame-report.json"
    if report.exists():
        raise AssertionError("New report required")
    tools = (options.tools_root or Path(__file__).resolve().parent.parent / "tools").resolve(strict=True)
    if not tools.is_dir() or not (tools / "bb_anim.py").is_file():
        raise AssertionError("Actual reviewed bb_anim tools required")
    resolved = output.resolve()
    project = tools.parent.parent
    if resolved == project or project in resolved.parents:
        raise AssertionError("External staging never writes under the tools' project")
    return options, tools, resolved, report.resolve()


def build(bb_anim):
    from bb_anim import F, L, U

    rig = bb_anim.load()
    base = rig.base()
    take = bb_anim.Take(rig, "React_farewell", FRAMES, exit_at=EXIT_AT)
    take.key(0, base)
    # Existing safe route round the torso, not a hand moving through it.
    take.leave_base(0, 5)
    take.key(5, rig.hang())
    greeting = rig.hang_with({"Spine1": {"flex": 1}, "Spine2": {"flex": 1}},
                             {"RightFingers": (10, 8, 5), "RightThumb": (10, 10, 10)})
    shoulder = rig.head(rig.fk(greeting), "RightArm")
    target = shoulder - L * rig.m(.12) + F * rig.m(.15) + U * rig.m(.06)
    greeting = rig.reach(greeting, "Right", target,
                         pole=(-U - L * .6).normalized(), palm=F)
    take.key(GREETING_START, greeting)
    take.key(GREETING_END, greeting)
    take.hold(GREETING_START, GREETING_END)
    # Brief nod and a gentle living settle. Nothing below the upper spine moves.
    take.curve("Head", "flex", [(0, 0), (10, 0), (14, 3), (17, 0), (30, 0)])
    take.curve("Spine2", "flex", [(0, 0), (10, 0), (14, .6), (17, 0), (30, 0)])
    take.micro(5, 28)
    take.key(19, greeting)
    take.return_to_base(19, EXIT_AT)
    take.key(FRAMES, base)
    return rig, take, take.bake()


def sample_pose(rig, act, frame):
    import bpy

    rig.assign(act)
    whole = math.floor(frame)
    bpy.context.scene.frame_set(whole, subframe=frame - whole)
    bpy.context.view_layer.update()
    return {bone.name: (bone.matrix_basis.decompose()[0].copy(),
                        bone.matrix_basis.decompose()[1].copy())
            for bone in rig.armature.pose.bones}


def vector(value):
    return [float(component) for component in value]


def observe(bb_anim, rig, act):
    from bb_anim import F, L, U
    from mathutils import Vector

    base_mats = rig.fk(rig.base())
    world = rig.armature.matrix_world.copy()
    if not math.isfinite(rig.unit) or rig.unit <= 0 or bb_anim.FPS != 30:
        raise AssertionError("Finite positive source units and actual30fps required")
    if any(not math.isfinite(float(value)) for row in world for value in row):
        raise AssertionError("Nonfinite source armature world transform")
    protected = ["Hips"] + [side + part for side in ("Left", "Right")
                           for part in ("UpLeg", "Leg", "Foot", "ToeBase", "Toe_End")]
    foot_names = [side + part for side in ("Left", "Right") for part in ("Foot", "ToeBase", "Toe_End")]
    base_world = {name: world @ base_mats[bb_anim.full(name)] for name in protected}
    base_feet = {name: base_world[name].translation.copy() for name in foot_names}
    rows, failures = [], []
    for quarter in range(FRAMES * 4 + 1):
        frame = quarter / 4.0
        pose = sample_pose(rig, act, frame)
        mats = rig.fk(pose)
        for matrix in mats.values():
            if any(not math.isfinite(float(value)) for row in matrix for value in row):
                raise AssertionError("Nonfinite actual posed matrix at f%.2f" % frame)
        drift = max(((world @ mats[bb_anim.full(name)]).translation - base_world[name].translation).length
                    for name in protected)
        matrix_delta = max(abs(float((world @ mats[bb_anim.full(name)])[r][c] - base_world[name][r][c]))
                           for name in protected for r in range(4) for c in range(4))
        feet = {name: vector((world @ mats[bb_anim.full(name)]).translation) for name in foot_names}
        foot_drift = max(((world @ mats[bb_anim.full(name)]).translation - base_feet[name]).length for name in foot_names)
        if drift > MAX_GROUND_DRIFT or foot_drift > MAX_GROUND_DRIFT or matrix_delta > MAX_GROUND_DRIFT:
            failures.append("f%.2f grounded base changed %.9g/%.9g/%.9g" % (frame, drift, foot_drift, matrix_delta))
        landmarks = rig.landmarks(pose)
        shoulder = rig.head(mats, "RightArm")
        target = shoulder - L * rig.m(.12) + F * rig.m(.15) + U * rig.m(.06)
        right = rig.palm(mats, "Right")
        reach_error = (right - target).length * rig.unit
        palm_forward = rig.palm_normal(mats, "Right").dot(F)
        palm_lift = (right - shoulder).dot(U) * rig.unit
        if GREETING_START <= frame <= GREETING_END:
            if reach_error > .025 or palm_forward < .75 or palm_lift < .035:
                failures.append("f%.2f greeting target/palm/lift %.6f/%.6f/%.6f" %
                                (frame, reach_error, palm_forward, palm_lift))
        pair_rows = []
        for gap in PAIR_GAPS:
            def partner(point):
                return Vector((-point.x, point.y, -point.z)) + F * rig.m(gap)
            points = [rig.palm(mats, side) for side in ("Left", "Right")]
            points += [rig.head(mats, side + part) for side in ("Left", "Right")
                       for part in ("Hand", "HandMiddle4", "HandIndex4", "HandThumb4")]
            torso = min(bb_anim.seg_dist(point, partner(rig.head(mats, "Hips")),
                                        partner(rig.head(mats, "Neck"))) * rig.unit - .10 for point in points)
            head = min((point - partner(landmarks["C"])).length * rig.unit - .10 for point in points)
            arm = min(bb_anim.seg_seg_dist(rig.head(mats, side_a + a), rig.head(mats, side_a + b),
                                          partner(rig.head(mats, side_b + c)), partner(rig.head(mats, side_b + d)))
                      * rig.unit - .075
                      for side_a in ("Left", "Right") for side_b in ("Left", "Right")
                      for a, b in (("Arm", "ForeArm"), ("ForeArm", "Hand"))
                      for c, d in (("Arm", "ForeArm"), ("ForeArm", "Hand")))
            hands = min((a - partner(b)).length * rig.unit for a in points for b in points)
            if arm < -.005 or torso < 0 or head < 0 or hands < .05:
                failures.append("f%.2f gap%.1f clearance arm/torso/head/hands %.6f/%.6f/%.6f/%.6f" %
                                (frame, gap, arm, torso, head, hands))
            pair_rows.append({"rootGapMetres": gap, "armGapMetres": arm, "torsoGapMetres": torso,
                              "handHeadGapMetres": head, "handHandMetres": hands, "contactClaimed": False})
        row = {"frame": frame, "hipsLegMatrixDelta": matrix_delta, "protectedPositionDriftMetres": drift,
               "footWorldDriftMetres": foot_drift, "feetWorld": feet, "palmTargetErrorMetres": reach_error,
               "palmForwardDot": palm_forward, "palmLiftMetres": palm_lift, "pairs": pair_rows}
        rows.append(row)
    # allow_nan=False below also refuses any derived nonfinite scalar before publication/export.
    if failures:
        raise AssertionError("Grounded farewell refused (%d): %s" % (len(failures), "; ".join(failures[:32])))
    if len(rows) != 121 or sum(len(row["pairs"]) for row in rows) != 363:
        raise AssertionError("Complete sample inventory required")
    return rows


def main():
    options, tools, output, report = arguments()
    sys.path.insert(0, str(tools))
    import bb_anim

    rig, take, act = build(bb_anim)
    original_rules = rig.check(act, take)
    rows = observe(bb_anim, rig, act)
    proof = {"schema": 1, "kind": "authored-grounded-noncontact-farewell", "take": take.name,
             "fps": bb_anim.FPS, "first": 0, "last": FRAMES, "exit": EXIT_AT,
             "frameGeometryPassed": True, "contactClaimed": False, "visualRootTranslationMetres": 0,
             "nativeRetargetingAccepted": False, "visualAccepted": False,
             "exportRequested": not options.check_only, "originalStoryRules": original_rules,
             "quarterFrameSamples": rows}
    encoded = (json.dumps(proof, indent=2, sort_keys=True, allow_nan=False) + "\n").encode("utf-8")
    if len(encoded) > 2 * 1024 * 1024:
        raise AssertionError("Report exceeds 2MiB")
    with report.open("xb") as stream:
        stream.write(encoded)
    if not options.check_only:
        bb_anim.export(rig, act, str(output))
    print("GROUNDED FAREWELL FRAME GEOMETRY PASS: 121 samples / 363 pair checks; native and visual pending")


if __name__ == "__main__":
    main()

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>Authored muscle animation on the existing Humanoid rig, independent of UMA assets.</summary>
    public static class HumanoidReactionAuthoring
    {
        public static readonly string[] Takes = { "Listen_loop", "React_nominated", "React_saved", "React_evicted" };

        /// <summary>
        /// The pose every authored clip is built on: the at-ease stance, as its living pose holds it
        /// (the still's first frame, facing forward, with the head turned to the chest).
        ///
        /// <para>They were built on the talk take's first frame, and that take was captured sitting:
        /// its root at 0.675 of standing height and its lower legs at 0.22 and 0.33. So every
        /// listener and every nominee sat down on nothing wherever they stood, which was the
        /// "sitting in the middle of the room" the owner saw (PACK8-PASS-PLAN A2). The at-ease
        /// pose stands at 0.978 with its lower legs at 1.04 and 1.00, read from its clip, and its
        /// arms hang at its sides rather than working a gesture.</para>
        /// </summary>
        public const string Source = "PoseAtEase_loop";

        /// <summary>
        /// Batch entry for the lead's regeneration on a Unity copy:
        /// <c>-executeMethod Gamesim.Editor.HumanoidReactionAuthoring.BuildFromCommandLine</c>.
        /// Re-authors the four clips from <see cref="Source"/> and rebuilds the controller over them
        /// (<see cref="HumanoidClipWiring.Apply"/>). A failure throws, which fails the batch run.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            Apply();
            Debug.Log("[Gamesim] humanoid · " + string.Join(", ", Takes) + " authored from " + Source
                + " and the controller rebuilt at " + HumanoidClipWiring.Controller + ".");
        }

        [MenuItem("Gamesim/Characters/Author missing humanoid reactions")]
        public static void Apply()
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(HumanoidClipWiring.Path(Source))
                .OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview", StringComparison.Ordinal));
            if (source == null || !source.humanMotion)
                throw new InvalidOperationException("The standing pose " + HumanoidClipWiring.Path(Source)
                    + " is required; the living poses are built by Gamesim > Characters > Author the living poses.");
            var pose = HumanoidPoseAuthoring.Frame(source);
            // Held here as well as by the controller's test: a source that sits would sit every
            // listener and every nominee again, and nothing else would say so until a playtest.
            pose.TryGetValue("RootT.y", out float height);
            pose.TryGetValue("Left Lower Leg Stretch", out float left);
            pose.TryGetValue("Right Lower Leg Stretch", out float right);
            if (height < HumanoidClipWiring.StandingRootHeight || Mathf.Max(left, right) < HumanoidClipWiring.StraightLowerLeg)
                throw new InvalidOperationException(Source + " does not stand (root " + height.ToString("0.000")
                    + ", lower legs " + left.ToString("0.00") + " and " + right.ToString("0.00") + "); the reactions need a standing source.");
            FaceForward(pose);
            var clips = new[]
            {
                Build("Listen_loop", 3f, pose, new Dictionary<string, float>
                { ["Head Nod Down-Up"] = -.06f, ["Head Turn Left-Right"] = .08f, ["Spine Left-Right"] = .025f }),
                Build("React_nominated", 1.7f, pose, new Dictionary<string, float>
                { ["Head Nod Down-Up"] = -.24f, ["Chest Front-Back"] = -.08f,
                    ["Left Shoulder Front-Back"] = -.12f, ["Right Shoulder Front-Back"] = -.12f }),
                Build("React_saved", 1.7f, pose, new Dictionary<string, float>
                { ["Head Nod Down-Up"] = .14f, ["Chest Front-Back"] = .10f,
                    ["Left Arm Down-Up"] = .28f, ["Right Arm Down-Up"] = .28f,
                    ["Left Shoulder Down-Up"] = .1f, ["Right Shoulder Down-Up"] = .1f }),
                Build("React_evicted", 2.2f, pose, new Dictionary<string, float>
                { ["Head Nod Down-Up"] = -.35f, ["Chest Front-Back"] = -.15f,
                    ["Spine Front-Back"] = -.12f, ["Left Shoulder Down-Up"] = -.13f,
                    ["Right Shoulder Down-Up"] = -.13f })
            };
            foreach (var clip in clips)
            {
                string path = HumanoidClipWiring.ClipFolder + "bb_anim_" + clip.name + ".anim";
                // The asset is named by its file. CopySerialized copies the name with everything
                // else, and a re-run used to rename every clip to its bare take.
                clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(clip); }
            }
            AssetDatabase.SaveAssets();
            HumanoidClipWiring.Apply();
        }

        /// <summary>
        /// A take's first frame carries the Mixamo file's heading - the talk take's was about 164
        /// degrees from forward, and every reaction built on it played facing away from the room.
        /// The body keeps its lean and loses its turn, and stands where its root is. The living
        /// poses are faced forward the same way when they are authored, so on them this changes
        /// nothing and is kept for any source that is not.
        /// </summary>
        public static void FaceForward(IDictionary<string, float> pose)
        {
            if (pose.TryGetValue("RootQ.x", out var x) && pose.TryGetValue("RootQ.y", out var y)
                && pose.TryGetValue("RootQ.z", out var z) && pose.TryGetValue("RootQ.w", out var w))
            {
                var body = new Quaternion(x, y, z, w);
                var ahead = body * Vector3.forward;
                ahead.y = 0f;
                if (ahead.sqrMagnitude > 1e-6f) body = Quaternion.Inverse(Quaternion.LookRotation(ahead.normalized)) * body;
                pose["RootQ.x"] = body.x; pose["RootQ.y"] = body.y; pose["RootQ.z"] = body.z; pose["RootQ.w"] = body.w;
            }
            if (pose.ContainsKey("RootT.x")) pose["RootT.x"] = 0f;
            if (pose.ContainsKey("RootT.z")) pose["RootT.z"] = 0f;
        }

        private static AnimationClip Build(string name, float duration, Dictionary<string, float> pose, Dictionary<string, float> offsets)
        {
            var clip = new AnimationClip { name = name, frameRate = 30f };
            bool loop = name.EndsWith("_loop", StringComparison.Ordinal);
            foreach (var entry in pose)
            {
                float delta = offsets.TryGetValue(entry.Key, out var change) ? change : 0f;
                float peak = Mathf.Clamp(entry.Value + delta, -1f, 1f);
                var curve = delta == 0f ? AnimationCurve.Constant(0f, duration, entry.Value)
                    : new AnimationCurve(new Keyframe(0f, entry.Value), new Keyframe(duration * .28f, peak),
                        new Keyframe(duration * .55f, loop ? entry.Value - delta * .35f : peak), new Keyframe(duration, entry.Value));
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), entry.Key), curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            // The same rule the mocap takes import with: the root turns with the body and stays put.
            settings.loopTime = loop; settings.loopBlend = loop;
            settings.loopBlendOrientation = true; settings.keepOriginalOrientation = false;
            settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }
    }
}

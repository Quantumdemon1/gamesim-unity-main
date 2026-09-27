using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// The standing poses, brought to life.
    ///
    /// <para>Each arrives from Mixamo as a still: two keys, one frame. Held on camera for longer
    /// than a beat, a body on one frame is a shop dummy. So each still is authored into a loop of
    /// its own here: the chest and spine breathe, the shoulders rise with them, and the head
    /// settles a hair. The stills also turn their heads - the female set 28 to 46 degrees away from
    /// the way the body faces - and a pose struck for the lens with the face turned from it is a
    /// photograph of an ear, so each keeps only the share of its head turn its row says. The look
    /// over the shoulder keeps all of it: the look is the pose.</para>
    ///
    /// <para>Played by the controller's pose states, chosen by <c>Pose</c>, one row each; the order
    /// is <c>CharacterPresentation.Pose</c>'s and is appended to, never reordered.</para>
    /// </summary>
    public static class HumanoidPoseAuthoring
    {
        /// <summary>
        /// Each living pose: the loop, the still it is built from, and how much of the still's head
        /// turn it keeps (one is all of it, nought faces the way the chest does).
        /// </summary>
        public static readonly (string take, string still, float keepTurn)[] Poses =
        {
            ("PoseHandBehindHead_loop", "PoseHandBehindHead", 0f),
            ("PoseFootUp_loop", "PoseFootUp", 0f),
            ("PoseOverShoulder_loop", "PoseOverShoulder", 1f),
            ("PoseAtEase_loop", "PoseAtEase", 0f),
            ("PoseHandOnHip_loop", "PoseHandOnHip", 0f),
            ("PoseHandOnHipGlance_loop", "PoseHandOnHipGlance", .35f),
            ("PosePowerStance_loop", "PosePowerStance", 0f),
        };

        public static readonly string[] Takes = Poses.Select(p => p.take).ToArray();

        /// <summary>One breath, in seconds: in over the first half, out over the second.</summary>
        public const float Breath = 4f;

        /// <summary>
        /// How far each muscle moves at the top of a breath, in muscle units (about a degree per
        /// fiftieth): enough to read as a body that is alive, too little to read as a gesture.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, float> Breathing = new Dictionary<string, float>
        {
            ["Chest Front-Back"] = .03f,
            ["Spine Front-Back"] = .015f,
            ["Left Shoulder Down-Up"] = .02f,
            ["Right Shoulder Down-Up"] = .02f,
            ["Head Nod Down-Up"] = .01f,
        };

        /// <summary>The head's turn, which a pose for the lens gives up.</summary>
        public static readonly string[] HeadTurn = { "Head Turn Left-Right", "Neck Turn Left-Right" };

        [MenuItem("Gamesim/Characters/Author the living poses")]
        public static void Apply()
        {
            foreach (var (take, still, keepTurn) in Poses)
            {
                var source = AssetDatabase.LoadAllAssetsAtPath(HumanoidClipWiring.Path(still)).OfType<AnimationClip>()
                    .FirstOrDefault(clip => !clip.name.StartsWith("__preview", StringComparison.Ordinal));
                if (source == null || !source.humanMotion)
                    throw new InvalidOperationException("No Humanoid still at " + HumanoidClipWiring.Path(still));
                var pose = Frame(source);
                HumanoidReactionAuthoring.FaceForward(pose);
                foreach (var muscle in HeadTurn)
                    if (pose.ContainsKey(muscle)) pose[muscle] *= keepTurn;

                var clip = Build(pose);
                string path = HumanoidClipWiring.Path(take);
                clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(clip); }
            }
            AssetDatabase.SaveAssets();
            HumanoidClipWiring.Apply();
        }

        /// <summary>A still's one frame, as muscle values and the body's root.</summary>
        public static Dictionary<string, float> Frame(AnimationClip still)
        {
            var names = new HashSet<string>(HumanTrait.MuscleName);
            var pose = AnimationUtility.GetCurveBindings(still).Where(binding => binding.type == typeof(Animator)
                    && (names.Contains(binding.propertyName) || binding.propertyName.StartsWith("RootT.", StringComparison.Ordinal)
                        || binding.propertyName.StartsWith("RootQ.", StringComparison.Ordinal)))
                .ToDictionary(binding => binding.propertyName, binding => AnimationUtility.GetEditorCurve(still, binding).Evaluate(0f));
            if (!pose.ContainsKey("Head Nod Down-Up"))
                throw new InvalidOperationException(still.name + " does not expose Humanoid muscle curves.");
            return pose;
        }

        private static AnimationClip Build(Dictionary<string, float> pose)
        {
            var clip = new AnimationClip { frameRate = 30f };
            foreach (var entry in pose)
            {
                AnimationCurve curve;
                if (Breathing.TryGetValue(entry.Key, out var depth))
                {
                    // In and out once a loop, easing at both ends, and back exactly where it began.
                    curve = new AnimationCurve(new Keyframe(0f, entry.Value), new Keyframe(Breath * .5f, Mathf.Clamp(entry.Value + depth, -1f, 1f)),
                        new Keyframe(Breath, entry.Value));
                    for (int i = 0; i < curve.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    }
                }
                else curve = AnimationCurve.Constant(0f, Breath, entry.Value);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), entry.Key), curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            // The rule the mocap takes import with: the root turns with the body, and the body stays
            // over the root, at its own height.
            settings.loopTime = true; settings.loopBlend = true;
            settings.loopBlendOrientation = true; settings.keepOriginalOrientation = false;
            settings.loopBlendPositionXZ = true; settings.loopBlendPositionY = true;
            settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }
    }
}

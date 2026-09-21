using Gamesim.Presentation;
using UMA.CharacterSystem;
using UMA.PoseTools;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// The face a UMA houseguest wears, from the two words the simulation already keeps about them.
    ///
    /// <para><see cref="FaceExpression"/> gives the low-poly bodies an expression by moving the
    /// vertices of two eye clusters, because a Quaternius head has no brows and no mouth to move. A
    /// UMA head has both, and UMA ships a player for them: <see cref="UMAExpressionPlayer"/> drives
    /// fifty-one pose channels on the built skeleton. So the same <c>mood</c> × <c>stressLevel</c>
    /// that squints an eye cluster over there lowers a brow and turns a mouth over here, and the
    /// eye-cluster path is simply left with nothing to bind to — a UMA body has no "Face" material,
    /// which is the condition that already made <see cref="FaceExpression"/> a no-op on one.</para>
    ///
    /// <para>Mood chooses the shape; stress colours it. They are separate because the simulation
    /// keeps them separately: a houseguest can be happy and about to come apart, and a face that
    /// could only be one of those would be lying about the week they are having.</para>
    ///
    /// <para>Reduced motion holds the face still — at rest, not at the current mood. A face is the
    /// smallest motion a body makes and the hardest to look away from, and the setting exists for
    /// people who need the screen to stop moving.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UmaExpressions : MonoBehaviour
    {
        /// <summary>
        /// A face as six numbers, in UMA's own units: −1 to 1 for the paired channels, 0 to 1 for
        /// the ones that only go one way. Public and pure so the mapping can be asserted without
        /// waiting on UMA to assemble a body.
        /// </summary>
        public struct FacePose
        {
            /// <summary>Mouth corners. Positive smiles, negative frowns.</summary>
            public float Smile;
            /// <summary>Brows as a pair. Positive raises, negative lowers.</summary>
            public float BrowUp;
            /// <summary>Inner brows drawn together: the tension channel, 0 to 1.</summary>
            public float BrowsIn;
            /// <summary>Lids. Positive widens, negative narrows.</summary>
            public float EyeOpen;
            /// <summary>Nose, 0 to 1. Only anger and real strain reach for it.</summary>
            public float Sneer;
            /// <summary>Jaw. Positive opens; the lip flap rides on this one.</summary>
            public float JawOpen;

            /// <summary>The largest channel, for "did this face change at all" questions.</summary>
            public float Magnitude => Mathf.Max(
                Mathf.Max(Mathf.Abs(Smile), Mathf.Abs(BrowUp)),
                Mathf.Max(Mathf.Max(BrowsIn, Mathf.Abs(EyeOpen)), Mathf.Max(Sneer, Mathf.Abs(JawOpen))));
        }

        /// <summary>How fast a face settles onto a new mood. Slower than a blink, faster than a beat.</summary>
        private const float Ease = 7f;
        /// <summary>The lip flap: a jaw that opens and closes about twice a second while speaking.</summary>
        private const float FlapRate = 11f, FlapDepth = 0.22f, FlapFloor = 0.06f;

        private CharacterPresentation presentation;
        private DynamicCharacterAvatar avatar;
        private UMAExpressionPlayer player;
        private FacePose current;
        private float flapPhase;
        private bool held;

        /// <summary>The face this body is currently showing. For the tests and the previews.</summary>
        public FacePose Current => current;

        /// <summary>The expression player this body drives, once UMA has given it one.</summary>
        public UMAExpressionPlayer Player => player;

        /// <summary>
        /// The pose for a pair of the simulation's words.
        ///
        /// <para>Every word here is one of the five moods or five stress levels
        /// <c>EpisodeFinaleValidation</c> insists a houseguest carries, and an unknown word is a
        /// neutral face rather than an exception: a face is not worth throwing over.</para>
        /// </summary>
        public static FacePose For(string mood, string stress)
        {
            var pose = new FacePose();
            switch (mood)
            {
                case "Angry":
                    pose.Smile = -0.55f; pose.BrowUp = -0.60f; pose.BrowsIn = 0.70f; pose.Sneer = 0.30f;
                    pose.EyeOpen = -0.15f;
                    break;
                case "Upset":
                    // The inner brows go up, not down. That single difference is what separates a
                    // face that is hurt from one that is furious.
                    pose.Smile = -0.45f; pose.BrowUp = 0.35f; pose.BrowsIn = 0.30f; pose.EyeOpen = -0.25f;
                    break;
                case "Content":
                    pose.Smile = 0.35f; pose.BrowUp = 0.10f;
                    break;
                case "Happy":
                    pose.Smile = 0.85f; pose.BrowUp = 0.30f; pose.EyeOpen = -0.20f;
                    break;
            }

            switch (stress)
            {
                case "Relaxed":
                    pose.EyeOpen -= 0.15f; pose.BrowsIn -= 0.10f;
                    break;
                case "Tense":
                    pose.BrowsIn += 0.35f; pose.EyeOpen += 0.15f;
                    break;
                case "Stressed":
                    pose.BrowsIn += 0.55f; pose.BrowUp -= 0.20f; pose.EyeOpen += 0.30f; pose.Sneer += 0.20f;
                    break;
                case "Overwhelmed":
                    // Wide and lifted, and the jaw goes slack. Nothing subtle about it.
                    pose.BrowsIn += 0.25f; pose.BrowUp += 0.45f; pose.EyeOpen += 0.65f; pose.JawOpen += 0.18f;
                    break;
            }

            pose.Smile = Mathf.Clamp(pose.Smile, -1f, 1f);
            pose.BrowUp = Mathf.Clamp(pose.BrowUp, -1f, 1f);
            pose.BrowsIn = Mathf.Clamp01(pose.BrowsIn);
            pose.EyeOpen = Mathf.Clamp(pose.EyeOpen, -1f, 1f);
            pose.Sneer = Mathf.Clamp01(pose.Sneer);
            pose.JawOpen = Mathf.Clamp(pose.JawOpen, -1f, 1f);
            return pose;
        }

        private void Awake()
        {
            presentation = GetComponentInParent<CharacterPresentation>();
            avatar = GetComponent<DynamicCharacterAvatar>();
            if (avatar == null) return;

            // Added now, before the avatar's own Start, because that is where UMA hands the player
            // the race's expression set: SetExpressionSet only fills in a player that already
            // exists unless it is asked to add one, and by the time the character is built it is
            // too late to be found.
            //
            // Added asleep, though. The player's own Start subscribes to the avatar's character
            // events, and on a body whose avatar has not run its Start yet those events do not
            // exist - which threw a null reference out of UMA's own Initialize on every cast screen
            // that built a season. It is woken below, once the avatar has its data.
            player = avatar.gameObject.GetComponent<UMAExpressionPlayer>();
            if (player == null)
            {
                player = avatar.gameObject.AddComponent<UMAExpressionPlayer>();
                player.enabled = false;
            }
        }

        /// <summary>
        /// Whether the player has been woken. UMA's own initialisation reads the avatar's events and
        /// its data, so it waits for a body that exists rather than one that has been asked for.
        /// </summary>
        private bool Ready()
        {
            if (player == null) return false;
            if (player.enabled) return true;
            if (avatar == null || avatar.umaData == null) return false;
            player.enabled = true;
            return false;
        }

        private void LateUpdate()
        {
            if (!Ready()) return;

            if (presentation != null && presentation.ReducedMotion)
            {
                // Once, then nothing. Re-writing zeros every frame would still be a write, and the
                // point of the setting is that this body stops asking to be looked at.
                if (held) return;
                current = new FacePose();
                flapPhase = 0f;
                Write(current);
                held = true;
                return;
            }

            held = false;
            var wanted = presentation == null
                ? new FacePose()
                : For(presentation.Mood, presentation.Stress);

            if (presentation != null && presentation.IsSpeaking)
            {
                flapPhase += Time.deltaTime * FlapRate;
                wanted.JawOpen = Mathf.Clamp(
                    wanted.JawOpen + FlapFloor + FlapDepth * (0.5f + 0.5f * Mathf.Sin(flapPhase)), -1f, 1f);
            }
            else
            {
                flapPhase = 0f;
            }

            float blend = 1f - Mathf.Exp(-Ease * Time.deltaTime);
            current.Smile = Mathf.Lerp(current.Smile, wanted.Smile, blend);
            current.BrowUp = Mathf.Lerp(current.BrowUp, wanted.BrowUp, blend);
            current.BrowsIn = Mathf.Lerp(current.BrowsIn, wanted.BrowsIn, blend);
            current.EyeOpen = Mathf.Lerp(current.EyeOpen, wanted.EyeOpen, blend);
            current.Sneer = Mathf.Lerp(current.Sneer, wanted.Sneer, blend);
            // The flap is the one channel that must not be smoothed away; a mouth that eases open
            // and shut at eleven hertz never opens at all.
            current.JawOpen = wanted.JawOpen;
            Write(current);
        }

        /// <summary>
        /// Both sides of every paired channel, because a face with one brow is worse than a face
        /// with none. The player reads these fields directly on its own update.
        /// </summary>
        private void Write(in FacePose pose)
        {
            player.leftMouthSmile_Frown = pose.Smile;
            player.rightMouthSmile_Frown = pose.Smile;
            player.leftBrowUp_Down = pose.BrowUp;
            player.rightBrowUp_Down = pose.BrowUp;
            player.midBrowUp_Down = pose.BrowUp * 0.6f;
            player.browsIn = pose.BrowsIn;
            player.leftEyeOpen_Close = pose.EyeOpen;
            player.rightEyeOpen_Close = pose.EyeOpen;
            player.noseSneer = pose.Sneer;
            player.jawOpen_Close = pose.JawOpen;
        }
    }
}

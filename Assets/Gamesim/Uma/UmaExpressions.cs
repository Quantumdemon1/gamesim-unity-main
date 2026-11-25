using Gamesim.Presentation;
using UMA.CharacterSystem;
using UMA.PoseTools;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// The face a UMA houseguest wears, from the two words the simulation already keeps about them.
    ///
    /// <para>A UMA head has brows and a mouth to move, and UMA ships a player for them:
    /// <see cref="UMAExpressionPlayer"/> drives fifty-one pose channels on the built skeleton. So the
    /// simulation's <c>mood</c> × <c>stressLevel</c> lowers a brow and turns a mouth, read from the
    /// presentation every frame.</para>
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
        /// A face in UMA's own units: −1 to 1 for the paired channels, 0 to 1 for
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
            /// <summary>Positive opens the jaw.</summary>
            public float JawOpen;
            public float MouthNarrow;

            /// <summary>The largest channel, for "did this face change at all" questions.</summary>
            public float Magnitude => Mathf.Max(
                Mathf.Max(Mathf.Abs(Smile), Mathf.Abs(BrowUp)),
                Mathf.Max(Mathf.Max(BrowsIn, Mathf.Abs(EyeOpen)), Mathf.Max(Sneer, Mathf.Max(Mathf.Abs(JawOpen), MouthNarrow))));
        }

        /// <summary>How fast a face settles onto a new mood. Slower than a blink, faster than a beat.</summary>
        private const float Ease = 7f;
        private const float SpeechPhraseSeconds = 1.62f;

        private CharacterPresentation presentation;
        private DynamicCharacterAvatar avatar;
        private UMAExpressionPlayer player;
        private FacePose current;
        private float speechTime;
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
                    pose.Smile = 0.62f; pose.BrowUp = 0.22f; pose.EyeOpen = -0.12f;
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
                    // Stress can last a whole ceremony. Carry it in the brows and lids.
                    pose.BrowsIn += 0.25f; pose.BrowUp += 0.24f; pose.EyeOpen += 0.28f;
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

        // The presentation supplies turns, not a voice track. Give the speaker short groups of
        // syllables with lip contact and a breath between phrases.
        public static void AddSpeech(ref FacePose pose, float elapsed)
        {
            float t = Mathf.Repeat(elapsed, SpeechPhraseSeconds);
            float first = Beat(t, 0f, .10f, .24f);
            float rounded = Beat(t, .27f, .40f, .59f);
            float shortBeat = Beat(t, .64f, .73f, .84f);
            float last = Beat(t, .90f, 1.03f, 1.25f);
            pose.JawOpen = Mathf.Max(pose.JawOpen, .16f * first + .10f * rounded + .075f * shortBeat + .13f * last);
            pose.MouthNarrow = Mathf.Max(pose.MouthNarrow, .14f * rounded + .055f * last);
        }

        private static float Beat(float time, float start, float peak, float end)
        {
            float amount = time <= peak ? Mathf.InverseLerp(start, peak, time) : Mathf.InverseLerp(end, peak, time);
            return Mathf.SmoothStep(0f, 1f, amount);
        }

        public static void AddReaction(ref FacePose pose, CharacterPresentation.Reaction kind, float seconds, float weight)
        {
            float beat = Beat(seconds, 0f, .18f, 1.05f) * Mathf.Clamp01(weight);
            switch (kind)
            {
                case CharacterPresentation.Reaction.Shocked:
                    pose.BrowUp = Mathf.Lerp(pose.BrowUp, .46f, beat);
                    pose.EyeOpen = Mathf.Lerp(pose.EyeOpen, .32f, beat);
                    pose.Smile = Mathf.Lerp(pose.Smile, 0f, beat);
                    float breath = Beat(seconds, .07f, .26f, .75f) * Mathf.Clamp01(weight);
                    pose.JawOpen = Mathf.Max(pose.JawOpen, .16f * breath);
                    break;
                case CharacterPresentation.Reaction.Evicted:
                case CharacterPresentation.Reaction.Nominated:
                case CharacterPresentation.Reaction.Tearful:
                    pose.Smile = Mathf.Lerp(pose.Smile, -.21f, beat);
                    pose.BrowUp = Mathf.Lerp(pose.BrowUp, .20f, beat);
                    pose.BrowsIn = Mathf.Lerp(pose.BrowsIn, .24f, beat);
                    break;
                case CharacterPresentation.Reaction.Saved:
                case CharacterPresentation.Reaction.Cheered:
                case CharacterPresentation.Reaction.Won:
                case CharacterPresentation.Reaction.Embrace:
                    pose.Smile = Mathf.Lerp(pose.Smile, .40f, beat);
                    pose.EyeOpen = Mathf.Lerp(pose.EyeOpen, -.10f, beat);
                    break;
                case CharacterPresentation.Reaction.Furious:
                case CharacterPresentation.Reaction.StormOff:
                    pose.BrowsIn = Mathf.Lerp(pose.BrowsIn, .48f, beat);
                    pose.BrowUp = Mathf.Lerp(pose.BrowUp, -.35f, beat);
                    break;
            }
        }

        private void Awake()
        {
            presentation = GetComponentInParent<CharacterPresentation>();
            avatar = GetComponent<DynamicCharacterAvatar>();
            if (avatar == null) return;

            // UMA assigns the race's expression set to an existing player during the build.
            // Its player's Start also binds the bones, so keep it asleep until the rig exists.
            player = avatar.gameObject.GetComponent<UMAExpressionPlayer>();
            if (player == null)
                player = avatar.gameObject.AddComponent<UMAExpressionPlayer>();
            if (!HasBuiltRig())
                player.enabled = false;
        }

        private bool HasBuiltRig()
        {
            var data = avatar.umaData;
            if (data == null || data.dirty || data.skeleton == null) return false;

            // Match the animator sources checked by UMAExpressionPlayer.SetupBones. The jaw
            // itself is optional and UMA resolves its unmapped name on the completed rig.
            var rig = data.animator != null ? data.animator.avatar : null;
            if (rig == null && player.animator != null) rig = player.animator.avatar;
            if (rig == null) rig = GetComponentInChildren<Animator>()?.avatar;
            return rig != null && rig.isValid && rig.isHuman;
        }

        private bool Ready()
        {
            if (player == null) return false;
            if (avatar == null || avatar.umaData == null || avatar.activeRace.racedata == null) return false;
            if (player.enabled && player.expressionSet != null) return true;
            if (!HasBuiltRig()) return false;

            // The shipped Human 3.0 races have no expression set on their race asset. Without a
            // set UMA ignores the expression values and the body clips take over the mandible.
            if (player.expressionSet == null)
            {
                var race = avatar.activeRace.racedata;
                var set = race.expressionSet;
                if (set == null && (race.raceName == UmaCastLibrary.FemaleRace || race.raceName == UmaCastLibrary.MaleRace))
                    set = Resources.Load<UmaFaceRigDefaults>("Gamesim/FaceRigDefaults")?.Expressions;
                if (set == null) return false;
                player.expressionSet = set;
                if (player.enabled && player.umaData?.skeleton != null) player.Initialize();
            }
            if (player.enabled) return true;
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
                speechTime = 0f;
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
                speechTime = Mathf.Repeat(speechTime + Time.deltaTime, SpeechPhraseSeconds);
                AddSpeech(ref wanted, speechTime);
            }
            else
            {
                speechTime = 0f;
            }
            if (presentation != null && presentation.TryGetFacialReaction(out var reaction, out float elapsed, out float weight))
                AddReaction(ref wanted, reaction, elapsed, weight);

            float blend = 1f - Mathf.Exp(-Ease * Time.deltaTime);
            current.Smile = Mathf.Lerp(current.Smile, wanted.Smile, blend);
            current.BrowUp = Mathf.Lerp(current.BrowUp, wanted.BrowUp, blend);
            current.BrowsIn = Mathf.Lerp(current.BrowsIn, wanted.BrowsIn, blend);
            current.EyeOpen = Mathf.Lerp(current.EyeOpen, wanted.EyeOpen, blend);
            current.Sneer = Mathf.Lerp(current.Sneer, wanted.Sneer, blend);
            current.JawOpen = Mathf.MoveTowards(current.JawOpen, wanted.JawOpen, 2.8f * Time.deltaTime);
            current.MouthNarrow = Mathf.MoveTowards(current.MouthNarrow, wanted.MouthNarrow, 2.8f * Time.deltaTime);
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
            player.mouthNarrow_Pucker = pose.MouthNarrow;
        }
    }
}

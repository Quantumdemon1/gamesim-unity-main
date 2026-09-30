#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Talk stands up, and a seat is sat on (PACK8-PASS-PLAN A2), asked of a body rather than of
    /// a flag. The owner saw houseguests "sitting in the middle of the room" while they talked, and
    /// the seated flag was false the whole time: the standing talk played a take captured sitting,
    /// and the listen and the ceremony reactions were built on its first frame. So these measure
    /// the hips.
    ///
    /// <para>The body is the mocap takes' own Humanoid skeleton, driven by the shipped controller
    /// through <see cref="CharacterPresentation"/> the way a UMA body is, so a clone without UMA
    /// asks the same questions. It has no mesh, and needs none: the hips are the answer.</para>
    /// </summary>
    public sealed class HumanoidTalkAndSeatPlayModeTests
    {
        /// <summary>Any take's file carries the skeleton and the avatar it was captured on.</summary>
        private const string Skeleton = "Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_PoseAtEase.fbx";

        /// <summary>The controller's handle in Resources, which is how the body provider finds it at runtime.</summary>
        private const string ControllerHandle = "Animation/GamesimHumanoid";

        private const float FrameTime = 1f / 60f;

        /// <summary>
        /// A speaking turn long enough for the way in to hand the floor on: the first take is four
        /// seconds and hands on at 0.92 of it. A listening turn, and a ceremony beat played out and
        /// back to idle (the longest is 2.2 s).
        /// </summary>
        private const int SpeakingFrames = 270, ListeningFrames = 120, BeatFrames = 150;

        /// <summary>The controller's seats: every state whose name starts Sit.</summary>
        private static readonly string[] Sits = { "SitIdle", "SitTalk", "SitTalkB", "SitClap", "SitVictory" };
        private static readonly string[] Standing = { "Idle", "Talk", "TalkB", "TalkC", "Listen", "ReactNominated", "ReactSaved", "ReactEvicted" };

        private GameObject actor, chair;
        private SkeletonProvider provider;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (provider != null) CharacterBodySource.Unregister(provider);
            provider = null;
            if (actor != null) Object.Destroy(actor);
            if (chair != null) Object.Destroy(chair);
            yield return null;
        }

        /// <summary>
        /// A standing conversation keeps its hips above 0.8 of standing height: the speaker through
        /// the way in and the ring, the listener, and the three ceremony beats a standing nominee
        /// plays. Each of those sat the body down at 0.6 of its height before the fix.
        /// </summary>
        [UnityTest]
        public IEnumerator AStandingConversationKeepsItsHipsUp()
        {
            // The animator and the presentation both run on the frame's delta, so pinning it makes a
            // frame a sixtieth of a second however fast the batchmode runner draws.
            Time.captureDeltaTime = FrameTime;
            var visual = Attach("Standing talker", AnimatorUpdateMode.Normal);
            yield return null;
            var animator = provider.Animator;
            Assert.That(animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid, Is.True,
                "The skeleton at " + Skeleton + " is driven by a Humanoid animator on the shipped controller.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(hips, Is.Not.Null, "The skeleton maps its hips.");

            for (int frame = 0; frame < 30; frame++) yield return null;
            float standing = 0f;
            for (int frame = 0; frame < 30; frame++) { yield return null; standing += Height(hips) / 30f; }
            Assert.That(StateOf(animator), Is.EqualTo("Idle"), "Measured standing in idle.");
            Assert.That(standing, Is.GreaterThan(.5f), "An idle skeleton holds its hips well off the floor.");

            float lowest = float.PositiveInfinity;
            string lowestIn = null;
            var played = new HashSet<string>();
            void Sample()
            {
                float height = Height(hips);
                string state = StateOf(animator);
                if (state != null) played.Add(state);
                if (height < lowest) { lowest = height; lowestIn = state ?? "a state this test does not name"; }
            }

            // A conversation as the house has one: the floor, then listening, then the floor again.
            visual.SetTalking(true);
            foreach (bool speaks in new[] { true, false, true })
            {
                visual.SetSpeaking(speaks);
                for (int frame = 0; frame < (speaks ? SpeakingFrames : ListeningFrames); frame++) { yield return null; Sample(); }
            }
            visual.SetTalking(false);
            // A standing nominee named, saved and evicted.
            foreach (var beat in new[] { CharacterPresentation.Reaction.Nominated, CharacterPresentation.Reaction.Saved,
                         CharacterPresentation.Reaction.Evicted })
            {
                visual.React(beat);
                for (int frame = 0; frame < BeatFrames; frame++) { yield return null; Sample(); }
            }

            Assert.That(played, Is.SupersetOf(new[] { "Talk", "TalkC", "Listen", "ReactNominated", "ReactSaved", "ReactEvicted" }),
                "The conversation played the way in, the ring's second take, the listen and the three beats; it played: "
                + string.Join(", ", played));
            Assert.That(lowest, Is.GreaterThanOrEqualTo(.8f * standing),
                "The hips came down to " + (lowest / standing).ToString("0.00") + " of standing height in " + lowestIn
                + ": a standing state playing a seated pose. Regenerate the clips and the controller "
                + "(-executeMethod Gamesim.Editor.HumanoidReactionAuthoring.BuildFromCommandLine).");
        }

        /// <summary>
        /// A seat is sat on: the body steps onto it on its feet and sits there, and gets up there -
        /// when the seat asks it to, and when its owner lets go straight after asking, as a
        /// conversation's end does (HouseMeetingCoordinator.Retire). On every frame the controller
        /// plays a sit, the hips are within 0.3 m of the seat. The sit used to be cued on the first
        /// frame, 0.85 m short of the seat, and a let-go seat snapped the body off the chair with
        /// the sit still playing.
        /// </summary>
        [UnityTest]
        public IEnumerator ASeatIsSatOnAndGotUpFrom()
        {
            // The seat keeps the real clock (unscaled time), and so does this body's animator, so
            // the two keep time together however long a batchmode frame is.
            var visual = Attach("Seated talker", AnimatorUpdateMode.UnscaledTime);
            yield return null;
            var animator = provider.Animator;
            Assert.That(animator != null && animator.isHuman, Is.True, "The skeleton is driven by a Humanoid animator.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float idle = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < idle) yield return null;

            // A chair 0.85 m in front of the root, facing it, as a couch faces its approach.
            chair = new GameObject("Test chair");
            chair.transform.position = actor.transform.position + Vector3.forward * .85f;
            var anchor = HouseInteractionAnchor.Create(chair.transform, "test-seat", "Living", 0, chair.transform.position, 180f, true);
            bool owns = true;
            var seat = actor.AddComponent<HouseSeatPresentation>();
            var off = new List<string>();
            float began = 0f;
            void Check(string moment)
            {
                if (visual.IsSeated && !seat.Active && off.Count < 12) off.Add(moment + ": seated with no seat pose holding the body");
                if (!PlaysASit(animator)) return;
                float flat = new Vector2(hips.position.x - anchor.SeatContact.x, hips.position.z - anchor.SeatContact.z).magnitude;
                if (flat > .3f && off.Count < 12)
                    off.Add(moment + " at " + (Time.realtimeSinceStartup - began).ToString("0.00") + " s: the hips "
                        + flat.ToString("0.00") + " m from the seat in " + (StateOf(animator) ?? "a transition"));
            }

            began = Time.realtimeSinceStartup;
            seat.Begin(anchor, () => owns);
            Assert.That(seat.Active && seat.IsStepping, Is.True, "The body steps onto the seat first.");
            Assert.That(visual.IsSeated, Is.False, "on its feet: it does not sit where it stands.");
            bool firstSit = true;
            float deadline = Time.realtimeSinceStartup + 5f, settledAt = float.PositiveInfinity;
            while (Time.realtimeSinceStartup < deadline && Time.realtimeSinceStartup < settledAt + .3f)
            {
                yield return null;
                if (seat.Settled && float.IsPositiveInfinity(settledAt)) settledAt = Time.realtimeSinceStartup;
                if (visual.IsSeated && firstSit)
                {
                    firstSit = false;
                    Assert.That(Vector3.Distance(seat.VisualFeet, anchor.Position), Is.LessThan(.05f),
                        "The body sits once it is on the seat, not on the way there.");
                    Assert.That(Time.realtimeSinceStartup - began, Is.GreaterThanOrEqualTo(HouseSeatPresentation.StepSeconds * .9f),
                        "and not before the step is over.");
                }
                Check("sitting down");
            }
            Assert.That(seat.Settled, Is.True, "The body settles in the seat.");
            Assert.That(visual.IsSeated, Is.True);

            began = Time.realtimeSinceStartup;
            seat.RequestExit();
            deadline = Time.realtimeSinceStartup + 3f;
            while (seat.Active && Time.realtimeSinceStartup < deadline) { yield return null; Check("getting up"); }
            Assert.That(seat.Active, Is.False, "The body gets up.");
            Assert.That(visual.VisualRoot.localPosition.magnitude, Is.LessThan(.01f), "and is back over its root.");

            // Again, let go as a conversation's end lets go: the seat asked to stand, then its owner gone.
            owns = true;
            began = Time.realtimeSinceStartup;
            seat.Begin(anchor, () => owns);
            deadline = Time.realtimeSinceStartup + 5f;
            while (!seat.Settled && Time.realtimeSinceStartup < deadline) { yield return null; Check("sitting down again"); }
            Assert.That(seat.Settled, Is.True, "Seated again.");
            began = Time.realtimeSinceStartup;
            seat.RequestExit();
            owns = false;
            yield return null;
            Assert.That(seat.Active && seat.IsExiting, Is.True, "The stand-up outlives its owner letting go.");
            deadline = Time.realtimeSinceStartup + 3f;
            while (seat.Active && Time.realtimeSinceStartup < deadline) { yield return null; Check("getting up after the owner let go"); }
            Assert.That(seat.Active, Is.False, "and finishes.");

            Assert.That(off, Is.Empty, "A sit played away from the seat:\n" + string.Join("\n", off));
        }

        private CharacterPresentation Attach(string name, AnimatorUpdateMode clock)
        {
            actor = new GameObject(name);
            actor.transform.position = new Vector3(1000f, 0f, 1000f);
            provider = new SkeletonProvider(clock);
            CharacterBodySource.Register(provider);
            var person = ContentCatalog.Create(91).Find(ContentCatalog.PlayerId);
            person.appearance = CharacterAppearance.Preset("player");
            var visual = CharacterPresentation.Attach(actor, person, Color.white);
            visual.SetReducedMotion(false);
            Assert.That(provider.Animator, Is.Not.Null, "The body is the mocap skeleton, not the primitive rig.");
            return visual;
        }

        private float Height(Transform hips) => hips.position.y - actor.transform.position.y;

        private static string StateOf(Animator animator)
        {
            var now = animator.GetCurrentAnimatorStateInfo(0);
            foreach (var name in Standing.Concat(Sits)) if (now.IsName(name)) return name;
            return null;
        }

        /// <summary>Whether the controller is playing a sit, or crossing into or out of one.</summary>
        private static bool PlaysASit(Animator animator)
        {
            var now = animator.GetCurrentAnimatorStateInfo(0);
            if (Sits.Any(name => now.IsName(name))) return true;
            if (!animator.IsInTransition(0)) return false;
            var next = animator.GetNextAnimatorStateInfo(0);
            return Sits.Any(name => next.IsName(name));
        }

        /// <summary>
        /// Builds the mocap skeleton as a body: the take's model with its own avatar, on the
        /// controller's Resources handle, animating whether or not a camera sees it.
        /// </summary>
        private sealed class SkeletonProvider : IModularCharacterBodyProvider
        {
            private readonly AnimatorUpdateMode clock;
            public SkeletonProvider(AnimatorUpdateMode clock) { this.clock = clock; }
            public Animator Animator { get; private set; }
            public ICharacterAppearanceCatalog Catalog => null;

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                body = default;
                var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Skeleton);
                var avatar = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(Skeleton).OfType<Avatar>().FirstOrDefault();
                var controller = Resources.Load<RuntimeAnimatorController>(ControllerHandle);
                if (model == null || avatar == null || controller == null) return false;
                var root = Object.Instantiate(model, parent, false);
                root.name = "Mocap skeleton";
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = clock;
                Animator = animator;
                body = new CharacterBody(root, animator, false, this);
                return true;
            }

            public bool TryCreate(string id, Transform parent, Color colour, out CharacterBody body) { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color colour) { }
        }
    }
}
#endif

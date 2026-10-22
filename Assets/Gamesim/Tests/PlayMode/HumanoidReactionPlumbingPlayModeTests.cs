#if UNITY_EDITOR
using System.Collections;
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
    /// The plumbing under a reaction (the art brief's R1), asked of the mocap takes' own skeleton
    /// driven by the shipped controller through <see cref="CharacterPresentation"/>, the way a UMA
    /// body is, so a clone without UMA asks the same questions.
    ///
    /// <para>A beat's trigger sent to a body at the stove waited behind the activity's guard on
    /// every reaction edge and fired when the cooking ended, out of its moment; a beat had no way
    /// to be stopped; and a body sent walking in the middle of one skated there acting it out.</para>
    /// </summary>
    public sealed class HumanoidReactionPlumbingPlayModeTests
    {
        private const string Skeleton = "Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_PoseAtEase.fbx";
        private const string ControllerHandle = "Animation/GamesimHumanoid";
        private const float FrameTime = 1f / 60f;

        private GameObject actor;
        private SkeletonProvider provider;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (provider != null) CharacterBodySource.Unregister(provider);
            provider = null;
            if (actor != null) Object.Destroy(actor);
            yield return null;
        }

        /// <summary>
        /// A beat asked of a body at the stove is dropped, not kept: once the cooking ends, the
        /// nominee's beat is not played in the second after.
        /// </summary>
        [UnityTest]
        public IEnumerator ATriggerSentDuringAnActivityIsDropped()
        {
            Time.captureDeltaTime = FrameTime;
            var visual = Attach("Cook");
            yield return Settle();
            var animator = provider.Animator;
            Assert.That(visual.CanAct(CharacterPresentation.BodyActivity.Cooking), Is.True, "The shipped controller cooks.");
            visual.SetActivity(CharacterPresentation.BodyActivity.Cooking);
            for (int frame = 0; frame < 120 && !animator.GetCurrentAnimatorStateInfo(0).IsName("Cook"); frame++) yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Cook"), Is.True, "The body is at the stove.");

            visual.React(CharacterPresentation.Reaction.Nominated);
            Assert.That(animator.GetBool("ReactNominated"), Is.True, "The beat was asked of the animator.");
            for (int frame = 0; frame < 60 && animator.GetBool("ReactNominated"); frame++) yield return null;
            Assert.That(animator.GetBool("ReactNominated"), Is.False, "The beat's trigger is dropped while the body cooks.");

            visual.SetActivity(CharacterPresentation.BodyActivity.None);
            bool played = false;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                var now = animator.GetCurrentAnimatorStateInfo(0);
                played |= now.IsName("ReactNominated") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("ReactNominated"));
            }
            Assert.That(played, Is.False, "The nominee's beat did not fire a second after the cooking ended.");
        }

        /// <summary>A beat under way is stopped: the body is back in its idle within 0.3 s.</summary>
        [UnityTest]
        public IEnumerator CancelReactionReturnsToIdle()
        {
            Time.captureDeltaTime = FrameTime;
            var visual = Attach("Cancelled");
            yield return Settle();
            var animator = provider.Animator;
            visual.React(CharacterPresentation.Reaction.Shocked);
            for (int frame = 0; frame < 30 && !visual.IsReacting; frame++) yield return null;
            Assert.That(visual.IsReacting, Is.True, "The beat is under way, under the reaction tag.");
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("ReactShocked"), Is.True, "and playing the story take itself.");

            visual.CancelReaction();
            int frames = 0;
            for (; frames < 18; frames++)
            {
                yield return null;
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0)) break;
            }
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0), Is.True,
                "The body is back in its idle within 0.3 s of the cancel.");
            Assert.That(visual.IsReacting, Is.False);
            Assert.That(animator.GetBool("ReactShocked"), Is.False, "and nothing is left to fire.");
        }

        /// <summary>
        /// A beat begun by a body standing still gives way to the walk once the body is sent
        /// somewhere: moving at 2 m/s, it is walking within half a second.
        /// </summary>
        [UnityTest]
        public IEnumerator ABeatStartedStillGivesWayToTheWalk()
        {
            Time.captureDeltaTime = FrameTime;
            var visual = Attach("Walker");
            yield return Settle();
            var animator = provider.Animator;
            Assert.That(visual.IsStill, Is.True);
            // A beat that plays out over a step in the controller: only the presentation's give-way
            // can take it to the walk.
            visual.React(CharacterPresentation.Reaction.Shocked);
            for (int frame = 0; frame < 30 && !visual.IsReacting; frame++) yield return null;
            Assert.That(visual.IsReacting, Is.True);
            for (int frame = 0; frame < 10; frame++) yield return null;

            bool walking = false;
            for (int frame = 0; frame < 30 && !walking; frame++)
            {
                actor.transform.position += Vector3.forward * (2f * FrameTime);
                yield return null;
                walking = animator.GetCurrentAnimatorStateInfo(0).IsName("Walk")
                          || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Walk"));
            }
            Assert.That(walking, Is.True, "The beat gave way to the walk within 0.5 s of the body moving at 2 m/s.");
        }

        /// <summary>
        /// Two mocap skeletons 0.9 m apart embrace: both beats begin on the same frame, and at the
        /// hold the left hand is above the right, each head turned to its right, the chests apart
        /// and no hand near the partner's. Released at the end, each visual body is home.
        /// </summary>
        [UnityTest]
        public IEnumerator TheEmbraceInterlocksOnTheMocapSkeleton()
        {
            Time.captureDeltaTime = FrameTime;
            var first = Attach("First");
            var firstAnimator = provider.Animator;
            actor.transform.SetPositionAndRotation(new Vector3(1000f, 0f, 1000f), Quaternion.identity);
            var firstActor = actor;
            var second = AttachAnother("Second", new Vector3(1000f, 0f, 1000.9f), Quaternion.Euler(0f, 180f, 0f));
            var secondAnimator = provider.Animator;
            try
            {
                for (int frame = 0; frame < 40; frame++) yield return null;
                // The head's own forward, read while it faces the way the body does.
                var firstHead = firstAnimator.GetBoneTransform(HumanBodyBones.Head);
                var secondHead = secondAnimator.GetBoneTransform(HumanBodyBones.Head);
                var firstFace = Quaternion.Inverse(firstHead.rotation) * firstActor.transform.forward;
                var secondFace = Quaternion.Inverse(secondHead.rotation) * actor.transform.forward;

                var pair = EmbracePair.TryBegin(first, second);
                Assert.That(pair, Is.Not.Null, EmbracePair.Refusal(first, second, null));
                // The distance and the step are the take's, scaled by these bodies' size over the size of
                // the skeleton the take was authored on (Gate 0's export, 1.0419588; this older mocap
                // body measures 1.029).
                float scale = (firstAnimator.humanScale + secondAnimator.humanScale) * .5f / EmbracePair.SkeletonHumanScale;
                Assert.That(pair.Distance, Is.EqualTo(EmbracePair.PairDistance * scale).Within(1e-4f), "The pair distance scales with the bodies,");
                Assert.That(pair.Step, Is.EqualTo(EmbracePair.SideStep * scale).Within(1e-4f), "and so does the side step.");
                int firstIn = -1, secondIn = -1;
                for (int frame = 0; frame < 120 && (firstIn < 0 || secondIn < 0); frame++)
                {
                    yield return null;
                    if (firstIn < 0 && first.IsReacting) firstIn = frame;
                    if (secondIn < 0 && second.IsReacting) secondIn = frame;
                }
                Assert.That(firstIn, Is.GreaterThanOrEqualTo(0), "The embrace began.");
                Assert.That(secondIn, Is.EqualTo(firstIn), "Both bodies began it on the same frame.");
                Assert.That(firstAnimator.GetCurrentAnimatorStateInfo(0).IsName("ReactEmbrace")
                    || firstAnimator.GetNextAnimatorStateInfo(0).IsName("ReactEmbrace"), Is.True);

                // The hold: frame 40 of 114 - of the embrace itself, not of the idle it is fading from.
                bool InTheHold()
                {
                    var now = firstAnimator.GetCurrentAnimatorStateInfo(0);
                    return now.IsName("ReactEmbrace") && !firstAnimator.IsInTransition(0) && now.normalizedTime >= 40f / 114f;
                }
                for (int frame = 0; frame < 240 && !InTheHold(); frame++) yield return null;
                Assert.That(firstAnimator.GetCurrentAnimatorStateInfo(0).IsName("ReactEmbrace"), Is.True, "Measured in the hold.");
                foreach (var (animator, root, face, head) in new[] { (firstAnimator, firstActor.transform, firstFace, firstHead), (secondAnimator, actor.transform, secondFace, secondHead) })
                {
                    float lift = animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y - animator.GetBoneTransform(HumanBodyBones.RightHand).position.y;
                    Assert.That(lift, Is.GreaterThanOrEqualTo(.15f), "The left hand goes over the partner's shoulder, the right under: " + lift.ToString("0.000"));
                    var turned = head.rotation * face; turned.y = 0f;
                    var ahead = root.forward; ahead.y = 0f;
                    float yaw = Vector3.SignedAngle(ahead, turned, Vector3.up);
                    Assert.That(yaw, Is.GreaterThanOrEqualTo(20f), "Each head turns to its own right: " + yaw.ToString("0.0"));
                }
                var chestA = firstAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chestA == null) chestA = firstAnimator.GetBoneTransform(HumanBodyBones.Chest);
                var chestB = secondAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chestB == null) chestB = secondAnimator.GetBoneTransform(HumanBodyBones.Chest);
                Assert.That(Vector3.Distance(chestA.position, chestB.position), Is.GreaterThanOrEqualTo(.15f), "The chests are not inside each other.");
                foreach (var a in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                    foreach (var b in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                        Assert.That(Vector3.Distance(firstAnimator.GetBoneTransform(a).position, secondAnimator.GetBoneTransform(b).position),
                            Is.GreaterThanOrEqualTo(.05f), a + " and the partner's " + b + " stay apart");

                for (int frame = 0; frame < 400 && pair != null && !pair.Released; frame++) yield return null;
                Assert.That(pair == null || pair.Released, Is.True, "It lets go when the take is done.");
                for (int frame = 0; frame < 40 && pair != null; frame++) yield return null;
                Assert.That(first.VisualRoot.localPosition.magnitude, Is.LessThan(.001f), "The first body is home over its root.");
                Assert.That(second.VisualRoot.localPosition.magnitude, Is.LessThan(.001f), "and so is the second.");
            }
            finally
            {
                if (firstActor != null) Object.Destroy(firstActor);
            }
        }

        /// <summary>
        /// Diagnostic local-pair control, not a played story/ceremony: real provided mocap bodies
        /// at the failed goodbye's approximately 3.34 m root spacing must refuse safely.
        /// </summary>
        [UnityTest]
        public IEnumerator AProvidedEmbraceRefusesDistantRootsWithoutWarpingOrRequestingTheBeat()
        {
            Time.captureDeltaTime = FrameTime;
            var first = Attach("Distant first");
            var firstActor = actor;
            try
            {
                var second = AttachAnother("Distant second", new Vector3(1000f, 0f, 1003.34f), Quaternion.Euler(0f, 180f, 0f));
                for (int frame = 0; frame < 40; frame++) yield return null;
                Assert.That(first.IsPrimitiveBody || second.IsPrimitiveBody, Is.False);
                Assert.That(first.Supports(CharacterPresentation.Reaction.Embrace)
                    && second.Supports(CharacterPresentation.Reaction.Embrace), Is.True);
                Assert.That(first.IsStill && second.IsStill, Is.True);
                var firstAt = first.transform.position;
                var secondAt = second.transform.position;
                var firstVisual = first.VisualRoot.localPosition;
                var secondVisual = second.VisualRoot.localPosition;
                Assert.That(Vector3.Distance(firstAt, secondAt), Is.GreaterThan(EmbracePair.FarthestApart));
                Assert.That(EmbracePair.Refusal(first, second, null), Does.Contain("m apart"));
                var pair = EmbracePair.TryBegin(first, second);
                Assert.That(pair, Is.Null, "The radius is not widened to cover the authored chairs' outer approaches.");
                yield return null; yield return null;
                Assert.That(first.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Embrace));
                Assert.That(second.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Embrace));
                Assert.That(first.transform.position, Is.EqualTo(firstAt));
                Assert.That(second.transform.position, Is.EqualTo(secondAt));
                Assert.That(first.VisualRoot.localPosition, Is.EqualTo(firstVisual));
                Assert.That(second.VisualRoot.localPosition, Is.EqualTo(secondVisual));
            }
            finally { if (firstActor != null) Object.Destroy(firstActor); }
        }

        private CharacterPresentation AttachAnother(string name, Vector3 at, Quaternion facing)
        {
            actor = new GameObject(name);
            actor.transform.SetPositionAndRotation(at, facing);
            var person = ContentCatalog.Create(92).Find(ContentCatalog.PlayerId);
            person.appearance = CharacterAppearance.Preset("player");
            var visual = CharacterPresentation.Attach(actor, person, Color.white);
            visual.SetReducedMotion(false);
            visual.SetFacing(facing.eulerAngles.y);
            return visual;
        }

        private IEnumerator Settle()
        {
            for (int frame = 0; frame < 40; frame++) yield return null;
            Assert.That(provider.Animator != null && provider.Animator.isHuman, Is.True, "The skeleton is driven by a Humanoid animator.");
            Assert.That(provider.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True, "and stands in its idle.");
        }

        private CharacterPresentation Attach(string name)
        {
            actor = new GameObject(name);
            actor.transform.position = new Vector3(1000f, 0f, 1000f);
            provider = new SkeletonProvider();
            CharacterBodySource.Register(provider);
            var person = ContentCatalog.Create(91).Find(ContentCatalog.PlayerId);
            person.appearance = CharacterAppearance.Preset("player");
            var visual = CharacterPresentation.Attach(actor, person, Color.white);
            visual.SetReducedMotion(false);
            Assert.That(provider.Animator, Is.Not.Null, "The body is the mocap skeleton, not the primitive rig.");
            return visual;
        }

        /// <summary>The mocap skeleton as a body, on the controller's Resources handle, as HumanoidTalkAndSeatPlayModeTests builds it.</summary>
        private sealed class SkeletonProvider : IModularCharacterBodyProvider
        {
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
                animator.updateMode = AnimatorUpdateMode.Normal;
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

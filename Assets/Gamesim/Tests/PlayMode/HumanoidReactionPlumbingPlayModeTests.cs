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

using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The farewell's planted stance on a UMA body: taken only from a settled Idle, held through the
    /// take and the first frame back in Idle, and let go - once the body has settled, or at once when
    /// another beat takes the body.
    /// </summary>
    public sealed class UmaFarewellStancePlayModeTests
    {
        private GameObject cast, holder;
        private CharacterPresentation body;
        private Animator animator;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            cast = new GameObject("Farewell stance UMA cast", typeof(GamesimUmaCast));
            yield return null;
            holder = new GameObject("Farewell stance alex-chen");
            holder.transform.position = new Vector3(2200f, 0f, 2200f);
            var person = ContentCatalog.Create(1).contestants.First(p => !p.isPlayer);
            person.id = "alex-chen";
            body = CharacterPresentation.Attach(holder, person, Color.white);
            body.SetReducedMotion(false);
            float by = Time.realtimeSinceStartup + 30f;
            for (int frame = 0; frame < 900 && Time.realtimeSinceStartup < by; frame++)
            {
                if (body.TryReadFarewell(out var pose) && pose.Idle) break;
                yield return null;
            }
            Assert.That(body.TryReadFarewell(out var idle) && idle.Idle && !body.IsPrimitiveBody, Is.True,
                "An assembled UMA body standing in its Idle.");
            animator = body.VisualRoot.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (holder != null) Object.Destroy(holder);
            if (cast != null) Object.Destroy(cast);
            yield return null;
        }

        private static bool Holding(CharacterPresentation body) => (bool)typeof(CharacterPresentation)
            .GetProperty("HoldsFarewellStance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(body);

        private bool Playing(string state) => animator.GetCurrentAnimatorStateInfo(0).IsName(state)
            || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state);

        [UnityTest]
        public IEnumerator TheStanceIsHeldThroughTheTakeAndLetGoOnceTheBodyIsBackInIdle()
        {
            body.React(CharacterPresentation.Reaction.Farewell);
            Assert.That(Holding(body), Is.True, "A farewell cued on a settled Idle takes hold of the stance.");
            bool entered = false, back = false;
            float by = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                Assert.That(body.TryReadFarewell(out var pose), Is.True);
                entered |= pose.InTake;
                Assert.That(Holding(body), Is.True, "Held from the cue until the body is back in Idle.");
                if (entered && pose.Idle) { back = true; break; }
            }
            Assert.That(entered && back, Is.True, "The actual take played and handed back to Idle.");
            // Still held on that first frame back: FarewellPair completes the transaction on it.
            by = Time.realtimeSinceStartup + 3f;
            while (Holding(body) && Time.realtimeSinceStartup < by)
            {
                Assert.That(body.TryReadFarewell(out var pose) && pose.Idle, Is.True, "Let go in Idle, not before.");
                yield return null;
            }
            Assert.That(Holding(body), Is.False, "The legs are the Idle's again once the body has settled.");
        }

        [UnityTest]
        public IEnumerator AnotherBeatTakesTheLegsBackAtOnce()
        {
            body.React(CharacterPresentation.Reaction.Farewell);
            float by = Time.realtimeSinceStartup + .5f;
            while ((!body.TryReadFarewell(out var pose) || !pose.InTake) && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(body.TryReadFarewell(out var entered) && entered.InTake && Holding(body), Is.True, "The take is playing, held.");
            Assert.That(body.Supports(CharacterPresentation.Reaction.Shocked), Is.True);
            body.React(CharacterPresentation.Reaction.Shocked);
            by = Time.realtimeSinceStartup + .5f;
            while (!Playing("ReactShocked") && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Playing("ReactShocked"), Is.True, "The newer beat actually took the body.");
            by = Time.realtimeSinceStartup + 2f;
            while (Holding(body) && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Holding(body), Is.False, "The shocked beat has the legs, not the farewell's stance.");
            Assert.That(Playing("ReactShocked"), Is.True, "Let go while the other beat plays, not after it.");
        }

        [UnityTest]
        public IEnumerator AFarewellCuedOutsideASettledIdleHoldsNothing()
        {
            body.React(CharacterPresentation.Reaction.Shocked);
            float by = Time.realtimeSinceStartup + .5f;
            while (!Playing("ReactShocked") && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Playing("ReactShocked"), Is.True);
            body.React(CharacterPresentation.Reaction.Farewell);
            Assert.That(Holding(body), Is.False, "Only a body standing in its Idle has a stance to keep.");
            yield return null;
            Assert.That(Holding(body), Is.False);
        }
    }
}
